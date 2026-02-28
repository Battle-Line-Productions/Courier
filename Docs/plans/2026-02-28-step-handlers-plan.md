# Protocol & Crypto Step Handlers — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build 19 step handlers (15 transfer + 4 crypto) that connect Connections and Keys into the job engine, enabling end-to-end secure file transfer pipelines.

**Architecture:** Thin step handlers delegate to `ITransferClient` (SSH.NET / FluentFTP) and `ICryptoProvider` (BouncyCastle). A `JobConnectionRegistry` reuses connections within a job execution. All step handlers are scoped (migrated from singleton).

**Tech Stack:** .NET 10, SSH.NET, FluentFTP, BouncyCastle 2.5.1, xUnit, Shouldly, NSubstitute, Testcontainers

**Design Doc:** `docs/plans/2026-02-28-step-handlers-design.md`

---

## Phase 1: Foundation — Domain Models & DI Migration

### Task 1: Add SSH.NET and FluentFTP packages

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/Courier.Features/Courier.Features.csproj`

**Step 1: Add package versions to central package management**

In `Directory.Packages.props`, add a new `ItemGroup` after the Cryptography group:

```xml
<ItemGroup Label="Transfer Protocols">
  <PackageVersion Include="SSH.NET" Version="2024.2.0" />
  <PackageVersion Include="FluentFTP" Version="51.1.0" />
</ItemGroup>
```

**Step 2: Add package references to Features project**

In `src/Courier.Features/Courier.Features.csproj`, add to the PackageReference ItemGroup:

```xml
<PackageReference Include="SSH.NET" />
<PackageReference Include="FluentFTP" />
```

**Step 3: Verify build**

Run: `dotnet build src/Courier.Features/Courier.Features.csproj`
Expected: Build succeeded

---

### Task 2: Add StepConfiguration convenience methods

**Files:**
- Modify: `src/Courier.Domain/Engine/StepConfiguration.cs`
- Test: `tests/Courier.Tests.Unit/Engine/StepConfigurationTests.cs`

**Step 1: Write tests for new methods**

Create `tests/Courier.Tests.Unit/Engine/StepConfigurationTests.cs`:

```csharp
using Courier.Domain.Engine;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class StepConfigurationTests
{
    [Fact]
    public void GetBoolOrDefault_MissingKey_ReturnsDefault()
    {
        var config = new StepConfiguration("{}");
        config.GetBoolOrDefault("missing", true).ShouldBeTrue();
    }

    [Fact]
    public void GetBoolOrDefault_PresentKey_ReturnsValue()
    {
        var config = new StepConfiguration("""{"flag": false}""");
        config.GetBoolOrDefault("flag", true).ShouldBeFalse();
    }

    [Fact]
    public void GetIntOrDefault_MissingKey_ReturnsDefault()
    {
        var config = new StepConfiguration("{}");
        config.GetIntOrDefault("missing", 42).ShouldBe(42);
    }

    [Fact]
    public void GetIntOrDefault_PresentKey_ReturnsValue()
    {
        var config = new StepConfiguration("""{"count": 7}""");
        config.GetIntOrDefault("count", 42).ShouldBe(7);
    }

    [Fact]
    public void GetStringArray_ReturnsValues()
    {
        var config = new StepConfiguration("""{"ids": ["aaa", "bbb"]}""");
        var result = config.GetStringArray("ids");
        result.ShouldBe(new[] { "aaa", "bbb" });
    }

    [Fact]
    public void GetStringArray_MissingKey_ReturnsEmpty()
    {
        var config = new StepConfiguration("{}");
        config.GetStringArray("ids").ShouldBeEmpty();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Courier.Tests.Unit --filter "StepConfigurationTests" -v q`
Expected: FAIL — methods don't exist

**Step 3: Add new methods to StepConfiguration**

In `src/Courier.Domain/Engine/StepConfiguration.cs`, add after the existing `GetBool` method:

```csharp
public bool GetBoolOrDefault(string key, bool defaultValue = false)
    => _root.TryGetProperty(key, out var prop) ? prop.GetBoolean() : defaultValue;

public int GetIntOrDefault(string key, int defaultValue = 0)
    => _root.TryGetProperty(key, out var prop) ? prop.GetInt32() : defaultValue;

public string[] GetStringArray(string key)
{
    if (!_root.TryGetProperty(key, out var prop))
        return [];
    return prop.EnumerateArray().Select(e => e.GetString()!).ToArray();
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Courier.Tests.Unit --filter "StepConfigurationTests" -v q`
Expected: PASS (6 passed)

---

### Task 3: Add error codes for transfer and crypto operations

**Files:**
- Modify: `src/Courier.Domain/Common/ErrorCodes.cs`

**Step 1: Add new error code constants**

In `ErrorCodes` class, after the Keys section add:

```csharp
// Transfer Operations (5000-5999)
public const int TransferFailed = 5000;
public const int ConnectionNotFound = 5001;
public const int ProtocolMismatch = 5002;
public const int RemotePathNotFound = 5003;
public const int UploadFailed = 5010;
public const int DownloadFailed = 5011;
public const int ConnectionLost = 5020;
public const int HostKeyVerificationFailed = 5021;
public const int TlsCertificateRejected = 5022;
public const int ResumeNotSupported = 5030;

// Crypto Operations (6000-6999)
public const int EncryptionFailed = 6000;
public const int DecryptionFailed = 6001;
public const int SigningFailed = 6002;
public const int VerificationFailed = 6003;
public const int KeyStatusInvalid = 6010;
public const int KeyTypeInvalid = 6011;
public const int WrongDecryptionKey = 6020;
public const int CorruptedCiphertext = 6021;
```

**Step 2: Add corresponding error messages in the `SystemMessages` dictionary**

Add entries for each new code following the existing pattern.

**Step 3: Verify build**

Run: `dotnet build src/Courier.Domain/Courier.Domain.csproj`
Expected: Build succeeded

---

### Task 4: Create domain models — ITransferClient and transfer records

**Files:**
- Create: `src/Courier.Domain/Protocols/ITransferClient.cs`
- Create: `src/Courier.Domain/Protocols/TransferModels.cs`

**Step 1: Create ITransferClient interface**

Create `src/Courier.Domain/Protocols/ITransferClient.cs`:

```csharp
namespace Courier.Domain.Protocols;

public interface ITransferClient : IAsyncDisposable
{
    string Protocol { get; }
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken ct);
    Task DisconnectAsync();

    Task UploadAsync(UploadRequest request, IProgress<TransferProgress>? progress, CancellationToken ct);
    Task DownloadAsync(DownloadRequest request, IProgress<TransferProgress>? progress, CancellationToken ct);
    Task RenameAsync(string oldPath, string newPath, CancellationToken ct);
    Task DeleteFileAsync(string remotePath, CancellationToken ct);

    Task<IReadOnlyList<RemoteFileInfo>> ListDirectoryAsync(string remotePath, CancellationToken ct);
    Task CreateDirectoryAsync(string remotePath, CancellationToken ct);
    Task DeleteDirectoryAsync(string remotePath, bool recursive, CancellationToken ct);

    Task<ConnectionTestResult> TestAsync(CancellationToken ct);
}
```

**Step 2: Create transfer model records**

Create `src/Courier.Domain/Protocols/TransferModels.cs`:

```csharp
namespace Courier.Domain.Protocols;

public record UploadRequest(
    string LocalPath,
    string RemotePath,
    bool AtomicUpload = true,
    string AtomicSuffix = ".tmp",
    bool ResumePartial = false);

public record DownloadRequest(
    string RemotePath,
    string LocalPath,
    bool ResumePartial = false,
    string FilePattern = "*",
    bool DeleteAfterDownload = false);

public record TransferProgress(
    long BytesTransferred,
    long TotalBytes,
    string CurrentFile,
    double TransferRateBytesPerSec);

public record RemoteFileInfo(
    string Name,
    string FullPath,
    long Size,
    DateTime LastModified,
    bool IsDirectory);

public record ConnectionTestResult(
    bool Success,
    TimeSpan Latency,
    string? ServerBanner,
    string? ErrorMessage,
    IReadOnlyList<string>? SupportedAlgorithms);
```

**Step 3: Verify build**

Run: `dotnet build src/Courier.Domain/Courier.Domain.csproj`
Expected: Build succeeded

---

### Task 5: Create crypto models — ICryptoProvider and records

**Files:**
- Create: `src/Courier.Features/Engine/Crypto/ICryptoProvider.cs`
- Create: `src/Courier.Features/Engine/Crypto/CryptoModels.cs`

**Step 1: Create ICryptoProvider interface**

Create `src/Courier.Features/Engine/Crypto/ICryptoProvider.cs`:

```csharp
namespace Courier.Features.Engine.Crypto;

public interface ICryptoProvider
{
    Task<CryptoResult> EncryptAsync(EncryptRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct);
    Task<CryptoResult> DecryptAsync(DecryptRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct);
    Task<CryptoResult> SignAsync(SignRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct);
    Task<VerifyResult> VerifyAsync(VerifyRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct);
}
```

**Step 2: Create crypto model records and enums**

Create `src/Courier.Features/Engine/Crypto/CryptoModels.cs`:

```csharp
namespace Courier.Features.Engine.Crypto;

public record EncryptRequest(
    string InputPath,
    string OutputPath,
    IReadOnlyList<Guid> RecipientKeyIds,
    Guid? SigningKeyId,
    OutputFormat Format);

public record DecryptRequest(
    string InputPath,
    string OutputPath,
    Guid PrivateKeyId,
    bool VerifySignature);

public record SignRequest(
    string InputPath,
    string OutputPath,
    Guid SigningKeyId,
    SignatureMode Mode);

public record VerifyRequest(
    string InputPath,
    string? DetachedSignaturePath,
    Guid? ExpectedSignerKeyId);

public record CryptoResult(
    bool Success,
    long BytesProcessed,
    string OutputPath,
    string? ErrorMessage);

public record VerifyResult(
    bool IsValid,
    VerifyStatus Status,
    string? SignerFingerprint,
    DateTime? SignatureTimestamp);

public record CryptoProgress(
    long BytesProcessed,
    long TotalBytes,
    string Operation);

public enum VerifyStatus { Valid, Invalid, UnknownSigner, ExpiredKey, RevokedKey }
public enum OutputFormat { Armored, Binary }
public enum SignatureMode { Detached, Inline, Clearsign }
```

**Step 3: Verify build**

Run: `dotnet build src/Courier.Features/Courier.Features.csproj`
Expected: Build succeeded

---

### Task 6: Migrate DI lifetimes from singleton to scoped

**Files:**
- Modify: `src/Courier.Features/FeaturesServiceExtensions.cs`

**Step 1: Change existing step handler and registry registrations**

In `FeaturesServiceExtensions.cs`, change lines 26-28 from:

```csharp
services.AddSingleton<IJobStep, FileCopyStep>();
services.AddSingleton<IJobStep, FileMoveStep>();
services.AddSingleton<StepTypeRegistry>();
```

to:

```csharp
services.AddScoped<IJobStep, FileCopyStep>();
services.AddScoped<IJobStep, FileMoveStep>();
services.AddScoped<StepTypeRegistry>();
```

**Step 2: Run existing tests to verify nothing breaks**

Run: `dotnet test --filter "Category!=Integration" -v q`
Expected: All existing tests pass (the in-memory EF tests resolve scoped services from their own scope, and the engine tests create services directly)

---

## Phase 2: Protocol Infrastructure

### Task 7: Build SftpTransferClient

**Files:**
- Create: `src/Courier.Features/Engine/Protocols/SftpTransferClient.cs`
- Test: `tests/Courier.Tests.Unit/Engine/Protocols/SftpTransferClientTests.cs`

**Step 1: Write unit tests**

Create `tests/Courier.Tests.Unit/Engine/Protocols/SftpTransferClientTests.cs`:

```csharp
using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Protocols;

public class SftpTransferClientTests
{
    [Fact]
    public void Protocol_ReturnsSftp()
    {
        var client = CreateClient();
        client.Protocol.ShouldBe("sftp");
    }

    [Fact]
    public void IsConnected_BeforeConnect_ReturnsFalse()
    {
        var client = CreateClient();
        client.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task ConnectAsync_InvalidHost_ThrowsConnectionException()
    {
        var client = CreateClient(host: "invalid.host.that.does.not.exist", connectTimeout: 2);
        await Should.ThrowAsync<Exception>(() => client.ConnectAsync(CancellationToken.None));
    }

    private static SftpTransferClient CreateClient(
        string host = "localhost",
        int port = 22,
        string authMethod = "password",
        int connectTimeout = 30)
    {
        var connection = new Connection
        {
            Id = Guid.NewGuid(),
            Host = host,
            Port = port,
            Protocol = "sftp",
            AuthMethod = authMethod,
            Username = "testuser",
            HostKeyPolicy = "always_trust",
            ConnectTimeoutSec = connectTimeout,
            OperationTimeoutSec = 300,
            KeepaliveIntervalSec = 0,
            TransportRetries = 0,
        };
        return new SftpTransferClient(connection, "testpass"u8.ToArray(), null);
    }
}
```

**Step 2: Implement SftpTransferClient**

Create `src/Courier.Features/Engine/Protocols/SftpTransferClient.cs`. This is a substantial file (~350-400 lines). Key structure:

```csharp
using System.Diagnostics;
using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using Renci.SshNet;

namespace Courier.Features.Engine.Protocols;

public class SftpTransferClient : ITransferClient
{
    private readonly Connection _connection;
    private readonly byte[]? _decryptedPassword;
    private readonly byte[]? _sshPrivateKeyData;
    private SftpClient? _client;
    private Timer? _keepaliveTimer;

    public SftpTransferClient(Connection connection, byte[]? decryptedPassword, byte[]? sshPrivateKeyData)
    {
        _connection = connection;
        _decryptedPassword = decryptedPassword;
        _sshPrivateKeyData = sshPrivateKeyData;
    }

    public string Protocol => "sftp";
    public bool IsConnected => _client?.IsConnected ?? false;

    public async Task ConnectAsync(CancellationToken ct) { /* Build ConnectionInfo with auth methods, host key handler, connect */ }
    public async Task DisconnectAsync() { /* Dispose timer, disconnect client */ }
    public async Task UploadAsync(UploadRequest request, IProgress<TransferProgress>? progress, CancellationToken ct) { /* Atomic upload + resume + progress */ }
    public async Task DownloadAsync(DownloadRequest request, IProgress<TransferProgress>? progress, CancellationToken ct) { /* Resume + progress */ }
    public async Task RenameAsync(string oldPath, string newPath, CancellationToken ct) { /* SftpClient.RenameFile */ }
    public async Task DeleteFileAsync(string remotePath, CancellationToken ct) { /* SftpClient.DeleteFile */ }
    public async Task<IReadOnlyList<RemoteFileInfo>> ListDirectoryAsync(string remotePath, CancellationToken ct) { /* SftpClient.ListDirectory → map */ }
    public async Task CreateDirectoryAsync(string remotePath, CancellationToken ct) { /* Recursive mkdir */ }
    public async Task DeleteDirectoryAsync(string remotePath, bool recursive, CancellationToken ct) { /* SftpClient.DeleteDirectory */ }
    public async Task<ConnectionTestResult> TestAsync(CancellationToken ct) { /* Connect + list root + measure latency */ }
    public async ValueTask DisposeAsync() { /* Disconnect + dispose */ }
}
```

Implementation details for each method:

- **ConnectAsync**: Build `AuthenticationMethod[]` based on `_connection.AuthMethod` (`password` → `PasswordAuthenticationMethod`, `ssh_key` → `PrivateKeyAuthenticationMethod` from `_sshPrivateKeyData` stream, `password_and_ssh_key` → both). Set `ConnectionInfo` with host/port/username/auth. Wire up `HostKeyReceived` event based on `_connection.HostKeyPolicy` (always_trust → accept, trust_on_first_use → check/store via `_connection.StoredHostFingerprint`, manual → compare). Set connect timeout. Call `_client.Connect()` (SSH.NET connect is synchronous, wrap in `Task.Run`). Start keepalive timer if `KeepaliveIntervalSec > 0`.
- **UploadAsync**: If `AtomicUpload`, upload to `RemotePath + AtomicSuffix`, then rename. If `ResumePartial`, check remote file size, seek local stream. Use `_client.UploadFile()` with progress callback. Report progress every 1MB.
- **DownloadAsync**: If `ResumePartial`, check local file size, use offset. `_client.DownloadFile()`. If `DeleteAfterDownload`, delete remote after success.
- **ListDirectoryAsync**: `_client.ListDirectory(remotePath)` → filter by `FilePattern` if provided → map to `RemoteFileInfo`.
- **CreateDirectoryAsync**: Split path, create each segment if not exists.
- **TestAsync**: `Stopwatch` around connect + list root, return result.
- **DisposeAsync**: Stop keepalive timer, disconnect, dispose client.

**Step 3: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "SftpTransferClientTests" -v q`
Expected: PASS (Protocol and IsConnected tests pass; ConnectAsync invalid host test passes with exception)

---

### Task 8: Build FluentFtpTransferClient

**Files:**
- Create: `src/Courier.Features/Engine/Protocols/FluentFtpTransferClient.cs`
- Test: `tests/Courier.Tests.Unit/Engine/Protocols/FluentFtpTransferClientTests.cs`

**Step 1: Write unit tests**

Create `tests/Courier.Tests.Unit/Engine/Protocols/FluentFtpTransferClientTests.cs`:

```csharp
using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using FluentFTP;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Protocols;

public class FluentFtpTransferClientTests
{
    [Theory]
    [InlineData(FtpEncryptionMode.None, "ftp")]
    [InlineData(FtpEncryptionMode.Explicit, "ftps")]
    [InlineData(FtpEncryptionMode.Implicit, "ftps")]
    public void Protocol_ReturnsCorrectValue(FtpEncryptionMode mode, string expected)
    {
        var client = CreateClient(encryptionMode: mode);
        client.Protocol.ShouldBe(expected);
    }

    [Fact]
    public void IsConnected_BeforeConnect_ReturnsFalse()
    {
        var client = CreateClient();
        client.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task ConnectAsync_InvalidHost_ThrowsException()
    {
        var client = CreateClient(host: "invalid.host.does.not.exist", connectTimeout: 2);
        await Should.ThrowAsync<Exception>(() => client.ConnectAsync(CancellationToken.None));
    }

    private static FluentFtpTransferClient CreateClient(
        string host = "localhost",
        int port = 21,
        FtpEncryptionMode encryptionMode = FtpEncryptionMode.None,
        int connectTimeout = 30)
    {
        var connection = new Connection
        {
            Id = Guid.NewGuid(),
            Host = host,
            Port = port,
            Protocol = encryptionMode == FtpEncryptionMode.None ? "ftp" : "ftps",
            AuthMethod = "password",
            Username = "testuser",
            PassiveMode = true,
            TlsCertPolicy = "system_trust",
            ConnectTimeoutSec = connectTimeout,
            OperationTimeoutSec = 300,
            TransportRetries = 0,
        };
        return new FluentFtpTransferClient(connection, "testpass"u8.ToArray(), encryptionMode);
    }
}
```

**Step 2: Implement FluentFtpTransferClient**

Create `src/Courier.Features/Engine/Protocols/FluentFtpTransferClient.cs` (~300 lines):

```csharp
using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using FluentFTP;

namespace Courier.Features.Engine.Protocols;

public class FluentFtpTransferClient : ITransferClient
{
    private readonly Connection _connection;
    private readonly byte[]? _decryptedPassword;
    private readonly FtpEncryptionMode _encryptionMode;
    private AsyncFtpClient? _client;

    public FluentFtpTransferClient(Connection connection, byte[]? decryptedPassword, FtpEncryptionMode encryptionMode)
    {
        _connection = connection;
        _decryptedPassword = decryptedPassword;
        _encryptionMode = encryptionMode;
    }

    public string Protocol => _encryptionMode == FtpEncryptionMode.None ? "ftp" : "ftps";
    public bool IsConnected => _client?.IsConnected ?? false;

    // ConnectAsync: new AsyncFtpClient, set Host/Port/Credentials, EncryptionMode,
    //   DataConnectionType based on PassiveMode, TLS validation callback, connect timeout, Connect()
    // UploadAsync: atomic via UploadFile + Rename, resume via FtpRemoteExists.Resume
    // DownloadAsync: resume via FtpLocalExists.Resume, delete after if requested
    // Other methods map directly to AsyncFtpClient methods
}
```

Implementation details:
- **ConnectAsync**: Create `AsyncFtpClient(host, port)`, set credentials from `_decryptedPassword`, set `Config.EncryptionMode`, set `Config.DataConnectionType` based on `PassiveMode`. If FTPS, wire up `ValidateCertificate` event with policy check (SystemTrust/PinnedThumbprint/Insecure). Set connect timeout. Call `await _client.Connect(ct)`.
- **TLS validation**: `SystemTrust` → accept if `e.PolicyErrors == SslPolicyErrors.None`. `PinnedThumbprint` → compare `e.Certificate.GetCertHashString(HashAlgorithmName.SHA256)` against `_connection.TlsPinnedThumbprint`. `Insecure` → accept all.
- **UploadAsync**: If atomic, upload to temp path, then `_client.Rename()`. Resume via `FtpRemoteExists.Resume`. Progress via FluentFTP's `IProgress<FtpProgress>` adapter.
- **DownloadAsync**: `_client.DownloadFile()` with `FtpLocalExists.Resume` if requested. Delete after download via `_client.DeleteFile()`.

**Step 3: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FluentFtpTransferClientTests" -v q`
Expected: PASS

---

### Task 9: Build TransferClientFactory

**Files:**
- Create: `src/Courier.Features/Engine/Protocols/TransferClientFactory.cs`
- Test: `tests/Courier.Tests.Unit/Engine/Protocols/TransferClientFactoryTests.cs`

**Step 1: Write tests**

```csharp
using Courier.Domain.Entities;
using Courier.Features.Engine.Protocols;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Protocols;

public class TransferClientFactoryTests
{
    [Fact]
    public void Create_Sftp_ReturnsSftpClient()
    {
        var factory = new TransferClientFactory();
        var connection = MakeConnection("sftp", 22);
        var client = factory.Create(connection, null, null);
        client.ShouldBeOfType<SftpTransferClient>();
        client.Protocol.ShouldBe("sftp");
    }

    [Fact]
    public void Create_Ftp_ReturnsFluentFtpClient()
    {
        var factory = new TransferClientFactory();
        var connection = MakeConnection("ftp", 21);
        var client = factory.Create(connection, null, null);
        client.ShouldBeOfType<FluentFtpTransferClient>();
        client.Protocol.ShouldBe("ftp");
    }

    [Fact]
    public void Create_Ftps_ReturnsFluentFtpClient()
    {
        var factory = new TransferClientFactory();
        var connection = MakeConnection("ftps", 990);
        var client = factory.Create(connection, null, null);
        client.ShouldBeOfType<FluentFtpTransferClient>();
        client.Protocol.ShouldBe("ftps");
    }

    [Fact]
    public void Create_UnknownProtocol_Throws()
    {
        var factory = new TransferClientFactory();
        var connection = MakeConnection("s3", 443);
        Should.Throw<ArgumentException>(() => factory.Create(connection, null, null));
    }

    private static Connection MakeConnection(string protocol, int port) => new()
    {
        Id = Guid.NewGuid(),
        Name = "test",
        Protocol = protocol,
        Host = "localhost",
        Port = port,
        AuthMethod = "password",
        Username = "user",
        HostKeyPolicy = "always_trust",
        TlsCertPolicy = "system_trust",
    };
}
```

**Step 2: Implement TransferClientFactory**

Create `src/Courier.Features/Engine/Protocols/TransferClientFactory.cs`:

```csharp
using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using FluentFTP;

namespace Courier.Features.Engine.Protocols;

public class TransferClientFactory
{
    public ITransferClient Create(Connection connection, byte[]? decryptedPassword, byte[]? sshPrivateKey)
    {
        return connection.Protocol.ToLowerInvariant() switch
        {
            "sftp" => new SftpTransferClient(connection, decryptedPassword, sshPrivateKey),
            "ftp" => new FluentFtpTransferClient(connection, decryptedPassword, FtpEncryptionMode.None),
            "ftps" => new FluentFtpTransferClient(connection, decryptedPassword, DetermineEncryptionMode(connection)),
            _ => throw new ArgumentException($"Unsupported protocol: {connection.Protocol}")
        };
    }

    private static FtpEncryptionMode DetermineEncryptionMode(Connection connection)
        => connection.Port == 990 ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
}
```

**Step 3: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "TransferClientFactoryTests" -v q`
Expected: PASS (4 passed)

---

### Task 10: Build JobConnectionRegistry

**Files:**
- Create: `src/Courier.Features/Engine/Protocols/JobConnectionRegistry.cs`
- Test: `tests/Courier.Tests.Unit/Engine/Protocols/JobConnectionRegistryTests.cs`

**Step 1: Write tests**

```csharp
using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Protocols;

public class JobConnectionRegistryTests
{
    [Fact]
    public async Task GetOrOpenAsync_FirstCall_CreatesAndConnects()
    {
        var factory = Substitute.For<TransferClientFactory>();
        var mockClient = Substitute.For<ITransferClient>();
        mockClient.IsConnected.Returns(true);
        var connection = MakeConnection();
        factory.Create(connection, null, null).Returns(mockClient);

        var registry = new JobConnectionRegistry(factory);
        var client = await registry.GetOrOpenAsync(connection, null, null, CancellationToken.None);

        client.ShouldBe(mockClient);
        await mockClient.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrOpenAsync_SecondCall_ReusesConnection()
    {
        var factory = Substitute.For<TransferClientFactory>();
        var mockClient = Substitute.For<ITransferClient>();
        mockClient.IsConnected.Returns(true);
        var connection = MakeConnection();
        factory.Create(connection, null, null).Returns(mockClient);

        var registry = new JobConnectionRegistry(factory);
        var first = await registry.GetOrOpenAsync(connection, null, null, CancellationToken.None);
        var second = await registry.GetOrOpenAsync(connection, null, null, CancellationToken.None);

        first.ShouldBe(second);
        await mockClient.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisposeAsync_DisconnectsAll()
    {
        var factory = Substitute.For<TransferClientFactory>();
        var mockClient = Substitute.For<ITransferClient>();
        mockClient.IsConnected.Returns(true);
        var connection = MakeConnection();
        factory.Create(connection, null, null).Returns(mockClient);

        var registry = new JobConnectionRegistry(factory);
        await registry.GetOrOpenAsync(connection, null, null, CancellationToken.None);
        await registry.DisposeAsync();

        await mockClient.Received(1).DisconnectAsync();
    }

    private static Connection MakeConnection() => new()
    {
        Id = Guid.NewGuid(), Name = "test", Protocol = "sftp",
        Host = "localhost", Port = 22, AuthMethod = "password", Username = "user",
        HostKeyPolicy = "always_trust",
    };
}
```

NOTE: `TransferClientFactory.Create` is not virtual by default. Either make it `virtual` or introduce an `ITransferClientFactory` interface for testability. Prefer adding the interface since we're using DI:

Create `ITransferClientFactory` alongside `TransferClientFactory`:
```csharp
public interface ITransferClientFactory
{
    ITransferClient Create(Connection connection, byte[]? decryptedPassword, byte[]? sshPrivateKey);
}
```

Make `TransferClientFactory` implement it. Update `JobConnectionRegistry` to accept `ITransferClientFactory`.

**Step 2: Implement JobConnectionRegistry**

Create `src/Courier.Features/Engine/Protocols/JobConnectionRegistry.cs`:

```csharp
using System.Collections.Concurrent;
using Courier.Domain.Entities;
using Courier.Domain.Protocols;

namespace Courier.Features.Engine.Protocols;

public class JobConnectionRegistry : IAsyncDisposable
{
    private readonly ITransferClientFactory _factory;
    private readonly ConcurrentDictionary<Guid, ITransferClient> _sessions = new();

    public JobConnectionRegistry(ITransferClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<ITransferClient> GetOrOpenAsync(
        Connection connection,
        byte[]? decryptedPassword,
        byte[]? sshPrivateKey,
        CancellationToken ct)
    {
        if (_sessions.TryGetValue(connection.Id, out var existing))
        {
            if (existing.IsConnected)
                return existing;
            // Reconnect if disconnected
            await existing.ConnectAsync(ct);
            return existing;
        }

        var client = _factory.Create(connection, decryptedPassword, sshPrivateKey);
        await client.ConnectAsync(ct);
        _sessions[connection.Id] = client;
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _sessions.Values)
        {
            try { await client.DisconnectAsync(); }
            catch { /* best-effort cleanup */ }
            await client.DisposeAsync();
        }
        _sessions.Clear();
    }
}
```

**Step 3: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "JobConnectionRegistryTests" -v q`
Expected: PASS (3 passed)

---

## Phase 3: Transfer Step Handlers

### Task 11: Build TransferStepBase and SftpUploadStep / SftpDownloadStep

**Files:**
- Create: `src/Courier.Features/Engine/Steps/Transfer/TransferStepBase.cs`
- Create: `src/Courier.Features/Engine/Steps/Transfer/SftpUploadStep.cs`
- Create: `src/Courier.Features/Engine/Steps/Transfer/SftpDownloadStep.cs`
- Test: `tests/Courier.Tests.Unit/Engine/Steps/Transfer/SftpUploadStepTests.cs`
- Test: `tests/Courier.Tests.Unit/Engine/Steps/Transfer/SftpDownloadStepTests.cs`

**Step 1: Write tests for SftpUploadStep**

```csharp
using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using Courier.Features.Engine.Steps.Transfer;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Steps.Transfer;

public class SftpUploadStepTests
{
    [Fact]
    public void TypeKey_IsSftpUpload()
    {
        var step = CreateStep();
        step.TypeKey.ShouldBe("sftp.upload");
    }

    [Fact]
    public async Task ValidateAsync_MissingConnectionId_Fails()
    {
        var step = CreateStep();
        var config = new StepConfiguration("""{"local_path": "/tmp/file.csv", "remote_path": "/out/file.csv"}""");
        var result = await step.ValidateAsync(config);
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("connection_id");
    }

    [Fact]
    public async Task ExecuteAsync_UploadsFile()
    {
        var connectionId = Guid.NewGuid();
        var (step, mockClient, db) = CreateStepWithMocks(connectionId, "sftp");
        await SeedConnection(db, connectionId, "sftp");

        // Create a temp file for the upload
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "test content");

        try
        {
            var config = new StepConfiguration($$"""{"connection_id": "{{connectionId}}", "local_path": "{{tempFile.Replace("\\", "/")}}", "remote_path": "/out/file.csv"}""");
            var context = new JobContext();
            var result = await step.ExecuteAsync(config, context, CancellationToken.None);

            result.Success.ShouldBeTrue();
            result.Outputs.ShouldContainKey("uploaded_file");
            await mockClient.Received(1).UploadAsync(Arg.Any<UploadRequest>(), Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WrongProtocol_Fails()
    {
        var connectionId = Guid.NewGuid();
        var (step, _, db) = CreateStepWithMocks(connectionId, "ftp");
        await SeedConnection(db, connectionId, "ftp"); // connection is FTP, step expects SFTP

        var config = new StepConfiguration($$"""{"connection_id": "{{connectionId}}", "local_path": "/tmp/f.csv", "remote_path": "/out/f.csv"}""");
        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("protocol");
    }

    // Helpers
    private static SftpUploadStep CreateStep()
    {
        var db = CreateInMemoryContext();
        var encryptor = Substitute.For<ICredentialEncryptor>();
        var factory = Substitute.For<ITransferClientFactory>();
        var registry = new JobConnectionRegistry(factory);
        return new SftpUploadStep(db, encryptor, registry);
    }

    private static (SftpUploadStep step, ITransferClient mockClient, CourierDbContext db) CreateStepWithMocks(Guid connectionId, string protocol)
    {
        var db = CreateInMemoryContext();
        var encryptor = Substitute.For<ICredentialEncryptor>();
        var mockClient = Substitute.For<ITransferClient>();
        mockClient.IsConnected.Returns(true);
        var factory = Substitute.For<ITransferClientFactory>();
        factory.Create(Arg.Any<Connection>(), Arg.Any<byte[]?>(), Arg.Any<byte[]?>()).Returns(mockClient);
        var registry = new JobConnectionRegistry(factory);
        return (new SftpUploadStep(db, encryptor, registry), mockClient, db);
    }

    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    private static async Task SeedConnection(CourierDbContext db, Guid id, string protocol)
    {
        db.Connections.Add(new Connection
        {
            Id = id, Name = "Test", Protocol = protocol, Host = "localhost",
            Port = protocol == "sftp" ? 22 : 21, AuthMethod = "password", Username = "user",
            HostKeyPolicy = "always_trust", TlsCertPolicy = "system_trust",
        });
        await db.SaveChangesAsync();
    }
}
```

**Step 2: Implement TransferStepBase**

Create `src/Courier.Features/Engine/Steps/Transfer/TransferStepBase.cs`:

```csharp
using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Engine.Steps.Transfer;

public abstract class TransferStepBase : IJobStep
{
    protected readonly CourierDbContext Db;
    protected readonly ICredentialEncryptor Encryptor;
    protected readonly JobConnectionRegistry ConnectionRegistry;

    protected TransferStepBase(CourierDbContext db, ICredentialEncryptor encryptor, JobConnectionRegistry registry)
    {
        Db = db;
        Encryptor = encryptor;
        ConnectionRegistry = registry;
    }

    public abstract string TypeKey { get; }
    protected abstract string ExpectedProtocol { get; }

    public abstract Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct);
    public abstract Task<StepResult> ValidateAsync(StepConfiguration config);

    protected async Task<(ITransferClient? client, StepResult? error)> ResolveClientAsync(StepConfiguration config, CancellationToken ct)
    {
        var connectionId = Guid.Parse(config.GetString("connection_id"));
        var connection = await Db.Connections.FirstOrDefaultAsync(c => c.Id == connectionId, ct);

        if (connection is null)
            return (null, StepResult.Fail($"Connection {connectionId} not found"));

        if (!connection.Protocol.Equals(ExpectedProtocol, StringComparison.OrdinalIgnoreCase))
            return (null, StepResult.Fail($"Connection {connectionId} uses protocol '{connection.Protocol}', expected '{ExpectedProtocol}'"));

        byte[]? password = connection.PasswordEncrypted is not null
            ? System.Text.Encoding.UTF8.GetBytes(Encryptor.Decrypt(connection.PasswordEncrypted))
            : null;

        byte[]? sshKey = null;
        if (connection.SshKeyId.HasValue)
        {
            var key = await Db.SshKeys.FirstOrDefaultAsync(k => k.Id == connection.SshKeyId, ct);
            if (key?.PrivateKeyData is not null)
            {
                var pem = Encryptor.Decrypt(key.PrivateKeyData);
                sshKey = System.Text.Encoding.UTF8.GetBytes(pem);
            }
        }

        var client = await ConnectionRegistry.GetOrOpenAsync(connection, password, sshKey, ct);
        return (client, null);
    }

    protected static string ResolveContextRef(string value, JobContext context)
    {
        if (value.StartsWith("context:"))
        {
            var key = value["context:".Length..];
            return context.TryGet<string>(key, out var resolved) && resolved is not null
                ? resolved
                : throw new InvalidOperationException($"Context reference '{key}' not found");
        }
        return value;
    }

    protected StepResult ValidateRequired(StepConfiguration config, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!config.Has(key))
                return StepResult.Fail($"Missing required config: {key}");
        }
        return StepResult.Ok();
    }
}
```

**Step 3: Implement SftpUploadStep**

Create `src/Courier.Features/Engine/Steps/Transfer/SftpUploadStep.cs`:

```csharp
using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using Courier.Infrastructure.Data;

namespace Courier.Features.Engine.Steps.Transfer;

public class SftpUploadStep : TransferStepBase
{
    public SftpUploadStep(CourierDbContext db, ICredentialEncryptor encryptor, JobConnectionRegistry registry)
        : base(db, encryptor, registry) { }

    public override string TypeKey => "sftp.upload";
    protected override string ExpectedProtocol => "sftp";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var (client, error) = await ResolveClientAsync(config, ct);
        if (error is not null) return error;

        var localPath = ResolveContextRef(config.GetString("local_path"), context);
        var remotePath = config.GetString("remote_path");
        var request = new UploadRequest(
            localPath, remotePath,
            AtomicUpload: config.GetBoolOrDefault("atomic_upload", true),
            AtomicSuffix: config.GetStringOrDefault("atomic_suffix", ".tmp")!,
            ResumePartial: config.GetBoolOrDefault("resume_partial"));

        await client!.UploadAsync(request, progress: null, ct);
        var bytes = File.Exists(localPath) ? new FileInfo(localPath).Length : 0;
        return StepResult.Ok(bytes, new() { ["uploaded_file"] = remotePath });
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
        => Task.FromResult(ValidateRequired(config, "connection_id", "local_path", "remote_path"));
}
```

**Step 4: Implement SftpDownloadStep** (same pattern)

Create `src/Courier.Features/Engine/Steps/Transfer/SftpDownloadStep.cs`:

```csharp
using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using Courier.Infrastructure.Data;

namespace Courier.Features.Engine.Steps.Transfer;

public class SftpDownloadStep : TransferStepBase
{
    public SftpDownloadStep(CourierDbContext db, ICredentialEncryptor encryptor, JobConnectionRegistry registry)
        : base(db, encryptor, registry) { }

    public override string TypeKey => "sftp.download";
    protected override string ExpectedProtocol => "sftp";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var (client, error) = await ResolveClientAsync(config, ct);
        if (error is not null) return error;

        var remotePath = config.GetString("remote_path");
        var localPath = ResolveContextRef(config.GetStringOrDefault("local_path", Path.GetTempPath())!, context);
        var request = new DownloadRequest(
            remotePath, localPath,
            ResumePartial: config.GetBoolOrDefault("resume_partial"),
            FilePattern: config.GetStringOrDefault("file_pattern", "*")!,
            DeleteAfterDownload: config.GetBoolOrDefault("delete_after_download"));

        await client!.DownloadAsync(request, progress: null, ct);
        var bytes = File.Exists(localPath) ? new FileInfo(localPath).Length : 0;
        return StepResult.Ok(bytes, new() { ["downloaded_file"] = localPath });
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
        => Task.FromResult(ValidateRequired(config, "connection_id", "remote_path"));
}
```

**Step 5: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "SftpUploadStepTests" -v q`
Expected: PASS

---

### Task 12: Build remaining 13 transfer step handlers

All follow the exact same pattern as Task 11. Each is ~30 lines, inheriting from `TransferStepBase`.

**Files to create (one per step handler):**

```
src/Courier.Features/Engine/Steps/Transfer/SftpMkdirStep.cs      → TypeKey="sftp.mkdir",  Protocol="sftp"
src/Courier.Features/Engine/Steps/Transfer/SftpRmdirStep.cs      → TypeKey="sftp.rmdir",  Protocol="sftp"
src/Courier.Features/Engine/Steps/Transfer/SftpListStep.cs       → TypeKey="sftp.list",   Protocol="sftp"
src/Courier.Features/Engine/Steps/Transfer/FtpUploadStep.cs      → TypeKey="ftp.upload",  Protocol="ftp"
src/Courier.Features/Engine/Steps/Transfer/FtpDownloadStep.cs    → TypeKey="ftp.download", Protocol="ftp"
src/Courier.Features/Engine/Steps/Transfer/FtpMkdirStep.cs       → TypeKey="ftp.mkdir",   Protocol="ftp"
src/Courier.Features/Engine/Steps/Transfer/FtpRmdirStep.cs       → TypeKey="ftp.rmdir",   Protocol="ftp"
src/Courier.Features/Engine/Steps/Transfer/FtpListStep.cs        → TypeKey="ftp.list",    Protocol="ftp"
src/Courier.Features/Engine/Steps/Transfer/FtpsUploadStep.cs     → TypeKey="ftps.upload", Protocol="ftps"
src/Courier.Features/Engine/Steps/Transfer/FtpsDownloadStep.cs   → TypeKey="ftps.download",Protocol="ftps"
src/Courier.Features/Engine/Steps/Transfer/FtpsMkdirStep.cs      → TypeKey="ftps.mkdir",  Protocol="ftps"
src/Courier.Features/Engine/Steps/Transfer/FtpsRmdirStep.cs      → TypeKey="ftps.rmdir",  Protocol="ftps"
src/Courier.Features/Engine/Steps/Transfer/FtpsListStep.cs       → TypeKey="ftps.list",   Protocol="ftps"
```

Each mkdir step calls `client.CreateDirectoryAsync()`, each rmdir calls `client.DeleteDirectoryAsync()`, each list calls `client.ListDirectoryAsync()` and returns the result as `file_list` output.

**Test file:** `tests/Courier.Tests.Unit/Engine/Steps/Transfer/TransferStepTypeKeyTests.cs` — verify all 15 step handlers return the correct `TypeKey`:

```csharp
[Theory]
[InlineData(typeof(SftpUploadStep), "sftp.upload")]
[InlineData(typeof(SftpDownloadStep), "sftp.download")]
[InlineData(typeof(SftpMkdirStep), "sftp.mkdir")]
// ... etc for all 15
public void TypeKey_IsCorrect(Type stepType, string expectedKey)
{
    var step = CreateStep(stepType);
    step.TypeKey.ShouldBe(expectedKey);
}
```

---

## Phase 4: PGP Crypto Provider

### Task 13: Build PgpCryptoProvider — Encrypt & Decrypt

**Files:**
- Create: `src/Courier.Features/Engine/Crypto/PgpCryptoProvider.cs`
- Test: `tests/Courier.Tests.Unit/Engine/Crypto/PgpCryptoProviderTests.cs`

This is the most substantial implementation. The existing `PgpKeyService` already uses BouncyCastle for key generation, so the patterns are established.

**Step 1: Write encrypt/decrypt round-trip tests**

```csharp
using Courier.Domain.Encryption;
using Courier.Domain.Entities;
using Courier.Features.Engine.Crypto;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Crypto;

public class PgpCryptoProviderTests
{
    [Fact]
    public async Task EncryptDecrypt_RoundTrip_ProducesOriginalContent()
    {
        var (provider, db, keyId) = await SetupWithGeneratedKey();
        var inputFile = CreateTempFile("Hello, PGP encryption!");
        var encryptedFile = Path.GetTempFileName();
        var decryptedFile = Path.GetTempFileName();

        try
        {
            var encResult = await provider.EncryptAsync(
                new EncryptRequest(inputFile, encryptedFile, [keyId], null, OutputFormat.Binary),
                null, CancellationToken.None);
            encResult.Success.ShouldBeTrue();
            File.Exists(encryptedFile).ShouldBeTrue();
            File.ReadAllBytes(encryptedFile).ShouldNotBe(File.ReadAllBytes(inputFile));

            var decResult = await provider.DecryptAsync(
                new DecryptRequest(encryptedFile, decryptedFile, keyId, false),
                null, CancellationToken.None);
            decResult.Success.ShouldBeTrue();
            File.ReadAllText(decryptedFile).ShouldBe("Hello, PGP encryption!");
        }
        finally { CleanupFiles(inputFile, encryptedFile, decryptedFile); }
    }

    [Fact]
    public async Task Encrypt_ArmoredFormat_ProducesAsciiOutput()
    {
        var (provider, db, keyId) = await SetupWithGeneratedKey();
        var inputFile = CreateTempFile("Armored test");
        var outputFile = Path.GetTempFileName();

        try
        {
            var result = await provider.EncryptAsync(
                new EncryptRequest(inputFile, outputFile, [keyId], null, OutputFormat.Armored),
                null, CancellationToken.None);
            result.Success.ShouldBeTrue();
            File.ReadAllText(outputFile).ShouldContain("BEGIN PGP MESSAGE");
        }
        finally { CleanupFiles(inputFile, outputFile); }
    }

    [Fact]
    public async Task Encrypt_RetiredKey_Fails()
    {
        var (provider, db, keyId) = await SetupWithGeneratedKey();
        var key = await db.PgpKeys.FindAsync(keyId);
        key!.Status = "retired";
        await db.SaveChangesAsync();

        var inputFile = CreateTempFile("test");
        try
        {
            var result = await provider.EncryptAsync(
                new EncryptRequest(inputFile, Path.GetTempFileName(), [keyId], null, OutputFormat.Binary),
                null, CancellationToken.None);
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldContain("retired");
        }
        finally { File.Delete(inputFile); }
    }

    [Fact]
    public async Task Decrypt_WrongKey_Fails()
    {
        var (provider, db, keyId1) = await SetupWithGeneratedKey();
        var keyId2 = await SeedSecondKey(db);

        var inputFile = CreateTempFile("encrypted for key1");
        var encryptedFile = Path.GetTempFileName();

        try
        {
            await provider.EncryptAsync(
                new EncryptRequest(inputFile, encryptedFile, [keyId1], null, OutputFormat.Binary),
                null, CancellationToken.None);

            var result = await provider.DecryptAsync(
                new DecryptRequest(encryptedFile, Path.GetTempFileName(), keyId2, false),
                null, CancellationToken.None);
            result.Success.ShouldBeFalse();
        }
        finally { CleanupFiles(inputFile, encryptedFile); }
    }

    // Helper: use PgpKeyService to generate a real key, return its ID
    // Helper: CreateTempFile, CleanupFiles
}
```

**Step 2: Implement PgpCryptoProvider**

Create `src/Courier.Features/Engine/Crypto/PgpCryptoProvider.cs` (~400-500 lines). Key structure:

```csharp
using Courier.Domain.Encryption;
using Courier.Domain.Entities;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Security;

namespace Courier.Features.Engine.Crypto;

public class PgpCryptoProvider : ICryptoProvider
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private const int BufferSize = 81920; // 80KB

    public PgpCryptoProvider(CourierDbContext db, ICredentialEncryptor encryptor)
    {
        _db = db;
        _encryptor = encryptor;
    }

    public async Task<CryptoResult> EncryptAsync(EncryptRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        // 1. Load + validate all recipient keys (must be Active or Expiring)
        // 2. Parse public key rings from PublicKeyData
        // 3. Create PgpEncryptedDataGenerator with all recipient public keys
        // 4. Optionally wrap with signing if SigningKeyId provided
        // 5. Stream: input → [sign] → encrypt → [armor] → output
        // 6. Report progress every 10MB
    }

    public async Task<CryptoResult> DecryptAsync(DecryptRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        // 1. Load private key entity
        // 2. Decrypt private key material via _encryptor
        // 3. Unlock with passphrase if present
        // 4. Parse encrypted data from input file
        // 5. Find matching session key for our key ID
        // 6. Stream decrypt to output
        // 7. If VerifySignature and signed, verify
    }

    public async Task<CryptoResult> SignAsync(SignRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        // Support Detached, Inline, Clearsign modes
    }

    public async Task<VerifyResult> VerifyAsync(VerifyRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        // Check signature against public key, return VerifyStatus
    }

    // Private helpers:
    // - LoadAndValidateKeys(ids, allowedStatuses)
    // - DecryptPrivateKey(PgpKey entity) → PgpSecretKey
    // - GetPublicKeyRing(string armoredPublicKey) → PgpPublicKeyRing
}
```

The BouncyCastle streaming encrypt pattern (reference from design doc Section 7.7):
```
FileStream(source, 80KB buffer)
  → [Optional] PgpSignatureGenerator
    → PgpEncryptedDataGenerator (streaming, all recipients)
      → PgpCompressedDataGenerator
        → PgpLiteralDataGenerator
          → [Optional] ArmoredOutputStream
            → FileStream(output, 80KB buffer)
```

**Step 3: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "PgpCryptoProviderTests" -v q`
Expected: PASS

---

### Task 14: Build PgpCryptoProvider — Sign & Verify

**Files:**
- Modify: `src/Courier.Features/Engine/Crypto/PgpCryptoProvider.cs` (add Sign/Verify methods)
- Test: `tests/Courier.Tests.Unit/Engine/Crypto/PgpCryptoProviderSignVerifyTests.cs`

**Step 1: Write sign/verify tests**

```csharp
namespace Courier.Tests.Unit.Engine.Crypto;

public class PgpCryptoProviderSignVerifyTests
{
    [Fact]
    public async Task SignVerify_Detached_RoundTrip()
    {
        // Generate key, sign file (detached), verify signature
        // Verify produces .sig file alongside original
        // VerifyResult.IsValid == true, Status == Valid
    }

    [Fact]
    public async Task SignVerify_Inline_RoundTrip()
    {
        // Sign with Inline mode, verify
    }

    [Fact]
    public async Task SignVerify_Clearsign_RoundTrip()
    {
        // Sign with Clearsign mode (produces human-readable output)
    }

    [Fact]
    public async Task Verify_TamperedFile_ReturnsInvalid()
    {
        // Sign file, modify file content, verify → Invalid
    }

    [Fact]
    public async Task Verify_UnknownSigner_ReturnsUnknownSigner()
    {
        // Sign with key A, verify with expected key B → UnknownSigner
    }

    [Fact]
    public async Task Verify_RevokedKey_ReturnsRevokedKey()
    {
        // Sign, revoke key, verify → RevokedKey
    }

    [Fact]
    public async Task Sign_RetiredKey_Fails()
    {
        // Retired key cannot sign (only decrypt/verify legacy)
    }

    [Fact]
    public async Task EncryptDecrypt_SignThenEncrypt_VerifiesOnDecrypt()
    {
        // Encrypt with signing_key_id, decrypt with verify_signature=true
        // DecryptResult includes verify info in outputs
    }
}
```

**Step 2: Implement Sign and Verify in PgpCryptoProvider**

Add `SignAsync` and `VerifyAsync` implementations using BouncyCastle:

- **Detached sign**: `PgpSignatureGenerator` → write signature to separate `.sig` file
- **Inline sign**: Wrap literal data in `PgpOnePassSignature` + `PgpSignature`
- **Clearsign**: `ArmoredOutputStream` with cleartext framework
- **Verify**: Parse signature, resolve public key, `PgpSignature.Verify()` → map to `VerifyStatus`

**Step 3: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "PgpCryptoProviderSignVerifyTests" -v q`
Expected: PASS

---

## Phase 5: Crypto Step Handlers

### Task 15: Build CryptoStepBase and all 4 crypto step handlers

**Files:**
- Create: `src/Courier.Features/Engine/Steps/Crypto/CryptoStepBase.cs`
- Create: `src/Courier.Features/Engine/Steps/Crypto/PgpEncryptStep.cs`
- Create: `src/Courier.Features/Engine/Steps/Crypto/PgpDecryptStep.cs`
- Create: `src/Courier.Features/Engine/Steps/Crypto/PgpSignStep.cs`
- Create: `src/Courier.Features/Engine/Steps/Crypto/PgpVerifyStep.cs`
- Test: `tests/Courier.Tests.Unit/Engine/Steps/Crypto/CryptoStepTests.cs`

**Step 1: Write tests**

```csharp
namespace Courier.Tests.Unit.Engine.Steps.Crypto;

public class CryptoStepTests
{
    [Theory]
    [InlineData(typeof(PgpEncryptStep), "pgp.encrypt")]
    [InlineData(typeof(PgpDecryptStep), "pgp.decrypt")]
    [InlineData(typeof(PgpSignStep), "pgp.sign")]
    [InlineData(typeof(PgpVerifyStep), "pgp.verify")]
    public void TypeKey_IsCorrect(Type stepType, string expected) { /* ... */ }

    [Fact]
    public async Task PgpEncryptStep_ValidateAsync_MissingRecipients_Fails() { /* ... */ }

    [Fact]
    public async Task PgpDecryptStep_ValidateAsync_MissingKeyId_Fails() { /* ... */ }

    [Fact]
    public async Task PgpEncryptStep_ExecuteAsync_DelegatesToProvider()
    {
        // Mock ICryptoProvider, verify EncryptAsync called with correct request
    }

    [Fact]
    public async Task PgpDecryptStep_ExecuteAsync_OutputsDecryptedFile()
    {
        // Mock ICryptoProvider, verify outputs contain "decrypted_file"
    }

    [Fact]
    public async Task PgpVerifyStep_ExecuteAsync_OutputsVerifyStatus()
    {
        // Mock ICryptoProvider, verify outputs contain verify_status, signer_fingerprint
    }
}
```

**Step 2: Implement CryptoStepBase**

```csharp
using Courier.Domain.Engine;
using Courier.Features.Engine.Crypto;

namespace Courier.Features.Engine.Steps.Crypto;

public abstract class CryptoStepBase : IJobStep
{
    protected readonly ICryptoProvider CryptoProvider;

    protected CryptoStepBase(ICryptoProvider cryptoProvider)
    {
        CryptoProvider = cryptoProvider;
    }

    public abstract string TypeKey { get; }
    public abstract Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct);
    public abstract Task<StepResult> ValidateAsync(StepConfiguration config);

    protected static string ResolveContextRef(string value, JobContext context)
    {
        if (value.StartsWith("context:"))
        {
            var key = value["context:".Length..];
            return context.TryGet<string>(key, out var resolved) && resolved is not null
                ? resolved
                : throw new InvalidOperationException($"Context reference '{key}' not found");
        }
        return value;
    }
}
```

**Step 3: Implement PgpEncryptStep**

```csharp
namespace Courier.Features.Engine.Steps.Crypto;

public class PgpEncryptStep : CryptoStepBase
{
    public PgpEncryptStep(ICryptoProvider cryptoProvider) : base(cryptoProvider) { }

    public override string TypeKey => "pgp.encrypt";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var inputPath = ResolveContextRef(config.GetString("input_path"), context);
        var outputPath = config.GetStringOrDefault("output_path", inputPath + ".pgp")!;
        var recipientIds = config.GetStringArray("recipient_key_ids").Select(Guid.Parse).ToList();
        var signingKeyId = config.Has("signing_key_id") ? Guid.Parse(config.GetString("signing_key_id")) : (Guid?)null;
        var format = config.GetStringOrDefault("output_format", "binary") == "armored" ? OutputFormat.Armored : OutputFormat.Binary;

        var result = await CryptoProvider.EncryptAsync(
            new EncryptRequest(inputPath, outputPath, recipientIds, signingKeyId, format), null, ct);

        return result.Success
            ? StepResult.Ok(result.BytesProcessed, new() { ["encrypted_file"] = result.OutputPath })
            : StepResult.Fail(result.ErrorMessage!);
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("input_path")) return Task.FromResult(StepResult.Fail("Missing required config: input_path"));
        if (config.GetStringArray("recipient_key_ids").Length == 0)
            return Task.FromResult(StepResult.Fail("Missing required config: recipient_key_ids"));
        return Task.FromResult(StepResult.Ok());
    }
}
```

**Step 4: Implement PgpDecryptStep, PgpSignStep, PgpVerifyStep** (same pattern)

- `PgpDecryptStep`: reads `input_path`, `output_path`, `private_key_id`, `verify_signature`. Outputs: `decrypted_file`, optionally `verify_status`.
- `PgpSignStep`: reads `input_path`, `output_path`, `signing_key_id`, `mode` (detached/inline/clearsign). Outputs: `signature_file`.
- `PgpVerifyStep`: reads `input_path`, `detached_signature_path`, `expected_signer_key_id`. Outputs: `verify_status`, `signer_fingerprint`, `signature_timestamp`.

**Step 5: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "CryptoStepTests" -v q`
Expected: PASS

---

## Phase 6: Wire Everything Together

### Task 16: Register all new services and step handlers in DI

**Files:**
- Modify: `src/Courier.Features/FeaturesServiceExtensions.cs`

**Step 1: Add all registrations**

```csharp
// Protocol services
services.AddScoped<ITransferClientFactory, TransferClientFactory>();
services.AddScoped<JobConnectionRegistry>();

// Crypto services
services.AddScoped<ICryptoProvider, PgpCryptoProvider>();

// Transfer step handlers (15)
services.AddScoped<IJobStep, SftpUploadStep>();
services.AddScoped<IJobStep, SftpDownloadStep>();
services.AddScoped<IJobStep, SftpMkdirStep>();
services.AddScoped<IJobStep, SftpRmdirStep>();
services.AddScoped<IJobStep, SftpListStep>();
services.AddScoped<IJobStep, FtpUploadStep>();
services.AddScoped<IJobStep, FtpDownloadStep>();
services.AddScoped<IJobStep, FtpMkdirStep>();
services.AddScoped<IJobStep, FtpRmdirStep>();
services.AddScoped<IJobStep, FtpListStep>();
services.AddScoped<IJobStep, FtpsUploadStep>();
services.AddScoped<IJobStep, FtpsDownloadStep>();
services.AddScoped<IJobStep, FtpsMkdirStep>();
services.AddScoped<IJobStep, FtpsRmdirStep>();
services.AddScoped<IJobStep, FtpsListStep>();

// Crypto step handlers (4)
services.AddScoped<IJobStep, PgpEncryptStep>();
services.AddScoped<IJobStep, PgpDecryptStep>();
services.AddScoped<IJobStep, PgpSignStep>();
services.AddScoped<IJobStep, PgpVerifyStep>();
```

**Step 2: Verify build**

Run: `dotnet build Courier.slnx`
Expected: Build succeeded

---

### Task 17: Integrate JobConnectionRegistry into JobEngine

**Files:**
- Modify: `src/Courier.Features/Engine/JobEngine.cs`

**Step 1: Add JobConnectionRegistry to constructor and dispose after execution**

Add `JobConnectionRegistry` as a constructor parameter. Wrap the step loop in `try/finally` that disposes the registry:

```csharp
public class JobEngine
{
    private readonly CourierDbContext _db;
    private readonly StepTypeRegistry _registry;
    private readonly JobConnectionRegistry _connectionRegistry;
    private readonly ILogger<JobEngine> _logger;

    public JobEngine(CourierDbContext db, StepTypeRegistry registry, JobConnectionRegistry connectionRegistry, ILogger<JobEngine> logger)
    {
        _db = db;
        _registry = registry;
        _connectionRegistry = connectionRegistry;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid executionId, CancellationToken cancellationToken)
    {
        // ... existing preamble ...

        try
        {
            // ... existing step loop (unchanged) ...
        }
        finally
        {
            await _connectionRegistry.DisposeAsync();
        }
    }
}
```

**Step 2: Run all existing tests**

Run: `dotnet test --filter "Category!=Integration" -v q`
Expected: All pass (unit tests construct JobEngine directly; update test setup to include the new parameter with a mock or real instance)

---

### Task 18: Architecture tests for new step types

**Files:**
- Modify or create: `tests/Courier.Tests.Architecture/StepRegistrationTests.cs`

**Step 1: Write architecture test verifying all step types resolve**

```csharp
using Courier.Domain.Engine;
using Courier.Features.Engine;
using Shouldly;

namespace Courier.Tests.Architecture;

public class StepRegistrationTests
{
    private static readonly string[] ExpectedStepTypes =
    [
        "file.copy", "file.move",
        "sftp.upload", "sftp.download", "sftp.mkdir", "sftp.rmdir", "sftp.list",
        "ftp.upload", "ftp.download", "ftp.mkdir", "ftp.rmdir", "ftp.list",
        "ftps.upload", "ftps.download", "ftps.mkdir", "ftps.rmdir", "ftps.list",
        "pgp.encrypt", "pgp.decrypt", "pgp.sign", "pgp.verify",
    ];

    [Fact]
    public void AllStepHandlers_HaveUniqueTypeKeys()
    {
        var stepTypes = typeof(StepTypeRegistry).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IJobStep).IsAssignableFrom(t))
            .Select(t => ((IJobStep)Activator.CreateInstance(t, GetConstructorDefaults(t))!).TypeKey)
            .ToList();

        stepTypes.Count.ShouldBe(stepTypes.Distinct().Count(), "Duplicate TypeKeys found");
    }

    [Theory]
    [MemberData(nameof(GetExpectedStepTypes))]
    public void StepHandler_ExistsForTypeKey(string typeKey)
    {
        var stepTypes = typeof(StepTypeRegistry).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IJobStep).IsAssignableFrom(t));

        stepTypes.ShouldContain(t => HasTypeKey(t, typeKey),
            $"No IJobStep implementation found with TypeKey '{typeKey}'");
    }

    public static IEnumerable<object[]> GetExpectedStepTypes()
        => ExpectedStepTypes.Select(k => new object[] { k });
}
```

**Step 2: Run architecture tests**

Run: `dotnet test tests/Courier.Tests.Architecture -v q`
Expected: PASS

---

## Phase 7: Integration Tests

### Task 19: PGP crypto integration tests (real BouncyCastle, no mocks)

**Files:**
- Create: `tests/Courier.Tests.Integration/Crypto/PgpCryptoIntegrationTests.cs`

Tests that run real BouncyCastle crypto end-to-end against the test Postgres database:

```csharp
public class PgpCryptoIntegrationTests : IClassFixture<CourierApiFactory>
{
    [Fact] public async Task FullPipeline_GenerateKey_Encrypt_Decrypt_VerifyContent() { }
    [Fact] public async Task MultiRecipient_BothCanDecrypt() { }
    [Fact] public async Task SignThenEncrypt_DecryptAndVerify() { }
    [Fact] public async Task DetachedSign_Verify_Valid() { }
    [Fact] public async Task DetachedSign_TamperedFile_Invalid() { }
    [Fact] public async Task Encrypt_LargeFile_StreamsWithoutOOM() { }
}
```

---

### Task 20: Transfer protocol integration tests (Testcontainers)

**Files:**
- Create: `tests/Courier.Tests.Integration/Protocols/SftpIntegrationTests.cs`
- Create: `tests/Courier.Tests.Integration/Protocols/FtpIntegrationTests.cs`

These require Docker containers for SSH and FTP servers. Use Testcontainers:

```csharp
// SFTP: atmoz/sftp container
private readonly IContainer _sftpContainer = new ContainerBuilder()
    .WithImage("atmoz/sftp:latest")
    .WithPortBinding(22, true)
    .WithCommand("testuser:testpass:::upload")
    .Build();

// FTP: fauria/vsftpd or stilliard/pure-ftpd
```

Tests:
```csharp
[Fact] public async Task Upload_Download_RoundTrip() { }
[Fact] public async Task AtomicUpload_RenamesOnSuccess() { }
[Fact] public async Task ListDirectory_ReturnsFiles() { }
[Fact] public async Task CreateDirectory_Recursive() { }
[Fact] public async Task SessionReuse_ThreeStepsSameConnection() { }
[Fact] public async Task ConnectionLost_Reconnects() { }
```

---

### Task 21: Full pipeline integration test

**Files:**
- Create: `tests/Courier.Tests.Integration/Pipeline/FullPipelineTests.cs`

End-to-end test that creates a job with multiple steps and triggers it:

```csharp
[Fact]
public async Task EndToEnd_SftpDownload_PgpDecrypt_PgpEncrypt_SftpUpload()
{
    // 1. Seed SFTP container with an encrypted file
    // 2. Create connection, PGP keys via API
    // 3. Create job with steps: sftp.download → pgp.decrypt → pgp.encrypt → sftp.upload
    // 4. Trigger job execution
    // 5. Wait for completion
    // 6. Verify output file on SFTP server
}
```

---

## Phase 8: Final Verification

### Task 22: Full build and test run

**Step 1: Build everything**

Run: `dotnet build Courier.slnx`
Expected: 0 errors, 0 warnings (or only pre-existing warnings)

**Step 2: Run all unit + architecture tests**

Run: `dotnet test --filter "Category!=Integration" -v q`
Expected: All pass

**Step 3: Run integration tests**

Run: `dotnet test tests/Courier.Tests.Integration -v q`
Expected: All pass

**Step 4: Verify step type count**

Quick sanity check — the `StepTypeRegistry` should resolve 21 step types (2 existing + 19 new).

---

## Summary

| Phase | Tasks | New Files | Key Deliverable |
|-------|-------|-----------|-----------------|
| 1. Foundation | 1-6 | 5 | Packages, models, interfaces, DI migration |
| 2. Protocol Infrastructure | 7-10 | 8 | SftpTransferClient, FluentFtpTransferClient, factory, registry |
| 3. Transfer Step Handlers | 11-12 | 17 | TransferStepBase + 15 thin handlers |
| 4. PGP Crypto Provider | 13-14 | 2 | PgpCryptoProvider (encrypt/decrypt/sign/verify) |
| 5. Crypto Step Handlers | 15 | 5 | CryptoStepBase + 4 handlers |
| 6. Wire Together | 16-18 | 1 | DI registration, engine integration, arch tests |
| 7. Integration Tests | 19-21 | 3 | Real crypto + real SFTP/FTP + full pipeline |
| 8. Final Verification | 22 | 0 | Full build + test pass |
| **Total** | **22 tasks** | **~41 files** | **19 step handlers, full spec** |

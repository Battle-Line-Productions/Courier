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

public class TransferStepBaseTests : IDisposable
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private readonly ITransferClientFactory _factory;
    private readonly JobConnectionRegistry _registry;

    public TransferStepBaseTests()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CourierDbContext(options);
        _encryptor = Substitute.For<ICredentialEncryptor>();
        _factory = Substitute.For<ITransferClientFactory>();
        _registry = new JobConnectionRegistry(_factory);
    }

    [Fact]
    public async Task ValidateRequired_MissingKey_ReturnsFailure()
    {
        var step = new SftpUploadStep(_db, _encryptor, _registry);
        var config = new StepConfiguration("""{"connection_id": "00000000-0000-0000-0000-000000000001"}""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Missing required config");
        result.ErrorMessage!.ShouldContain("local_path");
    }

    [Fact]
    public async Task ValidateRequired_AllKeysPresent_ReturnsOk()
    {
        var step = new SftpMkdirStep(_db, _encryptor, _registry);
        var config = new StepConfiguration("""{"connection_id": "00000000-0000-0000-0000-000000000001", "remote_path": "/data"}""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public void ResolveContextRef_WithContextPrefix_ResolvesFromJobContext()
    {
        var context = new JobContext();
        context.Set("1.downloaded_file", "/tmp/data.csv");

        var resolved = TransferStepBase_TestHelper.CallResolveContextRef("context:1.downloaded_file", context);

        resolved.ShouldBe("/tmp/data.csv");
    }

    [Fact]
    public void ResolveContextRef_WithoutContextPrefix_ReturnsLiteralValue()
    {
        var context = new JobContext();

        var resolved = TransferStepBase_TestHelper.CallResolveContextRef("/data/file.txt", context);

        resolved.ShouldBe("/data/file.txt");
    }

    [Fact]
    public void ResolveContextRef_MissingContextKey_ThrowsInvalidOperationException()
    {
        var context = new JobContext();

        Should.Throw<InvalidOperationException>(
            () => TransferStepBase_TestHelper.CallResolveContextRef("context:missing_key", context));
    }

    [Fact]
    public async Task ResolveClientAsync_ConnectionNotFound_ReturnsError()
    {
        var step = new SftpUploadStep(_db, _encryptor, _registry);
        var connectionId = Guid.NewGuid();
        var config = new StepConfiguration($$"""
        {
            "connection_id": "{{connectionId}}",
            "local_path": "/tmp/file.txt",
            "remote_path": "/data/file.txt"
        }
        """);

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("not found");
        result.ErrorMessage!.ShouldContain(connectionId.ToString());
    }

    [Fact]
    public async Task ResolveClientAsync_ProtocolMismatch_ReturnsError()
    {
        var connectionId = Guid.NewGuid();
        _db.Connections.Add(new Connection
        {
            Id = connectionId,
            Name = "FTP Server",
            Protocol = "ftp",
            Host = "ftp.example.com",
            Port = 21,
            AuthMethod = "password",
            Username = "user"
        });
        await _db.SaveChangesAsync();

        var step = new SftpUploadStep(_db, _encryptor, _registry);
        var config = new StepConfiguration($$"""
        {
            "connection_id": "{{connectionId}}",
            "local_path": "/tmp/file.txt",
            "remote_path": "/data/file.txt"
        }
        """);

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("ftp");
        result.ErrorMessage!.ShouldContain("sftp");
    }

    [Fact]
    public async Task ResolveClientAsync_ValidConnection_ReturnsClient()
    {
        var connectionId = Guid.NewGuid();
        var connection = new Connection
        {
            Id = connectionId,
            Name = "SFTP Server",
            Protocol = "sftp",
            Host = "sftp.example.com",
            Port = 22,
            AuthMethod = "password",
            Username = "user",
            PasswordEncrypted = [1, 2, 3]
        };
        _db.Connections.Add(connection);
        await _db.SaveChangesAsync();

        _encryptor.Decrypt(Arg.Any<byte[]>()).Returns("decrypted-password");

        var mockClient = Substitute.For<ITransferClient>();
        mockClient.IsConnected.Returns(true);
        _factory.Create(Arg.Any<Connection>(), Arg.Any<byte[]?>(), Arg.Any<byte[]?>())
            .Returns(mockClient);

        var step = new SftpMkdirStep(_db, _encryptor, _registry);
        var config = new StepConfiguration($$"""
        {
            "connection_id": "{{connectionId}}",
            "remote_path": "/data/new_dir"
        }
        """);

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeTrue();
        await mockClient.Received(1).CreateDirectoryAsync("/data/new_dir", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveClientAsync_ConnectionWithSshKey_DecryptsAndPassesKey()
    {
        var sshKeyId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        _db.SshKeys.Add(new SshKey
        {
            Id = sshKeyId,
            Name = "Test Key",
            KeyType = "ed25519",
            PrivateKeyData = [10, 20, 30],
            Status = "active"
        });
        _db.Connections.Add(new Connection
        {
            Id = connectionId,
            Name = "SFTP Server",
            Protocol = "sftp",
            Host = "sftp.example.com",
            Port = 22,
            AuthMethod = "key",
            Username = "user",
            SshKeyId = sshKeyId
        });
        await _db.SaveChangesAsync();

        _encryptor.Decrypt(Arg.Any<byte[]>()).Returns("-----BEGIN OPENSSH PRIVATE KEY-----\nfake\n-----END OPENSSH PRIVATE KEY-----");

        var mockClient = Substitute.For<ITransferClient>();
        mockClient.IsConnected.Returns(true);
        _factory.Create(Arg.Any<Connection>(), Arg.Any<byte[]?>(), Arg.Is<byte[]?>(b => b != null))
            .Returns(mockClient);

        var step = new SftpMkdirStep(_db, _encryptor, _registry);
        var config = new StepConfiguration($$"""
        {
            "connection_id": "{{connectionId}}",
            "remote_path": "/data/new_dir"
        }
        """);

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeTrue();
        _encryptor.Received(1).Decrypt(Arg.Any<byte[]>());
        _factory.Received(1).Create(Arg.Any<Connection>(), Arg.Any<byte[]?>(), Arg.Is<byte[]?>(b => b != null));
    }

    public void Dispose()
    {
        _db.Dispose();
        _registry.DisposeAsync().AsTask().Wait();
    }
}

/// <summary>
/// Helper to expose protected static method for testing.
/// </summary>
internal class TransferStepBase_TestHelper : TransferStepBase
{
    public TransferStepBase_TestHelper()
        : base(null!, null!, null!) { }

    public override string TypeKey => "test";
    protected override string ExpectedProtocol => "test";

    public override Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
        => throw new NotImplementedException();

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
        => throw new NotImplementedException();

    public static string CallResolveContextRef(string value, JobContext context)
        => ResolveContextRef(value, context);
}

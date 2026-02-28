# Protocol & Crypto Step Handlers — Design

**Date:** 2026-02-28
**Scope:** Full-spec implementation of transfer protocol step handlers (SFTP, FTP, FTPS) and PGP crypto step handlers, integrating the Connections and Keys verticals into the job engine pipeline.

---

## 1. Overview

Build 19 new step handlers that enable end-to-end secure file transfer in job pipelines:

- **15 protocol step handlers**: upload, download, mkdir, rmdir, list for each of SFTP, FTP, FTPS
- **4 crypto step handlers**: pgp.encrypt, pgp.decrypt, pgp.sign, pgp.verify

These handlers connect the existing management planes (Connections, Keys) to the operational plane (Job Engine), delivering Courier's core value prop: "move a file securely."

---

## 2. Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| V1 scope | Full spec | Transfer resume, atomic uploads, progress, host key TOFU, session reuse, retry/cleanup |
| Protocols | All three (SFTP + FTP + FTPS) | FTP/FTPS share FluentFTP; marginal cost is low |
| Step type naming | All protocol-prefixed | `sftp.upload`, `ftp.upload`, `ftps.upload` — explicit per protocol |
| Directory steps | Included | `sftp.mkdir`, `ftp.rmdir`, `ftps.list`, etc. |
| ICryptoProvider location | `Features/Engine/Crypto/` | Separates engine concerns from key management CRUD |
| DI lifetime | Scoped (migrate from singleton) | Handlers need DbContext, ICredentialEncryptor directly |

---

## 3. Architecture

### 3.1 File Layout

```
src/Courier.Domain/
  Protocols/
    ITransferClient.cs             — unified transfer interface
    TransferModels.cs              — UploadRequest, DownloadRequest, TransferProgress, RemoteFileInfo
    ConnectionTestResult.cs

src/Courier.Features/
  Engine/
    StepTypeRegistry.cs            — existing, lifetime changed to scoped
    JobEngine.cs                   — modified: accepts JobConnectionRegistry, disposes on completion
    Steps/
      FileCopyStep.cs              — existing (lifetime → scoped)
      FileMoveStep.cs              — existing (lifetime → scoped)
      Transfer/
        TransferStepBase.cs        — shared connection resolution + credential decryption
        SftpUploadStep.cs          — thin: TypeKey="sftp.upload", ExpectedProtocol="sftp"
        SftpDownloadStep.cs
        SftpMkdirStep.cs
        SftpRmdirStep.cs
        SftpListStep.cs
        FtpUploadStep.cs
        FtpDownloadStep.cs
        FtpMkdirStep.cs
        FtpRmdirStep.cs
        FtpListStep.cs
        FtpsUploadStep.cs
        FtpsDownloadStep.cs
        FtpsMkdirStep.cs
        FtpsRmdirStep.cs
        FtpsListStep.cs
      Crypto/
        CryptoStepBase.cs          — shared key resolution + status validation
        PgpEncryptStep.cs
        PgpDecryptStep.cs
        PgpSignStep.cs
        PgpVerifyStep.cs
    Crypto/
      ICryptoProvider.cs           — encrypt/decrypt/sign/verify interface
      CryptoModels.cs              — request/result records, enums
      PgpCryptoProvider.cs         — BouncyCastle streaming implementation
    Protocols/
      TransferClientFactory.cs     — creates ITransferClient from Connection entity
      SftpTransferClient.cs        — SSH.NET implementation
      FluentFtpTransferClient.cs   — FTP + FTPS via FluentFTP encryption mode
      JobConnectionRegistry.cs     — session reuse per job execution
```

### 3.2 Dependency Flow

```
Step Handlers (thin)
  → TransferStepBase / CryptoStepBase (shared logic)
    → ITransferClient (via TransferClientFactory + JobConnectionRegistry)
    → ICryptoProvider (via PgpCryptoProvider)
      → CourierDbContext (load Connection / PgpKey entities)
      → ICredentialEncryptor (decrypt passwords, private keys)
```

---

## 4. ITransferClient Interface

```csharp
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

### Models

```csharp
public record UploadRequest(
    string LocalPath, string RemotePath,
    bool AtomicUpload = true, string AtomicSuffix = ".tmp",
    bool ResumePartial = false);

public record DownloadRequest(
    string RemotePath, string LocalPath,
    bool ResumePartial = false);

public record TransferProgress(
    long BytesTransferred, long TotalBytes,
    string CurrentFile, double TransferRateBytesPerSec);

public record RemoteFileInfo(
    string Name, string FullPath, long Size,
    DateTime LastModified, bool IsDirectory);

public record ConnectionTestResult(
    bool Success, TimeSpan Latency, string? ServerBanner,
    string? ErrorMessage, IReadOnlyList<string>? SupportedAlgorithms);
```

---

## 5. Protocol Implementations

### 5.1 SftpTransferClient (SSH.NET)

- Constructor: `Connection` entity + decrypted password bytes + SSH private key bytes
- Auth: `PasswordAuthenticationMethod` and/or `PrivateKeyAuthenticationMethod`
- SSH algorithms from `Connection.SshAlgorithms` JSONB → `ConnectionInfo`
- Host key verification via `HostKeyReceived`:
  - `TrustOnFirstUse`: store fingerprint on first connect, reject mismatch
  - `Manual`: compare against `Connection.StoredHostFingerprint`
  - `AlwaysTrust`: accept all (audit logged)
- Keepalive: timer at `Connection.KeepaliveIntervalSec` calling `SendKeepAlive()`
- Atomic upload: upload as `{path}{suffix}`, rename on success, delete partial on failure
- Resume: check remote file size, seek local stream to offset, append
- Progress: reported every 1MB or 5 seconds

### 5.2 FluentFtpTransferClient (FTP + FTPS)

Single class, encryption mode set by factory:
- FTP: `FtpEncryptionMode.None`
- FTPS Explicit: `FtpEncryptionMode.Explicit` (standard port)
- FTPS Implicit: `FtpEncryptionMode.Implicit` (port 990)

TLS cert validation callback:
- `SystemTrust`: `e.Accept = e.PolicyErrors == SslPolicyErrors.None`
- `PinnedThumbprint`: SHA-256 thumbprint exact match
- `Insecure`: accept all (audit logged)

Passive mode from `Connection.PassiveMode`. Atomic upload via FTP RNFR/RNTO.
Resume: `FtpRemoteExists.Resume` for uploads, `FtpLocalExists.Resume` for downloads.

### 5.3 TransferClientFactory

```csharp
public ITransferClient Create(Connection connection, byte[]? decryptedPassword, byte[]? sshPrivateKey)
    => connection.Protocol switch
    {
        "sftp" => new SftpTransferClient(connection, decryptedPassword, sshPrivateKey),
        "ftp"  => new FluentFtpTransferClient(connection, decryptedPassword, FtpEncryptionMode.None),
        "ftps" => new FluentFtpTransferClient(connection, decryptedPassword, DetermineEncryptionMode(connection)),
        _ => throw new ArgumentException($"Unsupported protocol: {connection.Protocol}")
    };
```

### 5.4 JobConnectionRegistry

- Scoped per job execution, created by JobEngine
- `ConcurrentDictionary<Guid, ITransferClient>` keyed by connectionId
- `GetOrOpenAsync`: creates + connects on first access, returns cached on subsequent
- Health check before each operation: `IsConnected` → reconnect if needed (up to `TransportRetries`)
- `DisposeAsync`: disconnects all sessions (guaranteed via try/finally in JobEngine)

---

## 6. ICryptoProvider Interface

```csharp
public interface ICryptoProvider
{
    Task<CryptoResult> EncryptAsync(EncryptRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct);
    Task<CryptoResult> DecryptAsync(DecryptRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct);
    Task<CryptoResult> SignAsync(SignRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct);
    Task<VerifyResult> VerifyAsync(VerifyRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct);
}
```

### Models

```csharp
public record EncryptRequest(
    string InputPath, string OutputPath,
    IReadOnlyList<Guid> RecipientKeyIds,
    Guid? SigningKeyId, OutputFormat Format);

public record DecryptRequest(
    string InputPath, string OutputPath,
    Guid PrivateKeyId, bool VerifySignature);

public record SignRequest(
    string InputPath, string OutputPath,
    Guid SigningKeyId, SignatureMode Mode);

public record VerifyRequest(
    string InputPath, string? DetachedSignaturePath,
    Guid? ExpectedSignerKeyId);

public record CryptoResult(bool Success, long BytesProcessed, string OutputPath, string? ErrorMessage);
public record VerifyResult(bool IsValid, VerifyStatus Status, string? SignerFingerprint, DateTime? SignatureTimestamp);
public record CryptoProgress(long BytesProcessed, long TotalBytes, string Operation);

public enum VerifyStatus { Valid, Invalid, UnknownSigner, ExpiredKey, RevokedKey }
public enum OutputFormat { Armored, Binary }
public enum SignatureMode { Detached, Inline, Clearsign }
```

---

## 7. PgpCryptoProvider Implementation

### 7.1 Encrypt

Streaming pipeline (memory bounded to ~2× 80KB buffer):
```
FileStream(input, 80KB) → [SignatureGenerationStream] → PgpEncryptedDataGenerator → [ArmoredOutputStream] → FileStream(output)
```

- Resolve recipient public keys from DB; validate all `Active` or `Expiring`
- Multi-recipient: add session key for each recipient
- Sign-then-encrypt: sign with private key first, wrap in encrypted envelope
- Private key: decrypted from `PrivateKeyData` via `ICredentialEncryptor`, unlocked with passphrase
- Progress reported every 10MB

### 7.2 Decrypt

```
FileStream(encrypted) → PgpObjectFactory → decompress → extract literal data → FileStream(output)
```

- Resolve private key, decrypt via envelope encryption, unlock passphrase
- If `VerifySignature=true` and message signed: verify against known public keys
- Write `VerifyResult` to step outputs for downstream branching
- `CryptographicOperations.ZeroMemory` on DEK and private key material in `finally`

### 7.3 Sign

Three modes:
- `Detached`: separate `.sig` file alongside original
- `Inline`: signed data wrapping original in single file
- `Clearsign`: human-readable text with ASCII armored signature block

### 7.4 Verify

- Resolve signer's public key by `ExpectedSignerKeyId` or scan key store for matching fingerprint
- Return `VerifyStatus`: Valid, Invalid, UnknownSigner, ExpiredKey, RevokedKey
- Downstream steps can branch on `verify_status` output

### 7.5 Key Status Validation

| Status | Encrypt (recipient) | Decrypt | Sign | Verify |
|--------|-------------------|---------|------|--------|
| Active | Allowed | Allowed | Allowed | Valid |
| Expiring | Allowed (warning) | Allowed | Allowed | Valid (warning) |
| Retired | Rejected | Allowed (legacy) | Rejected | ExpiredKey |
| Revoked | Rejected | Rejected | Rejected | RevokedKey |

---

## 8. Step Handler Configuration Schemas

### Transfer Steps

```json
// sftp.upload / ftp.upload / ftps.upload
{
    "connection_id": "<uuid>",
    "local_path": "context:1.decrypted_file",   // or absolute path
    "remote_path": "/outgoing/invoice.csv",
    "atomic_upload": true,
    "atomic_suffix": ".tmp",
    "resume_partial": false
}

// sftp.download / ftp.download / ftps.download
{
    "connection_id": "<uuid>",
    "remote_path": "/incoming/data.csv",
    "local_path": "/data/courier/temp/",         // or "${job.temp_dir}"
    "file_pattern": "*",
    "delete_after_download": false,
    "resume_partial": false
}

// sftp.mkdir / ftp.mkdir / ftps.mkdir
{
    "connection_id": "<uuid>",
    "remote_path": "/outgoing/2026/02/28",
    "recursive": true
}

// sftp.rmdir / ftp.rmdir / ftps.rmdir
{
    "connection_id": "<uuid>",
    "remote_path": "/outgoing/old/",
    "recursive": false
}

// sftp.list / ftp.list / ftps.list
{
    "connection_id": "<uuid>",
    "remote_path": "/incoming/",
    "file_pattern": "*.csv"
}
```

### Crypto Steps

```json
// pgp.encrypt
{
    "input_path": "context:1.downloaded_file",
    "output_path": "/data/courier/temp/encrypted.pgp",
    "recipient_key_ids": ["<uuid>", "<uuid>"],
    "signing_key_id": "<uuid>",              // optional
    "output_format": "armored"               // or "binary"
}

// pgp.decrypt
{
    "input_path": "context:1.downloaded_file",
    "output_path": "/data/courier/temp/decrypted/",
    "private_key_id": "<uuid>",
    "verify_signature": true
}

// pgp.sign
{
    "input_path": "context:2.decrypted_file",
    "output_path": "/data/courier/temp/signed.sig",
    "signing_key_id": "<uuid>",
    "mode": "detached"                       // or "inline", "clearsign"
}

// pgp.verify
{
    "input_path": "/data/courier/temp/data.csv",
    "detached_signature_path": "/data/courier/temp/data.csv.sig",
    "expected_signer_key_id": "<uuid>"       // optional
}
```

---

## 9. Step Output Keys

| Step Type | Output Keys | Description |
|-----------|-------------|-------------|
| `*.upload` | `uploaded_file` | Remote path of uploaded file |
| `*.download` | `downloaded_file` | Local path of downloaded file |
| `*.list` | `file_list` | JSON array of RemoteFileInfo |
| `*.mkdir` | `created_directory` | Remote path created |
| `*.rmdir` | — | No output |
| `pgp.encrypt` | `encrypted_file` | Output file path |
| `pgp.decrypt` | `decrypted_file`, `verify_status` | Output path + optional verify result |
| `pgp.sign` | `signature_file` | Signature or signed file path |
| `pgp.verify` | `verify_status`, `signer_fingerprint`, `signature_timestamp` | Verification result |

---

## 10. DI & Engine Changes

### Lifetime Migration

All `IJobStep` registrations and `StepTypeRegistry` change from `AddSingleton` to `AddScoped`. `JobQueueProcessor` already creates a scope per execution.

### New Registrations

```csharp
// Protocol services
services.AddScoped<TransferClientFactory>();
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

### JobEngine Changes

- Constructor accepts `JobConnectionRegistry`
- After step loop completes (success or failure): `await _connectionRegistry.DisposeAsync()` in `finally`
- `StepConfiguration` gains `GetBool(key, defaultValue)` and `GetArray(key)` convenience methods

---

## 11. Testing Strategy

### Unit Tests (~40-50)

- `TransferClientFactory`: creates correct client type per protocol
- `JobConnectionRegistry`: session reuse, reconnect, dispose cleanup
- `SftpTransferClient`: mocked SSH.NET — upload/download/atomic rename/resume/host key policies
- `FluentFtpTransferClient`: mocked FluentFTP — FTP/FTPS/TLS modes/cert validation
- `PgpCryptoProvider`: encrypt/decrypt round-trip, multi-recipient, sign-then-encrypt, all three signature modes, key status validation (reject retired/revoked), streaming progress
- Each step handler: config validation, context reference resolution, protocol mismatch error

### Integration Tests (~15-20)

- Full pipeline: `sftp.download` → `pgp.decrypt` → `file.copy` → `pgp.encrypt` → `sftp.upload`
- Real BouncyCastle crypto (no mocks): generate key → encrypt → decrypt → verify round-trip
- SFTP/FTP against Testcontainers (OpenSSH + vsftpd containers)
- Connection session reuse (one connection across 3 sequential SFTP steps)
- Error paths: wrong key, expired key, connection refused, host key mismatch

### Architecture Tests

- All 19 new step types resolve from `StepTypeRegistry`
- All step handlers implement `ValidateAsync` with required config keys

---

## 12. File Count Summary

| Component | New Files | Modified Files |
|-----------|-----------|----------------|
| Domain interfaces + models | 3 | — |
| Protocol implementations | 4 | — |
| Transfer step handlers | 16 | — |
| Crypto provider + models | 3 | — |
| Crypto step handlers | 5 | — |
| DI/Engine changes | — | 3 (FeaturesServiceExtensions, JobEngine, StepConfiguration) |
| Unit tests | ~6-8 | — |
| Integration tests | ~4-5 | — |
| **Total** | **~37-39** | **3** |

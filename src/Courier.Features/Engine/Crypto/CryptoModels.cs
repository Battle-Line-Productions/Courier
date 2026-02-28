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

namespace Courier.Features.Engine.Crypto;

public interface ICryptoProvider
{
    Task<CryptoResult> EncryptAsync(EncryptRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct);
    Task<CryptoResult> DecryptAsync(DecryptRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct);
    Task<CryptoResult> SignAsync(SignRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct);
    Task<VerifyResult> VerifyAsync(VerifyRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct);
}

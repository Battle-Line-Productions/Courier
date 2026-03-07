using Courier.Domain.Encryption;
using Courier.Features.AuditLog;
using Courier.Features.PgpKeys;
using Courier.Infrastructure.Data;
using Courier.Tests.JobEngine.Helpers;

namespace Courier.Tests.JobEngine.Fixtures;

/// <summary>
/// Lazily generates PGP test keys in the shared database.
/// Thread-safe — keys are generated once and cached for the test run.
/// </summary>
public static class PgpTestKeys
{
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static bool _initialized;
    private static Guid _encryptionKeyId;
    private static Guid _signingKeyId;

    public static Guid EncryptionKeyId => _initialized
        ? _encryptionKeyId
        : throw new InvalidOperationException("Call EnsureCreatedAsync first");

    public static Guid SigningKeyId => _initialized
        ? _signingKeyId
        : throw new InvalidOperationException("Call EnsureCreatedAsync first");

    public static async Task EnsureCreatedAsync(DatabaseFixture database)
    {
        if (_initialized) return;

        await Lock.WaitAsync();
        try
        {
            if (_initialized) return;

            var encryptor = TestEncryptionHelper.CreateEncryptor();
            await using var db = database.CreateDbContext();
            var audit = new AuditService(db);
            var keyService = new PgpKeyService(db, encryptor, audit);

            var encResult = await keyService.GenerateAsync(new GeneratePgpKeyRequest
            {
                Name = "test-encrypt-key",
                RealName = "Test Encrypt",
                Email = "encrypt@test.local",
                Algorithm = "rsa_2048",
                Purpose = "encryption",
            });

            if (encResult.Error is not null)
                throw new InvalidOperationException($"Failed to generate encryption key: {encResult.Error.Message}");

            _encryptionKeyId = encResult.Data!.Id;

            var sigResult = await keyService.GenerateAsync(new GeneratePgpKeyRequest
            {
                Name = "test-sign-key",
                RealName = "Test Signer",
                Email = "signer@test.local",
                Algorithm = "rsa_2048",
                Purpose = "signing",
            });

            if (sigResult.Error is not null)
                throw new InvalidOperationException($"Failed to generate signing key: {sigResult.Error.Message}");

            _signingKeyId = sigResult.Data!.Id;
            _initialized = true;
        }
        finally
        {
            Lock.Release();
        }
    }
}

using Courier.Domain.Encryption;
using Courier.Infrastructure.Encryption;
using Microsoft.Extensions.Options;

namespace Courier.Tests.JobEngine.Helpers;

public static class TestEncryptionHelper
{
    // Deterministic 32-byte test KEK (not used in production)
    // "test-key-for-courier-job-engine!" = exactly 32 bytes
    private const string TestKekBase64 = "dGVzdC1rZXktZm9yLWNvdXJpZXItam9iLWVuZ2luZSE=";

    public static ICredentialEncryptor CreateEncryptor()
    {
        var settings = Options.Create(new EncryptionSettings
        {
            KeyEncryptionKey = TestKekBase64
        });
        return new AesGcmCredentialEncryptor(settings);
    }
}

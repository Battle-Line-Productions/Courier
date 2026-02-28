using Courier.Features.Engine.Crypto;
using Courier.Features.PgpKeys;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Courier.Tests.Integration.Crypto;

public class PgpCryptoIntegrationTests : IClassFixture<CourierApiFactory>
{
    private readonly CourierApiFactory _factory;

    public PgpCryptoIntegrationTests(CourierApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullPipeline_GenerateKey_Encrypt_Decrypt_VerifyContent()
    {
        // 1. Resolve services from DI
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<PgpKeyService>();
        var cryptoProvider = scope.ServiceProvider.GetRequiredService<ICryptoProvider>();

        // 2. Generate a PGP key
        var keyResult = await keyService.GenerateAsync(new GeneratePgpKeyRequest
        {
            Name = $"Integration Test Key {Guid.NewGuid():N}",
            Algorithm = "rsa_2048",
            RealName = "Test User",
            Email = "test@example.com",
        });
        keyResult.Data.ShouldNotBeNull();
        var keyId = keyResult.Data!.Id;

        // 3. Create test file
        var inputFile = Path.GetTempFileName();
        var encryptedFile = Path.GetTempFileName();
        var decryptedFile = Path.GetTempFileName();
        var testContent = "Hello from integration test! " + Guid.NewGuid();
        await File.WriteAllTextAsync(inputFile, testContent);

        try
        {
            // 4. Encrypt
            var encResult = await cryptoProvider.EncryptAsync(
                new EncryptRequest(inputFile, encryptedFile, [keyId], null, OutputFormat.Binary),
                null, CancellationToken.None);
            encResult.Success.ShouldBeTrue(encResult.ErrorMessage);
            new FileInfo(encryptedFile).Length.ShouldBeGreaterThan(0);

            // 5. Decrypt
            var decResult = await cryptoProvider.DecryptAsync(
                new DecryptRequest(encryptedFile, decryptedFile, keyId, false),
                null, CancellationToken.None);
            decResult.Success.ShouldBeTrue(decResult.ErrorMessage);

            // 6. Verify content matches
            var decryptedContent = await File.ReadAllTextAsync(decryptedFile);
            decryptedContent.ShouldBe(testContent);
        }
        finally
        {
            File.Delete(inputFile);
            File.Delete(encryptedFile);
            File.Delete(decryptedFile);
        }
    }

    [Fact]
    public async Task SignVerify_Detached_RoundTrip()
    {
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<PgpKeyService>();
        var cryptoProvider = scope.ServiceProvider.GetRequiredService<ICryptoProvider>();

        var keyResult = await keyService.GenerateAsync(new GeneratePgpKeyRequest
        {
            Name = $"Sign Test Key {Guid.NewGuid():N}",
            Algorithm = "rsa_2048",
            RealName = "Signer",
            Email = "signer@example.com",
        });
        var keyId = keyResult.Data!.Id;

        var inputFile = Path.GetTempFileName();
        var sigFile = inputFile + ".sig";
        await File.WriteAllTextAsync(inputFile, "Sign me please");

        try
        {
            var signResult = await cryptoProvider.SignAsync(
                new SignRequest(inputFile, sigFile, keyId, SignatureMode.Detached),
                null, CancellationToken.None);
            signResult.Success.ShouldBeTrue(signResult.ErrorMessage);
            File.Exists(sigFile).ShouldBeTrue();

            var verifyResult = await cryptoProvider.VerifyAsync(
                new VerifyRequest(inputFile, sigFile, keyId),
                null, CancellationToken.None);
            verifyResult.IsValid.ShouldBeTrue();
            verifyResult.Status.ShouldBe(VerifyStatus.Valid);
        }
        finally
        {
            File.Delete(inputFile);
            if (File.Exists(sigFile)) File.Delete(sigFile);
        }
    }

    [Fact]
    public async Task Encrypt_Armored_ProducesReadableOutput()
    {
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<PgpKeyService>();
        var cryptoProvider = scope.ServiceProvider.GetRequiredService<ICryptoProvider>();

        var keyResult = await keyService.GenerateAsync(new GeneratePgpKeyRequest
        {
            Name = $"Armored Test Key {Guid.NewGuid():N}",
            Algorithm = "rsa_2048",
            RealName = "Test",
            Email = "test@example.com",
        });
        var keyId = keyResult.Data!.Id;

        var inputFile = Path.GetTempFileName();
        var outputFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(inputFile, "Armored test content");

        try
        {
            var result = await cryptoProvider.EncryptAsync(
                new EncryptRequest(inputFile, outputFile, [keyId], null, OutputFormat.Armored),
                null, CancellationToken.None);
            result.Success.ShouldBeTrue(result.ErrorMessage);
            var content = await File.ReadAllTextAsync(outputFile);
            content.ShouldContain("BEGIN PGP MESSAGE");
        }
        finally
        {
            File.Delete(inputFile);
            File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task SignThenEncrypt_DecryptAndVerify()
    {
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<PgpKeyService>();
        var cryptoProvider = scope.ServiceProvider.GetRequiredService<ICryptoProvider>();

        // Generate two keys: one for encryption, one for signing
        var recipientKey = await keyService.GenerateAsync(new GeneratePgpKeyRequest
        {
            Name = $"Recipient {Guid.NewGuid():N}", Algorithm = "rsa_2048",
            RealName = "Recipient", Email = "recipient@example.com",
        });
        var signerKey = await keyService.GenerateAsync(new GeneratePgpKeyRequest
        {
            Name = $"Signer {Guid.NewGuid():N}", Algorithm = "rsa_2048",
            RealName = "Signer", Email = "signer@example.com",
        });

        var inputFile = Path.GetTempFileName();
        var encryptedFile = Path.GetTempFileName();
        var decryptedFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(inputFile, "Signed and encrypted content");

        try
        {
            // Sign-then-encrypt
            var encResult = await cryptoProvider.EncryptAsync(
                new EncryptRequest(inputFile, encryptedFile, [recipientKey.Data!.Id], signerKey.Data!.Id, OutputFormat.Binary),
                null, CancellationToken.None);
            encResult.Success.ShouldBeTrue(encResult.ErrorMessage);

            // Decrypt (verification happens inside PgpCryptoProvider if verify_signature=true)
            var decResult = await cryptoProvider.DecryptAsync(
                new DecryptRequest(encryptedFile, decryptedFile, recipientKey.Data.Id, true),
                null, CancellationToken.None);
            decResult.Success.ShouldBeTrue(decResult.ErrorMessage);
            (await File.ReadAllTextAsync(decryptedFile)).ShouldBe("Signed and encrypted content");
        }
        finally
        {
            File.Delete(inputFile);
            File.Delete(encryptedFile);
            File.Delete(decryptedFile);
        }
    }

    [Fact]
    public async Task Verify_TamperedFile_ReturnsInvalid()
    {
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<PgpKeyService>();
        var cryptoProvider = scope.ServiceProvider.GetRequiredService<ICryptoProvider>();

        var keyResult = await keyService.GenerateAsync(new GeneratePgpKeyRequest
        {
            Name = $"Tamper Test {Guid.NewGuid():N}", Algorithm = "rsa_2048",
            RealName = "Test", Email = "test@example.com",
        });
        var keyId = keyResult.Data!.Id;

        var inputFile = Path.GetTempFileName();
        var sigFile = inputFile + ".sig";
        await File.WriteAllTextAsync(inputFile, "Original content");

        try
        {
            await cryptoProvider.SignAsync(
                new SignRequest(inputFile, sigFile, keyId, SignatureMode.Detached),
                null, CancellationToken.None);

            // Tamper with file
            await File.WriteAllTextAsync(inputFile, "Tampered content");

            var verifyResult = await cryptoProvider.VerifyAsync(
                new VerifyRequest(inputFile, sigFile, keyId),
                null, CancellationToken.None);
            verifyResult.IsValid.ShouldBeFalse();
            verifyResult.Status.ShouldBe(VerifyStatus.Invalid);
        }
        finally
        {
            File.Delete(inputFile);
            if (File.Exists(sigFile)) File.Delete(sigFile);
        }
    }
}

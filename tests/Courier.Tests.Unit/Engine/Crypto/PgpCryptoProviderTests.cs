using Courier.Domain.Encryption;
using Courier.Features.AuditLog;
using Courier.Features.Engine.Crypto;
using Courier.Features.PgpKeys;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Crypto;

public class PgpCryptoProviderTests : IDisposable
{
    private readonly string _tempDir;

    public PgpCryptoProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "courier-pgp-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* cleanup best effort */ }
    }

    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    private static ICredentialEncryptor CreateMockEncryptor()
    {
        var encryptor = Substitute.For<ICredentialEncryptor>();
        encryptor.Encrypt(Arg.Any<string>()).Returns(ci =>
            System.Text.Encoding.UTF8.GetBytes($"enc:{ci.Arg<string>()}"));
        encryptor.Decrypt(Arg.Any<byte[]>()).Returns(ci =>
        {
            var bytes = ci.Arg<byte[]>();
            var str = System.Text.Encoding.UTF8.GetString(bytes);
            return str.StartsWith("enc:") ? str[4..] : str;
        });
        return encryptor;
    }

    private async Task<Guid> SeedGeneratedKey(CourierDbContext db, ICredentialEncryptor encryptor)
    {
        var keyService = new PgpKeyService(db, encryptor, new AuditService(db));
        var result = await keyService.GenerateAsync(new GeneratePgpKeyRequest
        {
            Name = "Test Key",
            Algorithm = "rsa_2048",
            RealName = "Test User",
            Email = "test@example.com",
        });
        return result.Data!.Id;
    }

    private string CreateTestFile(string content = "Hello, PGP World! This is a test of the encryption system.")
    {
        var path = Path.Combine(_tempDir, $"input-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, content);
        return path;
    }

    private string GetOutputPath(string suffix = ".pgp")
    {
        return Path.Combine(_tempDir, $"output-{Guid.NewGuid():N}{suffix}");
    }

    // ========== Test 1: Encrypt/Decrypt Round-Trip ==========

    [Fact]
    public async Task EncryptDecrypt_RoundTrip_ProducesOriginalContent()
    {
        // Arrange
        var encryptor = CreateMockEncryptor();
        using var db = CreateInMemoryContext();
        var keyId = await SeedGeneratedKey(db, encryptor);
        var provider = new PgpCryptoProvider(db, encryptor);

        var originalContent = "Hello, PGP World! This is a test of the encryption system.";
        var inputPath = CreateTestFile(originalContent);
        var encryptedPath = GetOutputPath(".pgp");
        var decryptedPath = GetOutputPath(".txt");

        // Act: Encrypt
        var encryptResult = await provider.EncryptAsync(
            new EncryptRequest(inputPath, encryptedPath, [keyId], null, OutputFormat.Binary),
            null, CancellationToken.None);

        encryptResult.Success.ShouldBeTrue(encryptResult.ErrorMessage);

        // Act: Decrypt
        var decryptResult = await provider.DecryptAsync(
            new DecryptRequest(encryptedPath, decryptedPath, keyId, false),
            null, CancellationToken.None);

        decryptResult.Success.ShouldBeTrue(decryptResult.ErrorMessage);

        // Assert
        var decryptedContent = await File.ReadAllTextAsync(decryptedPath);
        decryptedContent.ShouldBe(originalContent);
    }

    // ========== Test 2: Armored Format Output ==========

    [Fact]
    public async Task Encrypt_ArmoredFormat_ProducesAsciiOutput()
    {
        // Arrange
        var encryptor = CreateMockEncryptor();
        using var db = CreateInMemoryContext();
        var keyId = await SeedGeneratedKey(db, encryptor);
        var provider = new PgpCryptoProvider(db, encryptor);

        var inputPath = CreateTestFile();
        var encryptedPath = GetOutputPath(".asc");

        // Act
        var result = await provider.EncryptAsync(
            new EncryptRequest(inputPath, encryptedPath, [keyId], null, OutputFormat.Armored),
            null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue(result.ErrorMessage);
        var encryptedContent = await File.ReadAllTextAsync(encryptedPath);
        encryptedContent.ShouldContain("BEGIN PGP MESSAGE");
    }

    // ========== Test 3: Encrypt with Retired Key Fails ==========

    [Fact]
    public async Task Encrypt_RetiredKey_Fails()
    {
        // Arrange
        var encryptor = CreateMockEncryptor();
        using var db = CreateInMemoryContext();
        var keyId = await SeedGeneratedKey(db, encryptor);

        // Retire the key
        var key = await db.PgpKeys.FindAsync(keyId);
        key!.Status = "retired";
        await db.SaveChangesAsync();

        var provider = new PgpCryptoProvider(db, encryptor);
        var inputPath = CreateTestFile();
        var outputPath = GetOutputPath();

        // Act
        var result = await provider.EncryptAsync(
            new EncryptRequest(inputPath, outputPath, [keyId], null, OutputFormat.Binary),
            null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage!.ShouldContain("retired");
    }

    // ========== Test 4: Encrypt with Revoked Key Fails ==========

    [Fact]
    public async Task Encrypt_RevokedKey_Fails()
    {
        // Arrange
        var encryptor = CreateMockEncryptor();
        using var db = CreateInMemoryContext();
        var keyId = await SeedGeneratedKey(db, encryptor);

        // Revoke the key
        var key = await db.PgpKeys.FindAsync(keyId);
        key!.Status = "revoked";
        await db.SaveChangesAsync();

        var provider = new PgpCryptoProvider(db, encryptor);
        var inputPath = CreateTestFile();
        var outputPath = GetOutputPath();

        // Act
        var result = await provider.EncryptAsync(
            new EncryptRequest(inputPath, outputPath, [keyId], null, OutputFormat.Binary),
            null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage!.ShouldContain("revoked");
    }

    // ========== Test 5: Decrypt with Wrong Key Fails ==========

    [Fact]
    public async Task Decrypt_WrongKey_Fails()
    {
        // Arrange
        var encryptor = CreateMockEncryptor();
        using var db = CreateInMemoryContext();
        var keyIdA = await SeedGeneratedKey(db, encryptor);
        var keyIdB = await SeedGeneratedKey(db, encryptor);
        var provider = new PgpCryptoProvider(db, encryptor);

        var inputPath = CreateTestFile("Secret message for Key A only");
        var encryptedPath = GetOutputPath(".pgp");
        var decryptedPath = GetOutputPath(".txt");

        // Encrypt with Key A
        var encryptResult = await provider.EncryptAsync(
            new EncryptRequest(inputPath, encryptedPath, [keyIdA], null, OutputFormat.Binary),
            null, CancellationToken.None);
        encryptResult.Success.ShouldBeTrue(encryptResult.ErrorMessage);

        // Act: Decrypt with Key B
        var decryptResult = await provider.DecryptAsync(
            new DecryptRequest(encryptedPath, decryptedPath, keyIdB, false),
            null, CancellationToken.None);

        // Assert
        decryptResult.Success.ShouldBeFalse();
        decryptResult.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    // ========== Test 6: Sign/Verify Detached Round-Trip ==========

    [Fact]
    public async Task SignVerify_Detached_RoundTrip()
    {
        // Arrange
        var encryptor = CreateMockEncryptor();
        using var db = CreateInMemoryContext();
        var keyId = await SeedGeneratedKey(db, encryptor);
        var provider = new PgpCryptoProvider(db, encryptor);

        var inputPath = CreateTestFile("Document to be signed.");
        var signaturePath = GetOutputPath(".sig");

        // Act: Sign
        var signResult = await provider.SignAsync(
            new SignRequest(inputPath, signaturePath, keyId, SignatureMode.Detached),
            null, CancellationToken.None);

        signResult.Success.ShouldBeTrue(signResult.ErrorMessage);

        // Act: Verify
        var verifyResult = await provider.VerifyAsync(
            new VerifyRequest(inputPath, signaturePath, keyId),
            null, CancellationToken.None);

        // Assert
        verifyResult.IsValid.ShouldBeTrue();
        verifyResult.Status.ShouldBe(VerifyStatus.Valid);
        verifyResult.SignerFingerprint.ShouldNotBeNullOrEmpty();
        verifyResult.SignatureTimestamp.ShouldNotBeNull();
    }

    // ========== Test 7: Sign/Verify Inline Round-Trip ==========

    [Fact]
    public async Task SignVerify_Inline_RoundTrip()
    {
        // Arrange
        var encryptor = CreateMockEncryptor();
        using var db = CreateInMemoryContext();
        var keyId = await SeedGeneratedKey(db, encryptor);
        var provider = new PgpCryptoProvider(db, encryptor);

        var inputPath = CreateTestFile("Inline signed document content.");
        var signedPath = GetOutputPath(".pgp");

        // Act: Sign
        var signResult = await provider.SignAsync(
            new SignRequest(inputPath, signedPath, keyId, SignatureMode.Inline),
            null, CancellationToken.None);

        signResult.Success.ShouldBeTrue(signResult.ErrorMessage);

        // Act: Verify
        var verifyResult = await provider.VerifyAsync(
            new VerifyRequest(signedPath, null, keyId),
            null, CancellationToken.None);

        // Assert
        verifyResult.IsValid.ShouldBeTrue();
        verifyResult.Status.ShouldBe(VerifyStatus.Valid);
        verifyResult.SignerFingerprint.ShouldNotBeNullOrEmpty();
    }

    // ========== Test 8: Sign/Verify Clearsign Round-Trip ==========

    [Fact]
    public async Task SignVerify_Clearsign_RoundTrip()
    {
        // Arrange
        var encryptor = CreateMockEncryptor();
        using var db = CreateInMemoryContext();
        var keyId = await SeedGeneratedKey(db, encryptor);
        var provider = new PgpCryptoProvider(db, encryptor);

        var inputPath = CreateTestFile("Cleartext signed message.\nWith multiple lines.");
        var signedPath = GetOutputPath(".asc");

        // Act: Sign
        var signResult = await provider.SignAsync(
            new SignRequest(inputPath, signedPath, keyId, SignatureMode.Clearsign),
            null, CancellationToken.None);

        signResult.Success.ShouldBeTrue(signResult.ErrorMessage);

        // Verify file contains cleartext marker
        var signedContent = await File.ReadAllTextAsync(signedPath);
        signedContent.ShouldContain("BEGIN PGP SIGNED MESSAGE");

        // Act: Verify
        var verifyResult = await provider.VerifyAsync(
            new VerifyRequest(signedPath, null, keyId),
            null, CancellationToken.None);

        // Assert
        verifyResult.IsValid.ShouldBeTrue();
        verifyResult.Status.ShouldBe(VerifyStatus.Valid);
    }

    // ========== Test 9: Verify Tampered File Returns Invalid ==========

    [Fact]
    public async Task Verify_TamperedFile_ReturnsInvalid()
    {
        // Arrange
        var encryptor = CreateMockEncryptor();
        using var db = CreateInMemoryContext();
        var keyId = await SeedGeneratedKey(db, encryptor);
        var provider = new PgpCryptoProvider(db, encryptor);

        var inputPath = CreateTestFile("Original content for signing.");
        var signaturePath = GetOutputPath(".sig");

        // Sign the original file
        var signResult = await provider.SignAsync(
            new SignRequest(inputPath, signaturePath, keyId, SignatureMode.Detached),
            null, CancellationToken.None);
        signResult.Success.ShouldBeTrue(signResult.ErrorMessage);

        // Tamper with the file
        await File.WriteAllTextAsync(inputPath, "TAMPERED content that is different!");

        // Act: Verify tampered file
        var verifyResult = await provider.VerifyAsync(
            new VerifyRequest(inputPath, signaturePath, keyId),
            null, CancellationToken.None);

        // Assert
        verifyResult.IsValid.ShouldBeFalse();
        verifyResult.Status.ShouldBe(VerifyStatus.Invalid);
    }

    // ========== Test 10: Verify Unknown Signer Returns UnknownSigner ==========

    [Fact]
    public async Task Verify_UnknownSigner_ReturnsUnknownSigner()
    {
        // Arrange: Create signing key in one DB, verify against a different DB
        var encryptor = CreateMockEncryptor();
        using var dbSigner = CreateInMemoryContext();
        var signerKeyId = await SeedGeneratedKey(dbSigner, encryptor);
        var signerProvider = new PgpCryptoProvider(dbSigner, encryptor);

        var inputPath = CreateTestFile("Document signed by unknown signer.");
        var signaturePath = GetOutputPath(".sig");

        // Sign with key from dbSigner
        var signResult = await signerProvider.SignAsync(
            new SignRequest(inputPath, signaturePath, signerKeyId, SignatureMode.Detached),
            null, CancellationToken.None);
        signResult.Success.ShouldBeTrue(signResult.ErrorMessage);

        // Verify against a different DB that has no keys
        using var dbVerifier = CreateInMemoryContext();
        var verifierProvider = new PgpCryptoProvider(dbVerifier, encryptor);

        // Act: Verify — no ExpectedSignerKeyId, and signer key not in this DB
        var verifyResult = await verifierProvider.VerifyAsync(
            new VerifyRequest(inputPath, signaturePath, null),
            null, CancellationToken.None);

        // Assert
        verifyResult.IsValid.ShouldBeFalse();
        verifyResult.Status.ShouldBe(VerifyStatus.UnknownSigner);
    }

    // ========== Test 11: Sign with Retired Key Fails ==========

    [Fact]
    public async Task Sign_RetiredKey_Fails()
    {
        // Arrange
        var encryptor = CreateMockEncryptor();
        using var db = CreateInMemoryContext();
        var keyId = await SeedGeneratedKey(db, encryptor);

        // Retire the key
        var key = await db.PgpKeys.FindAsync(keyId);
        key!.Status = "retired";
        await db.SaveChangesAsync();

        var provider = new PgpCryptoProvider(db, encryptor);
        var inputPath = CreateTestFile();
        var outputPath = GetOutputPath(".sig");

        // Act
        var result = await provider.SignAsync(
            new SignRequest(inputPath, outputPath, keyId, SignatureMode.Detached),
            null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage!.ShouldContain("retired");
    }

    // ========== Test 12: Encrypt with Signing + Decrypt with Verify ==========

    [Fact]
    public async Task EncryptWithSigning_DecryptAndVerify()
    {
        // Arrange
        var encryptor = CreateMockEncryptor();
        using var db = CreateInMemoryContext();
        var keyId = await SeedGeneratedKey(db, encryptor);
        var provider = new PgpCryptoProvider(db, encryptor);

        var originalContent = "This message is encrypted AND signed.";
        var inputPath = CreateTestFile(originalContent);
        var encryptedPath = GetOutputPath(".pgp");
        var decryptedPath = GetOutputPath(".txt");

        // Act: Encrypt with signing
        var encryptResult = await provider.EncryptAsync(
            new EncryptRequest(inputPath, encryptedPath, [keyId], keyId, OutputFormat.Binary),
            null, CancellationToken.None);

        encryptResult.Success.ShouldBeTrue(encryptResult.ErrorMessage);

        // Act: Decrypt (with verify flag)
        var decryptResult = await provider.DecryptAsync(
            new DecryptRequest(encryptedPath, decryptedPath, keyId, true),
            null, CancellationToken.None);

        // Assert
        decryptResult.Success.ShouldBeTrue(decryptResult.ErrorMessage);
        var decryptedContent = await File.ReadAllTextAsync(decryptedPath);
        decryptedContent.ShouldBe(originalContent);
    }
}

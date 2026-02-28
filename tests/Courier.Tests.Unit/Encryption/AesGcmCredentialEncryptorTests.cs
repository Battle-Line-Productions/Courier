using System.Security.Cryptography;
using Courier.Infrastructure.Encryption;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Courier.Tests.Unit.Encryption;

public class AesGcmCredentialEncryptorTests
{
    private static AesGcmCredentialEncryptor CreateEncryptor(byte[]? kek = null)
    {
        kek ??= RandomNumberGenerator.GetBytes(32);
        var settings = Options.Create(new EncryptionSettings
        {
            KeyEncryptionKey = Convert.ToBase64String(kek)
        });
        return new AesGcmCredentialEncryptor(settings);
    }

    [Fact]
    public void EncryptDecrypt_Roundtrip_ReturnsOriginalPlaintext()
    {
        var encryptor = CreateEncryptor();
        var plaintext = "my-secret-password-123!@#";

        var encrypted = encryptor.Encrypt(plaintext);
        var decrypted = encryptor.Decrypt(encrypted);

        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public void Encrypt_DifferentPlaintexts_ProduceDifferentBlobs()
    {
        var encryptor = CreateEncryptor();

        var blob1 = encryptor.Encrypt("password-one");
        var blob2 = encryptor.Encrypt("password-two");

        blob1.ShouldNotBe(blob2);
    }

    [Fact]
    public void Encrypt_SamePlaintextTwice_ProducesDifferentBlobs()
    {
        var encryptor = CreateEncryptor();
        var plaintext = "same-password";

        var blob1 = encryptor.Encrypt(plaintext);
        var blob2 = encryptor.Encrypt(plaintext);

        blob1.SequenceEqual(blob2).ShouldBeFalse();
    }

    [Fact]
    public void Decrypt_WithWrongKek_Throws()
    {
        var encryptor1 = CreateEncryptor(RandomNumberGenerator.GetBytes(32));
        var encryptor2 = CreateEncryptor(RandomNumberGenerator.GetBytes(32));

        var encrypted = encryptor1.Encrypt("secret");

        Should.Throw<CryptographicException>(() => encryptor2.Decrypt(encrypted));
    }

    [Fact]
    public void Encrypt_BlobStartsWithVersionByte()
    {
        var encryptor = CreateEncryptor();

        var blob = encryptor.Encrypt("test");

        blob[0].ShouldBe((byte)0x01);
    }

    [Fact]
    public void Constructor_MissingKek_Throws()
    {
        var settings = Options.Create(new EncryptionSettings { KeyEncryptionKey = "" });

        Should.Throw<InvalidOperationException>(() => new AesGcmCredentialEncryptor(settings));
    }

    [Fact]
    public void Constructor_WrongLengthKek_Throws()
    {
        var settings = Options.Create(new EncryptionSettings
        {
            KeyEncryptionKey = Convert.ToBase64String(new byte[16]) // 16 bytes instead of 32
        });

        Should.Throw<InvalidOperationException>(() => new AesGcmCredentialEncryptor(settings));
    }

    [Fact]
    public void Decrypt_TruncatedBlob_Throws()
    {
        var encryptor = CreateEncryptor();

        Should.Throw<CryptographicException>(() => encryptor.Decrypt(new byte[10]));
    }

    [Fact]
    public void Decrypt_WrongVersionByte_Throws()
    {
        var encryptor = CreateEncryptor();
        var blob = encryptor.Encrypt("test");
        blob[0] = 0xFF; // corrupt version byte

        Should.Throw<CryptographicException>(() => encryptor.Decrypt(blob));
    }
}

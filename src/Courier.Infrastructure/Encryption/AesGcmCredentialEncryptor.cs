using System.Security.Cryptography;
using System.Text;
using Courier.Domain.Encryption;
using Microsoft.Extensions.Options;

namespace Courier.Infrastructure.Encryption;

public class AesGcmCredentialEncryptor : ICredentialEncryptor
{
    private const byte Version = 0x01;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int DekSizeBytes = 32;

    // Blob layout:
    // [1B version][12B dek-wrap-nonce][16B dek-wrap-tag][32B wrapped-dek][12B data-nonce][16B data-tag][N bytes ciphertext]
    private const int HeaderSize = 1 + NonceSizeBytes + TagSizeBytes + DekSizeBytes + NonceSizeBytes + TagSizeBytes;

    private readonly byte[] _kek;

    public AesGcmCredentialEncryptor(IOptions<EncryptionSettings> settings)
    {
        var kekBase64 = settings.Value.KeyEncryptionKey;

        if (string.IsNullOrWhiteSpace(kekBase64))
            throw new InvalidOperationException("Encryption KEK is not configured.");

        _kek = Convert.FromBase64String(kekBase64);

        if (_kek.Length != 32)
            throw new InvalidOperationException($"Encryption KEK must be exactly 32 bytes (got {_kek.Length}).");
    }

    public byte[] Encrypt(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var dek = new byte[DekSizeBytes];

        try
        {
            RandomNumberGenerator.Fill(dek);

            // Encrypt data with DEK
            var dataNonce = new byte[NonceSizeBytes];
            RandomNumberGenerator.Fill(dataNonce);
            var ciphertext = new byte[plaintextBytes.Length];
            var dataTag = new byte[TagSizeBytes];

            using (var dataAes = new AesGcm(dek, TagSizeBytes))
            {
                dataAes.Encrypt(dataNonce, plaintextBytes, ciphertext, dataTag);
            }

            // Wrap DEK with KEK
            var dekWrapNonce = new byte[NonceSizeBytes];
            RandomNumberGenerator.Fill(dekWrapNonce);
            var wrappedDek = new byte[DekSizeBytes];
            var dekWrapTag = new byte[TagSizeBytes];

            using (var kekAes = new AesGcm(_kek, TagSizeBytes))
            {
                kekAes.Encrypt(dekWrapNonce, dek, wrappedDek, dekWrapTag);
            }

            // Assemble blob
            var blob = new byte[HeaderSize + ciphertext.Length];
            var offset = 0;

            blob[offset++] = Version;

            Buffer.BlockCopy(dekWrapNonce, 0, blob, offset, NonceSizeBytes);
            offset += NonceSizeBytes;

            Buffer.BlockCopy(dekWrapTag, 0, blob, offset, TagSizeBytes);
            offset += TagSizeBytes;

            Buffer.BlockCopy(wrappedDek, 0, blob, offset, DekSizeBytes);
            offset += DekSizeBytes;

            Buffer.BlockCopy(dataNonce, 0, blob, offset, NonceSizeBytes);
            offset += NonceSizeBytes;

            Buffer.BlockCopy(dataTag, 0, blob, offset, TagSizeBytes);
            offset += TagSizeBytes;

            Buffer.BlockCopy(ciphertext, 0, blob, offset, ciphertext.Length);

            return blob;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    public string Decrypt(byte[] blob)
    {
        if (blob.Length < HeaderSize)
            throw new CryptographicException("Encrypted blob is too short.");

        if (blob[0] != Version)
            throw new CryptographicException($"Unsupported encryption version: {blob[0]}.");

        var offset = 1;

        var dekWrapNonce = blob.AsSpan(offset, NonceSizeBytes);
        offset += NonceSizeBytes;

        var dekWrapTag = blob.AsSpan(offset, TagSizeBytes);
        offset += TagSizeBytes;

        var wrappedDek = blob.AsSpan(offset, DekSizeBytes);
        offset += DekSizeBytes;

        var dataNonce = blob.AsSpan(offset, NonceSizeBytes);
        offset += NonceSizeBytes;

        var dataTag = blob.AsSpan(offset, TagSizeBytes);
        offset += TagSizeBytes;

        var ciphertext = blob.AsSpan(offset);

        var dek = new byte[DekSizeBytes];

        try
        {
            // Unwrap DEK
            using (var kekAes = new AesGcm(_kek, TagSizeBytes))
            {
                kekAes.Decrypt(dekWrapNonce, wrappedDek, dekWrapTag, dek);
            }

            // Decrypt data
            var plaintext = new byte[ciphertext.Length];

            using (var dataAes = new AesGcm(dek, TagSizeBytes))
            {
                dataAes.Decrypt(dataNonce, ciphertext, dataTag, plaintext);
            }

            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }
}

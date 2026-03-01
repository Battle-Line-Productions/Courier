using System.Security.Cryptography;
using System.Text;
using Courier.Domain.Common;
using Courier.Domain.Encryption;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.SshKeys;

public class SshKeyService
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private readonly AuditService _audit;

    public SshKeyService(CourierDbContext db, ICredentialEncryptor encryptor, AuditService audit)
    {
        _db = db;
        _encryptor = encryptor;
        _audit = audit;
    }

    public async Task<ApiResponse<SshKeyDto>> GenerateAsync(GenerateSshKeyRequest request, CancellationToken ct = default)
    {
        try
        {
            var (publicKeyOpenSsh, privateKeyPem) = GenerateKeyMaterial(request.KeyType);
            var fingerprint = ComputeFingerprint(publicKeyOpenSsh);

            var sshKey = new SshKey
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                KeyType = request.KeyType,
                PublicKeyData = publicKeyOpenSsh,
                PrivateKeyData = _encryptor.Encrypt(privateKeyPem),
                PassphraseHash = !string.IsNullOrEmpty(request.Passphrase)
                    ? _encryptor.Encrypt(request.Passphrase)
                    : null,
                Fingerprint = fingerprint,
                Status = "active",
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _db.SshKeys.Add(sshKey);
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync(AuditableEntityType.SshKey, sshKey.Id, "Created", details: new { sshKey.Name, sshKey.KeyType, sshKey.Fingerprint }, ct: ct);

            return new ApiResponse<SshKeyDto> { Data = MapToDto(sshKey) };
        }
        catch (Exception ex)
        {
            return new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyGenerationFailed, $"SSH key generation failed: {ex.Message}")
            };
        }
    }

    public async Task<ApiResponse<SshKeyDto>> ImportAsync(ImportSshKeyRequest request, Stream keyStream, CancellationToken ct = default)
    {
        try
        {
            using var reader = new StreamReader(keyStream);
            var keyContent = await reader.ReadToEndAsync(ct);
            keyContent = keyContent.Trim();

            string? publicKeyData = null;
            string? privateKeyData = null;
            string keyType;
            string? fingerprint = null;

            if (keyContent.StartsWith("ssh-") || keyContent.StartsWith("ecdsa-"))
            {
                // Public key in OpenSSH format
                publicKeyData = keyContent;
                keyType = DetectKeyType(keyContent);
                fingerprint = ComputeFingerprint(keyContent);
            }
            else if (keyContent.Contains("PRIVATE KEY"))
            {
                // Private key — determine type from PEM header
                privateKeyData = keyContent;
                keyType = DetectKeyTypeFromPrivateKey(keyContent);

                // Try to extract public key from private key
                publicKeyData = ExtractPublicKeyFromPrivate(keyContent, keyType);
                if (publicKeyData != null)
                    fingerprint = ComputeFingerprint(publicKeyData);
            }
            else
            {
                return new ApiResponse<SshKeyDto>
                {
                    Error = ErrorMessages.Create(ErrorCodes.KeyImportInvalidFormat, "Unrecognized SSH key format.")
                };
            }

            var sshKey = new SshKey
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                KeyType = keyType,
                PublicKeyData = publicKeyData,
                PrivateKeyData = privateKeyData != null ? _encryptor.Encrypt(privateKeyData) : null,
                PassphraseHash = !string.IsNullOrEmpty(request.Passphrase)
                    ? _encryptor.Encrypt(request.Passphrase)
                    : null,
                Fingerprint = fingerprint,
                Status = "active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _db.SshKeys.Add(sshKey);
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync(AuditableEntityType.SshKey, sshKey.Id, "Imported", details: new { sshKey.Name, sshKey.KeyType, sshKey.Fingerprint }, ct: ct);

            return new ApiResponse<SshKeyDto> { Data = MapToDto(sshKey) };
        }
        catch (Exception ex)
        {
            return new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyImportFailed, $"SSH key import failed: {ex.Message}")
            };
        }
    }

    public async Task<ApiResponse<SshKeyDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _db.SshKeys.FirstOrDefaultAsync(k => k.Id == id, ct);

        if (key is null)
        {
            return new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"SSH key with id '{id}' not found.")
            };
        }

        return new ApiResponse<SshKeyDto> { Data = MapToDto(key) };
    }

    public async Task<PagedApiResponse<SshKeyDto>> ListAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        string? keyType = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.SshKeys.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(k => k.Name.ToLower().Contains(term)
                || (k.Fingerprint != null && k.Fingerprint.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(k => k.Status == status);

        if (!string.IsNullOrWhiteSpace(keyType))
            query = query.Where(k => k.KeyType == keyType);

        query = query.OrderByDescending(k => k.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(k => MapToDto(k))
            .ToListAsync(ct);

        return new PagedApiResponse<SshKeyDto>
        {
            Data = items,
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    public async Task<ApiResponse<SshKeyDto>> UpdateAsync(Guid id, UpdateSshKeyRequest request, CancellationToken ct = default)
    {
        var key = await _db.SshKeys.FindAsync([id], ct);

        if (key is null)
        {
            return new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"SSH key with id '{id}' not found.")
            };
        }

        if (request.Name is not null)
            key.Name = request.Name;
        if (request.Notes is not null)
            key.Notes = request.Notes;

        key.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.SshKey, id, "Updated", ct: ct);

        return new ApiResponse<SshKeyDto> { Data = MapToDto(key) };
    }

    public async Task<ApiResponse> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _db.SshKeys.FindAsync([id], ct);

        if (key is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"SSH key with id '{id}' not found.")
            };
        }

        // Purge all key material
        key.PublicKeyData = null;
        key.PrivateKeyData = null;
        key.PassphraseHash = null;
        key.Status = "deleted";
        key.IsDeleted = true;
        key.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.SshKey, id, "Deleted", ct: ct);

        return new ApiResponse();
    }

    public async Task<ApiResponse<string>> ExportPublicKeyAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _db.SshKeys.FirstOrDefaultAsync(k => k.Id == id, ct);

        if (key is null)
        {
            return new ApiResponse<string>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"SSH key with id '{id}' not found.")
            };
        }

        if (key.PublicKeyData is null)
        {
            return new ApiResponse<string>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, "No public key data available.")
            };
        }

        return new ApiResponse<string> { Data = key.PublicKeyData };
    }

    public async Task<ApiResponse<SshKeyDto>> RetireAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _db.SshKeys.FindAsync([id], ct);

        if (key is null)
        {
            return new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"SSH key with id '{id}' not found.")
            };
        }

        if (key.Status == "retired")
        {
            return new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyAlreadyRetired, "Key is already retired.")
            };
        }

        if (key.Status != "active")
        {
            return new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidKeyTransition, $"Cannot retire a key with status '{key.Status}'.")
            };
        }

        key.Status = "retired";
        key.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ApiResponse<SshKeyDto> { Data = MapToDto(key) };
    }

    public async Task<ApiResponse<SshKeyDto>> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _db.SshKeys.FindAsync([id], ct);

        if (key is null)
        {
            return new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"SSH key with id '{id}' not found.")
            };
        }

        if (key.Status == "active")
        {
            return new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyAlreadyActive, "Key is already active.")
            };
        }

        if (key.Status != "retired")
        {
            return new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidKeyTransition,
                    $"Cannot activate a key with status '{key.Status}'. Only retired keys can be reactivated.")
            };
        }

        key.Status = "active";
        key.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ApiResponse<SshKeyDto> { Data = MapToDto(key) };
    }

    // --- Key generation helpers ---

    private static (string publicKeyOpenSsh, string privateKeyPem) GenerateKeyMaterial(string keyType)
    {
        return keyType switch
        {
            "rsa_2048" => GenerateRsaKey(2048),
            "rsa_4096" => GenerateRsaKey(4096),
            "ed25519" => GenerateEd25519Key(),
            "ecdsa_256" => GenerateEcdsaKey(),
            _ => throw new ArgumentException($"Unsupported key type: {keyType}")
        };
    }

    private static (string publicKeyOpenSsh, string privateKeyPem) GenerateRsaKey(int keySize)
    {
        using var rsa = RSA.Create(keySize);
        var privateKeyPem = rsa.ExportRSAPrivateKeyPem();
        var publicKeyOpenSsh = FormatRsaPublicKeyOpenSsh(rsa);
        return (publicKeyOpenSsh, privateKeyPem);
    }

    private static (string publicKeyOpenSsh, string privateKeyPem) GenerateEd25519Key()
    {
        // Use BouncyCastle for Ed25519 SSH key generation for consistent OpenSSH formatting
        var random = new Org.BouncyCastle.Security.SecureRandom();
        var generator = new Org.BouncyCastle.Crypto.Generators.Ed25519KeyPairGenerator();
        generator.Init(new Org.BouncyCastle.Crypto.Parameters.Ed25519KeyGenerationParameters(random));
        var keyPair = generator.GenerateKeyPair();

        var pubKey = (Org.BouncyCastle.Crypto.Parameters.Ed25519PublicKeyParameters)keyPair.Public;
        var privKey = (Org.BouncyCastle.Crypto.Parameters.Ed25519PrivateKeyParameters)keyPair.Private;

        var publicKeyOpenSsh = FormatEd25519PublicKeyOpenSsh(pubKey.GetEncoded());
        var privateKeyPem = FormatEd25519PrivateKeyPem(privKey.GetEncoded(), pubKey.GetEncoded());
        return (publicKeyOpenSsh, privateKeyPem);
    }

    private static (string publicKeyOpenSsh, string privateKeyPem) GenerateEcdsaKey()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKeyPem = ecdsa.ExportECPrivateKeyPem();
        var publicKeyOpenSsh = FormatEcdsaPublicKeyOpenSsh(ecdsa);
        return (publicKeyOpenSsh, privateKeyPem);
    }

    private static string FormatRsaPublicKeyOpenSsh(RSA rsa)
    {
        var parameters = rsa.ExportParameters(false);
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        WriteBytes(writer, Encoding.ASCII.GetBytes("ssh-rsa"));
        WriteBytes(writer, parameters.Exponent!);
        WriteMpint(writer, parameters.Modulus!);

        return $"ssh-rsa {Convert.ToBase64String(ms.ToArray())}";
    }

    private static string FormatEd25519PublicKeyOpenSsh(byte[] publicKey)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        WriteBytes(writer, Encoding.ASCII.GetBytes("ssh-ed25519"));
        WriteBytes(writer, publicKey);

        return $"ssh-ed25519 {Convert.ToBase64String(ms.ToArray())}";
    }

    private static string FormatEd25519PrivateKeyPem(byte[] privateKey, byte[] publicKey)
    {
        // OpenSSH private key format for Ed25519
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // AUTH_MAGIC
        writer.Write(Encoding.ASCII.GetBytes("openssh-key-v1\0"));

        // ciphername, kdfname, kdf, number of keys
        WriteBytes(writer, Encoding.ASCII.GetBytes("none"));
        WriteBytes(writer, Encoding.ASCII.GetBytes("none"));
        WriteBytes(writer, []);
        writer.Write(BitConverter.IsLittleEndian
            ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(1)
            : 1);

        // Public key section
        using var pubMs = new MemoryStream();
        using var pubWriter = new BinaryWriter(pubMs);
        WriteBytes(pubWriter, Encoding.ASCII.GetBytes("ssh-ed25519"));
        WriteBytes(pubWriter, publicKey);
        WriteBytes(writer, pubMs.ToArray());

        // Private key section
        using var privMs = new MemoryStream();
        using var privWriter = new BinaryWriter(privMs);
        var checkInt = (uint)Random.Shared.Next();
        privWriter.Write(BitConverter.IsLittleEndian
            ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(checkInt)
            : checkInt);
        privWriter.Write(BitConverter.IsLittleEndian
            ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(checkInt)
            : checkInt);
        WriteBytes(privWriter, Encoding.ASCII.GetBytes("ssh-ed25519"));
        WriteBytes(privWriter, publicKey);
        // Ed25519 private key is 64 bytes: 32-byte seed + 32-byte public key
        var fullPrivate = new byte[64];
        Buffer.BlockCopy(privateKey, 0, fullPrivate, 0, 32);
        Buffer.BlockCopy(publicKey, 0, fullPrivate, 32, 32);
        WriteBytes(privWriter, fullPrivate);
        WriteBytes(privWriter, Encoding.ASCII.GetBytes("")); // comment

        // Pad to block size (8)
        var privData = privMs.ToArray();
        var padLen = 8 - (privData.Length % 8);
        if (padLen < 8)
        {
            var padded = new byte[privData.Length + padLen];
            Buffer.BlockCopy(privData, 0, padded, 0, privData.Length);
            for (var i = 0; i < padLen; i++)
                padded[privData.Length + i] = (byte)(i + 1);
            privData = padded;
        }
        WriteBytes(writer, privData);

        var base64 = Convert.ToBase64String(ms.ToArray());
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN OPENSSH PRIVATE KEY-----");
        for (var i = 0; i < base64.Length; i += 70)
            sb.AppendLine(base64[i..Math.Min(i + 70, base64.Length)]);
        sb.AppendLine("-----END OPENSSH PRIVATE KEY-----");
        return sb.ToString();
    }

    private static string FormatEcdsaPublicKeyOpenSsh(ECDsa ecdsa)
    {
        var parameters = ecdsa.ExportParameters(false);
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        WriteBytes(writer, Encoding.ASCII.GetBytes("ecdsa-sha2-nistp256"));
        WriteBytes(writer, Encoding.ASCII.GetBytes("nistp256"));

        // Uncompressed point: 0x04 + X + Y
        var point = new byte[1 + parameters.Q.X!.Length + parameters.Q.Y!.Length];
        point[0] = 0x04;
        Buffer.BlockCopy(parameters.Q.X, 0, point, 1, parameters.Q.X.Length);
        Buffer.BlockCopy(parameters.Q.Y, 0, point, 1 + parameters.Q.X.Length, parameters.Q.Y.Length);
        WriteBytes(writer, point);

        return $"ecdsa-sha2-nistp256 {Convert.ToBase64String(ms.ToArray())}";
    }

    internal static string ComputeFingerprint(string publicKeyOpenSsh)
    {
        var parts = publicKeyOpenSsh.Split(' ');
        if (parts.Length < 2) return string.Empty;

        var keyBytes = Convert.FromBase64String(parts[1]);
        var hash = SHA256.HashData(keyBytes);
        return $"SHA256:{Convert.ToBase64String(hash).TrimEnd('=')}";
    }

    private static string DetectKeyType(string publicKeyOpenSsh)
    {
        if (publicKeyOpenSsh.StartsWith("ssh-rsa"))
        {
            // Estimate key size from blob length
            var parts = publicKeyOpenSsh.Split(' ');
            if (parts.Length >= 2)
            {
                var bytes = Convert.FromBase64String(parts[1]);
                return bytes.Length > 400 ? "rsa_4096" : "rsa_2048";
            }
            return "rsa_2048";
        }
        if (publicKeyOpenSsh.StartsWith("ssh-ed25519")) return "ed25519";
        if (publicKeyOpenSsh.StartsWith("ecdsa-sha2-nistp256")) return "ecdsa_256";
        return "rsa_2048";
    }

    private static string DetectKeyTypeFromPrivateKey(string pemContent)
    {
        if (pemContent.Contains("RSA PRIVATE KEY") || pemContent.Contains("BEGIN PRIVATE KEY"))
            return "rsa_2048";
        if (pemContent.Contains("OPENSSH PRIVATE KEY"))
        {
            if (pemContent.Contains("ed25519")) return "ed25519";
            return "rsa_2048";
        }
        if (pemContent.Contains("EC PRIVATE KEY"))
            return "ecdsa_256";
        return "rsa_2048";
    }

    private static string? ExtractPublicKeyFromPrivate(string pemContent, string keyType)
    {
        try
        {
            if (keyType.StartsWith("rsa"))
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(pemContent);
                return FormatRsaPublicKeyOpenSsh(rsa);
            }
            if (keyType == "ecdsa_256")
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportFromPem(pemContent);
                return FormatEcdsaPublicKeyOpenSsh(ecdsa);
            }
        }
        catch
        {
            // Cannot extract — that's okay for import
        }
        return null;
    }

    private static void WriteBytes(BinaryWriter writer, byte[] data)
    {
        writer.Write(BitConverter.IsLittleEndian
            ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(data.Length)
            : data.Length);
        writer.Write(data);
    }

    private static void WriteMpint(BinaryWriter writer, byte[] value)
    {
        // If the high bit is set, prepend a zero byte
        if (value.Length > 0 && (value[0] & 0x80) != 0)
        {
            writer.Write(BitConverter.IsLittleEndian
                ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(value.Length + 1)
                : value.Length + 1);
            writer.Write((byte)0);
            writer.Write(value);
        }
        else
        {
            WriteBytes(writer, value);
        }
    }

    private static SshKeyDto MapToDto(SshKey k) => new()
    {
        Id = k.Id,
        Name = k.Name,
        KeyType = k.KeyType,
        Fingerprint = k.Fingerprint,
        Status = k.Status,
        HasPublicKey = k.PublicKeyData is not null,
        HasPrivateKey = k.PrivateKeyData is not null,
        Notes = k.Notes,
        CreatedBy = k.CreatedBy,
        CreatedAt = k.CreatedAt,
        UpdatedAt = k.UpdatedAt,
    };
}

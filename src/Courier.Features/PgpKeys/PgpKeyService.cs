using Courier.Domain.Common;
using Courier.Domain.Encryption;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace Courier.Features.PgpKeys;

public class PgpKeyService
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private readonly AuditService _audit;

    public PgpKeyService(CourierDbContext db, ICredentialEncryptor encryptor, AuditService audit)
    {
        _db = db;
        _encryptor = encryptor;
        _audit = audit;
    }

    public async Task<ApiResponse<PgpKeyDto>> GenerateAsync(GeneratePgpKeyRequest request, CancellationToken ct = default)
    {
        try
        {
            var identity = BuildIdentity(request.RealName, request.Email);
            var keyPair = GenerateKeyPair(request.Algorithm);
            var passphrase = request.Passphrase ?? string.Empty;
            var expiresAt = request.ExpiresInDays.HasValue
                ? DateTime.UtcNow.AddDays(request.ExpiresInDays.Value)
                : (DateTime?)null;

            var pgpSecretKey = CreatePgpSecretKey(keyPair, identity, passphrase, expiresAt);
            var publicKeyArmored = ExportPublicKeyArmored(pgpSecretKey);
            var privateKeyArmored = ExportSecretKeyArmored(pgpSecretKey);
            var fingerprint = BitConverter.ToString(pgpSecretKey.PublicKey.GetFingerprint()).Replace("-", "").ToUpperInvariant();
            var shortKeyId = pgpSecretKey.KeyId.ToString("X16");

            var pgpKey = new PgpKey
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Fingerprint = fingerprint,
                ShortKeyId = shortKeyId,
                Algorithm = request.Algorithm,
                KeyType = "key_pair",
                Purpose = request.Purpose,
                Status = "active",
                PublicKeyData = publicKeyArmored,
                PrivateKeyData = _encryptor.Encrypt(privateKeyArmored),
                PassphraseHash = !string.IsNullOrEmpty(request.Passphrase)
                    ? _encryptor.Encrypt(request.Passphrase)
                    : null,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _db.PgpKeys.Add(pgpKey);
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync(AuditableEntityType.PgpKey, pgpKey.Id, "Created", details: new { pgpKey.Name, pgpKey.Algorithm, pgpKey.Fingerprint }, ct: ct);

            return new ApiResponse<PgpKeyDto> { Data = MapToDto(pgpKey) };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyGenerationFailed, $"PGP key generation failed: {ex.Message}")
            };
        }
    }

    public async Task<ApiResponse<PgpKeyDto>> ImportAsync(ImportPgpKeyRequest request, Stream keyStream, CancellationToken ct = default)
    {
        try
        {
            using var reader = new StreamReader(keyStream);
            var armoredKey = await reader.ReadToEndAsync(ct);

            var decoderStream = PgpUtilities.GetDecoderStream(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(armoredKey)));

            string? fingerprint = null;
            string? shortKeyId = null;
            string algorithm = "unknown";
            string keyType;
            string? publicKeyArmored = null;
            string? privateKeyArmored = null;

            // Try to read as secret key ring first
            try
            {
                var secretBundle = new PgpSecretKeyRingBundle(decoderStream);
                var secretRing = secretBundle.GetKeyRings().Cast<PgpSecretKeyRing>().FirstOrDefault();
                if (secretRing != null)
                {
                    var masterSecret = secretRing.GetSecretKey();
                    fingerprint = BitConverter.ToString(masterSecret.PublicKey.GetFingerprint()).Replace("-", "").ToUpperInvariant();
                    shortKeyId = masterSecret.KeyId.ToString("X16");
                    algorithm = DetectAlgorithm(masterSecret.PublicKey);
                    keyType = "key_pair";
                    publicKeyArmored = ExportPublicKeyFromSecretRing(secretRing);
                    privateKeyArmored = armoredKey;
                }
                else
                {
                    return new ApiResponse<PgpKeyDto>
                    {
                        Error = ErrorMessages.Create(ErrorCodes.KeyImportInvalidFormat, "No PGP key found in the uploaded file.")
                    };
                }
            }
            catch
            {
                // Not a secret key — try public key ring
                decoderStream = PgpUtilities.GetDecoderStream(
                    new MemoryStream(System.Text.Encoding.UTF8.GetBytes(armoredKey)));
                try
                {
                    var publicBundle = new PgpPublicKeyRingBundle(decoderStream);
                    var publicRing = publicBundle.GetKeyRings().Cast<PgpPublicKeyRing>().FirstOrDefault();
                    if (publicRing != null)
                    {
                        var masterPublic = publicRing.GetPublicKey();
                        fingerprint = BitConverter.ToString(masterPublic.GetFingerprint()).Replace("-", "").ToUpperInvariant();
                        shortKeyId = masterPublic.KeyId.ToString("X16");
                        algorithm = DetectAlgorithm(masterPublic);
                        keyType = "public_only";
                        publicKeyArmored = armoredKey;
                    }
                    else
                    {
                        return new ApiResponse<PgpKeyDto>
                        {
                            Error = ErrorMessages.Create(ErrorCodes.KeyImportInvalidFormat, "No PGP key found in the uploaded file.")
                        };
                    }
                }
                catch (Exception ex)
                {
                    return new ApiResponse<PgpKeyDto>
                    {
                        Error = ErrorMessages.Create(ErrorCodes.KeyImportInvalidFormat, $"Invalid PGP key format: {ex.Message}")
                    };
                }
            }

            var pgpKey = new PgpKey
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Fingerprint = fingerprint,
                ShortKeyId = shortKeyId,
                Algorithm = algorithm,
                KeyType = keyType,
                Purpose = request.Purpose,
                Status = "active",
                PublicKeyData = publicKeyArmored,
                PrivateKeyData = privateKeyArmored != null ? _encryptor.Encrypt(privateKeyArmored) : null,
                PassphraseHash = !string.IsNullOrEmpty(request.Passphrase)
                    ? _encryptor.Encrypt(request.Passphrase)
                    : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _db.PgpKeys.Add(pgpKey);
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync(AuditableEntityType.PgpKey, pgpKey.Id, "Imported", details: new { pgpKey.Name, pgpKey.Algorithm, pgpKey.Fingerprint }, ct: ct);

            return new ApiResponse<PgpKeyDto> { Data = MapToDto(pgpKey) };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyImportFailed, $"PGP key import failed: {ex.Message}")
            };
        }
    }

    public async Task<ApiResponse<PgpKeyDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _db.PgpKeys.FirstOrDefaultAsync(k => k.Id == id, ct);

        if (key is null)
        {
            return new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"PGP key with id '{id}' not found.")
            };
        }

        return new ApiResponse<PgpKeyDto> { Data = MapToDto(key) };
    }

    public async Task<PagedApiResponse<PgpKeyDto>> ListAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        string? keyType = null,
        string? algorithm = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.PgpKeys.AsQueryable();

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

        if (!string.IsNullOrWhiteSpace(algorithm))
            query = query.Where(k => k.Algorithm == algorithm);

        query = query.OrderByDescending(k => k.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(k => MapToDto(k))
            .ToListAsync(ct);

        return new PagedApiResponse<PgpKeyDto>
        {
            Data = items,
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    public async Task<ApiResponse<PgpKeyDto>> UpdateAsync(Guid id, UpdatePgpKeyRequest request, CancellationToken ct = default)
    {
        var key = await _db.PgpKeys.FindAsync([id], ct);

        if (key is null)
        {
            return new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"PGP key with id '{id}' not found.")
            };
        }

        if (request.Name is not null)
            key.Name = request.Name;
        if (request.Purpose is not null)
            key.Purpose = request.Purpose;
        if (request.Notes is not null)
            key.Notes = request.Notes;

        key.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.PgpKey, id, "Updated", ct: ct);

        return new ApiResponse<PgpKeyDto> { Data = MapToDto(key) };
    }

    public async Task<ApiResponse> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _db.PgpKeys.FindAsync([id], ct);

        if (key is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"PGP key with id '{id}' not found.")
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

        await _audit.LogAsync(AuditableEntityType.PgpKey, id, "Deleted", ct: ct);

        return new ApiResponse();
    }

    public async Task<ApiResponse<string>> ExportPublicKeyAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _db.PgpKeys.FirstOrDefaultAsync(k => k.Id == id, ct);

        if (key is null)
        {
            return new ApiResponse<string>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"PGP key with id '{id}' not found.")
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

    public async Task<ApiResponse<PgpKeyDto>> RetireAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _db.PgpKeys.FindAsync([id], ct);

        if (key is null)
        {
            return new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"PGP key with id '{id}' not found.")
            };
        }

        if (key.Status == "retired")
        {
            return new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyAlreadyRetired, "Key is already retired.")
            };
        }

        if (key.Status is not "active" and not "expiring")
        {
            return new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidKeyTransition, $"Cannot retire a key with status '{key.Status}'.")
            };
        }

        key.Status = "retired";
        key.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ApiResponse<PgpKeyDto> { Data = MapToDto(key) };
    }

    public async Task<ApiResponse<PgpKeyDto>> RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _db.PgpKeys.FindAsync([id], ct);

        if (key is null)
        {
            return new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"PGP key with id '{id}' not found.")
            };
        }

        if (key.Status == "revoked")
        {
            return new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyAlreadyRevoked, "Key is already revoked.")
            };
        }

        if (key.Status is "deleted")
        {
            return new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidKeyTransition, "Cannot revoke a deleted key.")
            };
        }

        // Revoke purges private key material
        key.Status = "revoked";
        key.PrivateKeyData = null;
        key.PassphraseHash = null;
        key.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ApiResponse<PgpKeyDto> { Data = MapToDto(key) };
    }

    public async Task<ApiResponse<PgpKeyDto>> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _db.PgpKeys.FindAsync([id], ct);

        if (key is null)
        {
            return new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"PGP key with id '{id}' not found.")
            };
        }

        if (key.Status == "active")
        {
            return new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyAlreadyActive, "Key is already active.")
            };
        }

        if (key.Status != "retired")
        {
            return new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidKeyTransition,
                    $"Cannot activate a key with status '{key.Status}'. Only retired keys can be reactivated.")
            };
        }

        key.Status = "active";
        key.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ApiResponse<PgpKeyDto> { Data = MapToDto(key) };
    }

    // --- Key generation helpers ---

    private static AsymmetricCipherKeyPair GenerateKeyPair(string algorithm)
    {
        var random = new SecureRandom();

        return algorithm switch
        {
            "rsa_2048" => GenerateRsaKeyPair(random, 2048),
            "rsa_3072" => GenerateRsaKeyPair(random, 3072),
            "rsa_4096" => GenerateRsaKeyPair(random, 4096),
            "ecc_curve25519" => GenerateEd25519KeyPair(random),
            "ecc_p256" => GenerateEcKeyPair(random, "P-256"),
            "ecc_p384" => GenerateEcKeyPair(random, "P-384"),
            _ => throw new ArgumentException($"Unsupported algorithm: {algorithm}")
        };
    }

    private static AsymmetricCipherKeyPair GenerateRsaKeyPair(SecureRandom random, int keySize)
    {
        var generator = new RsaKeyPairGenerator();
        generator.Init(new RsaKeyGenerationParameters(
            BigInteger.ValueOf(65537), random, keySize, 80));
        return generator.GenerateKeyPair();
    }

    private static AsymmetricCipherKeyPair GenerateEd25519KeyPair(SecureRandom random)
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(random));
        return generator.GenerateKeyPair();
    }

    private static AsymmetricCipherKeyPair GenerateEcKeyPair(SecureRandom random, string curveName)
    {
        var generator = new ECKeyPairGenerator();
        generator.Init(new ECKeyGenerationParameters(
            Org.BouncyCastle.Asn1.X9.ECNamedCurveTable.GetOid(curveName), random));
        return generator.GenerateKeyPair();
    }

    private static PgpSecretKey CreatePgpSecretKey(
        AsymmetricCipherKeyPair keyPair,
        string identity,
        string passphrase,
        DateTime? expiresAt)
    {
        var now = DateTime.UtcNow;

        var hashedSubpacketGenerator = new PgpSignatureSubpacketGenerator();
        hashedSubpacketGenerator.SetKeyFlags(false,
            PgpKeyFlags.CanSign | PgpKeyFlags.CanCertify | PgpKeyFlags.CanEncryptCommunications | PgpKeyFlags.CanEncryptStorage);
        hashedSubpacketGenerator.SetPreferredHashAlgorithms(false, [
            (int)HashAlgorithmTag.Sha256,
            (int)HashAlgorithmTag.Sha384,
            (int)HashAlgorithmTag.Sha512
        ]);
        hashedSubpacketGenerator.SetPreferredSymmetricAlgorithms(false, [
            (int)SymmetricKeyAlgorithmTag.Aes256,
            (int)SymmetricKeyAlgorithmTag.Aes192,
            (int)SymmetricKeyAlgorithmTag.Aes128
        ]);

        if (expiresAt.HasValue)
        {
            var seconds = (long)(expiresAt.Value - now).TotalSeconds;
            if (seconds > 0)
                hashedSubpacketGenerator.SetKeyExpirationTime(false, seconds);
        }

        var secretKey = new PgpSecretKey(
            PgpSignature.DefaultCertification,
            keyPair.Public is Ed25519PublicKeyParameters
                ? PublicKeyAlgorithmTag.EdDsa
                : keyPair.Public is ECPublicKeyParameters
                    ? PublicKeyAlgorithmTag.ECDsa
                    : PublicKeyAlgorithmTag.RsaGeneral,
            keyPair.Public,
            keyPair.Private,
            now,
            identity,
            SymmetricKeyAlgorithmTag.Aes256,
            passphrase.ToCharArray(),
            hashedSubpacketGenerator.Generate(),
            null,
            new SecureRandom());

        return secretKey;
    }

    private static string BuildIdentity(string? realName, string? email)
    {
        if (!string.IsNullOrWhiteSpace(realName) && !string.IsNullOrWhiteSpace(email))
            return $"{realName} <{email}>";
        if (!string.IsNullOrWhiteSpace(email))
            return email;
        if (!string.IsNullOrWhiteSpace(realName))
            return realName;
        return "Courier Key";
    }

    private static string ExportPublicKeyArmored(PgpSecretKey secretKey)
    {
        using var ms = new MemoryStream();
        using (var armoredStream = new ArmoredOutputStream(ms))
        {
            secretKey.PublicKey.Encode(armoredStream);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string ExportSecretKeyArmored(PgpSecretKey secretKey)
    {
        using var ms = new MemoryStream();
        using (var armoredStream = new ArmoredOutputStream(ms))
        {
            secretKey.Encode(armoredStream);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string ExportPublicKeyFromSecretRing(PgpSecretKeyRing secretRing)
    {
        using var ms = new MemoryStream();
        using (var armoredStream = new ArmoredOutputStream(ms))
        {
            foreach (PgpSecretKey key in secretRing.GetSecretKeys())
            {
                key.PublicKey.Encode(armoredStream);
            }
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string DetectAlgorithm(PgpPublicKey publicKey)
    {
        return publicKey.Algorithm switch
        {
            PublicKeyAlgorithmTag.RsaGeneral or PublicKeyAlgorithmTag.RsaEncrypt or PublicKeyAlgorithmTag.RsaSign =>
                publicKey.BitStrength switch
                {
                    <= 2048 => "rsa_2048",
                    <= 3072 => "rsa_3072",
                    _ => "rsa_4096"
                },
            PublicKeyAlgorithmTag.EdDsa => "ecc_curve25519",
            PublicKeyAlgorithmTag.ECDsa => "ecc_p256",
            PublicKeyAlgorithmTag.ECDH => "ecc_p256",
            _ => "unknown"
        };
    }

    private static PgpKeyDto MapToDto(PgpKey k) => new()
    {
        Id = k.Id,
        Name = k.Name,
        Fingerprint = k.Fingerprint,
        ShortKeyId = k.ShortKeyId,
        Algorithm = k.Algorithm,
        KeyType = k.KeyType,
        Purpose = k.Purpose,
        Status = k.Status,
        HasPublicKey = k.PublicKeyData is not null,
        HasPrivateKey = k.PrivateKeyData is not null,
        ExpiresAt = k.ExpiresAt,
        SuccessorKeyId = k.SuccessorKeyId,
        CreatedBy = k.CreatedBy,
        Notes = k.Notes,
        CreatedAt = k.CreatedAt,
        UpdatedAt = k.UpdatedAt,
    };
}

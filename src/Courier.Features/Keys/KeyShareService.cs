using System.Security.Cryptography;
using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Features.Settings;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Keys;

public class KeyShareService
{
    private readonly CourierDbContext _db;
    private readonly SettingsService _settings;

    public KeyShareService(CourierDbContext db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<ApiResponse<ShareLinkResponse>> CreateShareLinkAsync(
        Guid keyId, string keyType, string? createdBy, int? expiryDays, CancellationToken ct)
    {
        var featureEnabled = await IsFeatureEnabledAsync(ct);
        if (!featureEnabled)
        {
            return new ApiResponse<ShareLinkResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.ShareLinksDisabled,
                    "Public key share links are not enabled. Enable the 'security.public_key_share_links_enabled' setting.")
            };
        }

        // Validate the key exists
        var keyExists = keyType switch
        {
            "pgp" => await _db.PgpKeys.AnyAsync(k => k.Id == keyId, ct),
            "ssh" => await _db.SshKeys.AnyAsync(k => k.Id == keyId, ct),
            _ => false
        };

        if (!keyExists)
        {
            return new ApiResponse<ShareLinkResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, $"Key with id '{keyId}' not found.")
            };
        }

        // Determine expiry
        var maxDaysSetting = await _settings.GetSettingAsync("security.max_share_link_days", ct);
        var maxDays = int.TryParse(maxDaysSetting, out var md) ? md : 30;

        var effectiveDays = expiryDays.HasValue
            ? Math.Min(expiryDays.Value, maxDays)
            : maxDays;

        if (effectiveDays <= 0)
            effectiveDays = maxDays;

        // Generate secure token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        // Hash the token with a random salt
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var hash = Convert.ToBase64String(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token + salt)));

        var link = new KeyShareLink
        {
            Id = Guid.CreateVersion7(),
            KeyId = keyId,
            KeyType = keyType,
            TokenHash = hash,
            TokenSalt = salt,
            ExpiresAt = DateTime.UtcNow.AddDays(effectiveDays),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };

        _db.KeyShareLinks.Add(link);
        await _db.SaveChangesAsync(ct);

        return new ApiResponse<ShareLinkResponse>
        {
            Data = new ShareLinkResponse(link.Id, token, link.ExpiresAt)
        };
    }

    public async Task<ApiResponse> RevokeShareLinkAsync(Guid linkId, CancellationToken ct)
    {
        var link = await _db.KeyShareLinks.FindAsync([linkId], ct);

        if (link is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.ShareLinkNotFound,
                    $"Share link with id '{linkId}' not found.")
            };
        }

        if (link.RevokedAt.HasValue)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.ShareLinkRevoked,
                    "Share link is already revoked.")
            };
        }

        link.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new ApiResponse();
    }

    public async Task<ApiResponse<SharedKeyResponse>> GetSharedKeyAsync(
        string token, string keyType, CancellationToken ct)
    {
        var featureEnabled = await IsFeatureEnabledAsync(ct);
        if (!featureEnabled)
        {
            return new ApiResponse<SharedKeyResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.ShareLinksDisabled,
                    "Public key share links are not enabled.")
            };
        }

        // Find all non-revoked, non-expired links for this key type and try to match the token
        var candidates = await _db.KeyShareLinks
            .Where(l => l.KeyType == keyType
                        && l.RevokedAt == null
                        && l.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

        KeyShareLink? matchedLink = null;

        foreach (var candidate in candidates)
        {
            var candidateHash = Convert.ToBase64String(
                SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token + candidate.TokenSalt)));

            if (CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(candidateHash),
                System.Text.Encoding.UTF8.GetBytes(candidate.TokenHash)))
            {
                matchedLink = candidate;
                break;
            }
        }

        if (matchedLink is null)
        {
            return new ApiResponse<SharedKeyResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.ShareLinkInvalidToken,
                    "Invalid or expired share link token.")
            };
        }

        // Fetch the public key material
        return keyType switch
        {
            "pgp" => await GetPgpPublicKeyAsync(matchedLink.KeyId, ct),
            "ssh" => await GetSshPublicKeyAsync(matchedLink.KeyId, ct),
            _ => new ApiResponse<SharedKeyResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, "Invalid key type.")
            }
        };
    }

    public async Task<ApiResponse<List<ShareLinkListItem>>> ListShareLinksAsync(
        Guid keyId, string keyType, CancellationToken ct)
    {
        var links = await _db.KeyShareLinks
            .Where(l => l.KeyId == keyId && l.KeyType == keyType)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new ShareLinkListItem(
                l.Id,
                l.ExpiresAt,
                l.CreatedBy,
                l.CreatedAt,
                l.RevokedAt,
                l.RevokedAt.HasValue ? "revoked"
                    : l.ExpiresAt < DateTime.UtcNow ? "expired"
                    : "active"))
            .ToListAsync(ct);

        return new ApiResponse<List<ShareLinkListItem>> { Data = links };
    }

    private async Task<bool> IsFeatureEnabledAsync(CancellationToken ct)
    {
        var setting = await _settings.GetSettingAsync("security.public_key_share_links_enabled", ct);
        return string.Equals(setting, "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ApiResponse<SharedKeyResponse>> GetPgpPublicKeyAsync(Guid keyId, CancellationToken ct)
    {
        var key = await _db.PgpKeys.FirstOrDefaultAsync(k => k.Id == keyId, ct);

        if (key is null || key.PublicKeyData is null)
        {
            return new ApiResponse<SharedKeyResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, "Key not found or has no public key data.")
            };
        }

        return new ApiResponse<SharedKeyResponse>
        {
            Data = new SharedKeyResponse(key.PublicKeyData, "pgp", key.Name)
        };
    }

    private async Task<ApiResponse<SharedKeyResponse>> GetSshPublicKeyAsync(Guid keyId, CancellationToken ct)
    {
        var key = await _db.SshKeys.FirstOrDefaultAsync(k => k.Id == keyId, ct);

        if (key is null || key.PublicKeyData is null)
        {
            return new ApiResponse<SharedKeyResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyNotFound, "Key not found or has no public key data.")
            };
        }

        return new ApiResponse<SharedKeyResponse>
        {
            Data = new SharedKeyResponse(key.PublicKeyData, "ssh", key.Name)
        };
    }
}

public record ShareLinkListItem(
    Guid Id,
    DateTime ExpiresAt,
    string? CreatedBy,
    DateTime CreatedAt,
    DateTime? RevokedAt,
    string Status);

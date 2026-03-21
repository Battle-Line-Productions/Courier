using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Courier.Domain.Common;
using Courier.Domain.Encryption;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Courier.Features.Auth.Sso;

public class SsoService
{
    private readonly CourierDbContext _db;
    private readonly OidcHandler _oidcHandler;
    private readonly SamlHandler _samlHandler;
    private readonly AuthService _authService;
    private readonly AuditService _audit;
    private readonly IMemoryCache _cache;
    private readonly SsoSettings _ssoSettings;
    private readonly ICredentialEncryptor _encryptor;

    public SsoService(
        CourierDbContext db,
        OidcHandler oidcHandler,
        SamlHandler samlHandler,
        AuthService authService,
        AuditService audit,
        IMemoryCache cache,
        IOptions<SsoSettings> ssoSettings,
        ICredentialEncryptor encryptor)
    {
        _db = db;
        _oidcHandler = oidcHandler;
        _samlHandler = samlHandler;
        _authService = authService;
        _audit = audit;
        _cache = cache;
        _ssoSettings = ssoSettings.Value;
        _encryptor = encryptor;
    }

    public string? GetFrontendCallbackUrl() => _ssoSettings.FrontendCallbackUrl;

    public string GetApiBaseUrl(HttpRequest request)
    {
        if (!string.IsNullOrEmpty(_ssoSettings.ApiBaseUrl))
            return _ssoSettings.ApiBaseUrl.TrimEnd('/');

        return $"{request.Scheme}://{request.Host}";
    }

    public async Task<ApiResponse<string>> InitiateLoginAsync(
        Guid providerId,
        string state,
        string codeChallenge,
        string apiBaseUrl,
        CancellationToken ct)
    {
        var provider = await _db.AuthProviders.FirstOrDefaultAsync(p => p.Id == providerId, ct);

        if (provider is null)
        {
            return new ApiResponse<string>
            {
                Error = ErrorMessages.Create(ErrorCodes.SsoProviderNotFound, "SSO provider not found.")
            };
        }

        if (!provider.IsEnabled)
        {
            return new ApiResponse<string>
            {
                Error = ErrorMessages.Create(ErrorCodes.SsoProviderDisabled, "SSO provider is not enabled.")
            };
        }

        if (string.IsNullOrEmpty(_ssoSettings.FrontendCallbackUrl))
        {
            return new ApiResponse<string>
            {
                Error = ErrorMessages.Create(ErrorCodes.SsoNotConfigured, "SSO frontend callback URL is not configured.")
            };
        }

        var decryptedConfig = DecryptConfiguration(provider.Configuration);
        using var configDoc = JsonDocument.Parse(decryptedConfig);
        var config = configDoc.RootElement.Clone();

        var callbackUrl = apiBaseUrl.TrimEnd('/') + "/api/v1/auth/sso/callback";
        string redirectUrl;

        if (provider.Type == "oidc")
        {
            redirectUrl = await _oidcHandler.BuildAuthorizationUrl(config, callbackUrl, state, codeChallenge, null, ct);
        }
        else if (provider.Type == "saml")
        {
            redirectUrl = _samlHandler.BuildAuthnRequestUrl(config, state, callbackUrl);
        }
        else
        {
            return new ApiResponse<string>
            {
                Error = ErrorMessages.Create(ErrorCodes.SsoProviderNotFound, $"Unsupported SSO provider type: {provider.Type}")
            };
        }

        return new ApiResponse<string> { Data = redirectUrl };
    }

    public async Task<ApiResponse<string>> HandleOidcCallbackAsync(
        string code,
        string state,
        string codeVerifier,
        Guid providerId,
        string? ipAddress,
        string apiBaseUrl,
        CancellationToken ct)
    {
        try
        {
            var provider = await _db.AuthProviders.FirstOrDefaultAsync(p => p.Id == providerId, ct);
            if (provider is null)
            {
                return new ApiResponse<string>
                {
                    Error = ErrorMessages.Create(ErrorCodes.SsoProviderNotFound, "SSO provider not found.")
                };
            }

            var decryptedConfig = DecryptConfiguration(provider.Configuration);
            using var configDoc = JsonDocument.Parse(decryptedConfig);
            var config = configDoc.RootElement.Clone();

            var callbackUrl = apiBaseUrl.TrimEnd('/') + "/api/v1/auth/sso/callback";

            var claims = await _oidcHandler.ExchangeCodeAsync(config, code, callbackUrl, codeVerifier, ct);

            var provisionResult = await ProvisionOrLinkUserAsync(claims, provider, ct);
            if (provisionResult.Error is not null)
            {
                return new ApiResponse<string> { Error = provisionResult.Error };
            }

            var userId = provisionResult.Data;

            await _audit.LogAsync(AuditableEntityType.User, userId, "sso_login_success",
                details: new { IpAddress = ipAddress, ProviderId = providerId, ProviderName = provider.Name }, ct: ct);

            var exchangeCode = CreateExchangeCode(userId, providerId);
            return new ApiResponse<string> { Data = exchangeCode };
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(AuditableEntityType.AuthProvider, providerId, "sso_login_failed",
                details: new { IpAddress = ipAddress, Error = ex.Message }, ct: ct);

            return new ApiResponse<string>
            {
                Error = ErrorMessages.Create(ErrorCodes.SsoIdTokenValidationFailed, $"OIDC login failed: {ex.Message}")
            };
        }
    }

    public async Task<ApiResponse<string>> HandleSamlCallbackAsync(
        string samlResponse,
        Guid providerId,
        string? ipAddress,
        string apiBaseUrl,
        CancellationToken ct)
    {
        try
        {
            var provider = await _db.AuthProviders.FirstOrDefaultAsync(p => p.Id == providerId, ct);
            if (provider is null)
            {
                return new ApiResponse<string>
                {
                    Error = ErrorMessages.Create(ErrorCodes.SsoProviderNotFound, "SSO provider not found.")
                };
            }

            var decryptedConfig = DecryptConfiguration(provider.Configuration);
            using var configDoc = JsonDocument.Parse(decryptedConfig);
            var config = configDoc.RootElement.Clone();

            var callbackUrl = apiBaseUrl.TrimEnd('/') + "/api/v1/auth/sso/callback";

            var claims = _samlHandler.ValidateAndExtractClaims(samlResponse, config, callbackUrl);

            var provisionResult = await ProvisionOrLinkUserAsync(claims, provider, ct);
            if (provisionResult.Error is not null)
            {
                return new ApiResponse<string> { Error = provisionResult.Error };
            }

            var userId = provisionResult.Data;

            await _audit.LogAsync(AuditableEntityType.User, userId, "sso_login_success",
                details: new { IpAddress = ipAddress, ProviderId = providerId, ProviderName = provider.Name }, ct: ct);

            var exchangeCode = CreateExchangeCode(userId, providerId);
            return new ApiResponse<string> { Data = exchangeCode };
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(AuditableEntityType.AuthProvider, providerId, "sso_login_failed",
                details: new { IpAddress = ipAddress, Error = ex.Message }, ct: ct);

            return new ApiResponse<string>
            {
                Error = ErrorMessages.Create(ErrorCodes.SsoSamlValidationFailed, $"SAML login failed: {ex.Message}")
            };
        }
    }

    public async Task<ApiResponse<Guid>> ProvisionOrLinkUserAsync(
        SsoClaimsPrincipal claims,
        AuthProvider provider,
        CancellationToken ct)
    {
        var existingLink = await _db.SsoUserLinks
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.ProviderId == provider.Id && l.SubjectId == claims.SubjectId, ct);

        if (existingLink is not null)
        {
            var user = existingLink.User;

            if (!user.IsActive)
            {
                return new ApiResponse<Guid>
                {
                    Error = ErrorMessages.Create(ErrorCodes.AccountDisabled, "This account has been disabled.")
                };
            }

            if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
            {
                return new ApiResponse<Guid>
                {
                    Error = ErrorMessages.Create(ErrorCodes.AccountLocked, "Account is locked. Try again later.")
                };
            }

            // Update link and user info
            existingLink.LastLoginAt = DateTime.UtcNow;
            existingLink.Email = claims.Email;
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(claims.DisplayName))
                user.DisplayName = claims.DisplayName;

            if (!string.IsNullOrEmpty(claims.Email))
                user.Email = claims.Email;

            await ApplyRoleMappingAsync(claims, provider, user, ct);
            await _db.SaveChangesAsync(ct);

            return new ApiResponse<Guid> { Data = user.Id };
        }

        // No existing link — auto-provision
        if (!provider.AutoProvision)
        {
            return new ApiResponse<Guid>
            {
                Error = ErrorMessages.Create(ErrorCodes.SsoAutoProvisionDisabled,
                    "Automatic account provisioning is disabled for this SSO provider.")
            };
        }

        // Check email collision
        if (!string.IsNullOrEmpty(claims.Email))
        {
            var emailCollision = await _db.Users
                .AnyAsync(u => u.Email == claims.Email && !u.IsDeleted, ct);

            if (emailCollision)
            {
                return new ApiResponse<Guid>
                {
                    Error = ErrorMessages.Create(ErrorCodes.SsoEmailCollision,
                        "A user with this email already exists. Contact an administrator to link your account.")
                };
            }
        }

        // Generate unique username from email
        var username = GenerateUsername(claims.Email);
        var usernameExists = await _db.Users.AnyAsync(u => u.Username == username, ct);
        if (usernameExists)
        {
            username = $"{username}-{Guid.NewGuid().ToString("N")[..6]}";
        }

        var now = DateTime.UtcNow;
        var newUser = new User
        {
            Id = Guid.CreateVersion7(),
            Username = username,
            DisplayName = claims.DisplayName ?? username,
            Email = claims.Email,
            Role = provider.DefaultRole,
            IsSsoUser = true,
            SsoProviderId = provider.Id,
            SsoSubjectId = claims.SubjectId,
            PasswordHash = null,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            LastLoginAt = now,
        };

        var newLink = new SsoUserLink
        {
            Id = Guid.CreateVersion7(),
            UserId = newUser.Id,
            ProviderId = provider.Id,
            SubjectId = claims.SubjectId,
            Email = claims.Email,
            LinkedAt = now,
            LastLoginAt = now,
        };

        _db.Users.Add(newUser);
        _db.SsoUserLinks.Add(newLink);

        await ApplyRoleMappingAsync(claims, provider, newUser, ct);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.User, newUser.Id, "sso_user_provisioned",
            details: new { ProviderId = provider.Id, ProviderName = provider.Name, SubjectId = claims.SubjectId }, ct: ct);

        return new ApiResponse<Guid> { Data = newUser.Id };
    }

    public async Task ApplyRoleMappingAsync(
        SsoClaimsPrincipal claims,
        AuthProvider provider,
        User user,
        CancellationToken ct)
    {
        var targetRole = provider.DefaultRole;

        try
        {
            using var mappingDoc = JsonDocument.Parse(provider.RoleMapping);
            var root = mappingDoc.RootElement;

            if (root.TryGetProperty("enabled", out var enabledProp) && enabledProp.GetBoolean()
                && root.TryGetProperty("rules", out var rulesProp) && rulesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var rule in rulesProp.EnumerateArray())
                {
                    if (!rule.TryGetProperty("value", out var valueProp) || !rule.TryGetProperty("role", out var roleProp))
                        continue;

                    var groupValue = valueProp.GetString();
                    var mappedRole = roleProp.GetString();

                    if (string.IsNullOrEmpty(groupValue) || string.IsNullOrEmpty(mappedRole))
                        continue;

                    if (claims.Groups.Contains(groupValue, StringComparer.OrdinalIgnoreCase))
                    {
                        targetRole = mappedRole;
                        break; // First match wins
                    }
                }
            }
        }
        catch (JsonException)
        {
            // If role mapping JSON is invalid, fall through to default role
        }

        if (user.Role != targetRole)
        {
            var oldRole = user.Role;
            user.Role = targetRole;

            await _audit.LogAsync(AuditableEntityType.User, user.Id, "sso_role_updated",
                details: new { OldRole = oldRole, NewRole = targetRole, ProviderId = provider.Id }, ct: ct);
        }
    }

    public string CreateExchangeCode(Guid userId, Guid providerId)
    {
        var codeBytes = RandomNumberGenerator.GetBytes(32);
        var code = Convert.ToBase64String(codeBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var data = new SsoExchangeCodeData(userId, providerId, DateTime.UtcNow);
        _cache.Set(code, data, TimeSpan.FromSeconds(60));

        return code;
    }

    public async Task<ApiResponse<LoginResponse>> ExchangeCodeAsync(
        string code,
        string? ipAddress,
        CancellationToken ct)
    {
        if (!_cache.TryGetValue(code, out SsoExchangeCodeData? data) || data is null)
        {
            return new ApiResponse<LoginResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.SsoExchangeCodeInvalid, "SSO exchange code is invalid or expired.")
            };
        }

        // Remove immediately — single use
        _cache.Remove(code);

        return await _authService.LoginViaSsoAsync(data.UserId, ipAddress, ct);
    }

    private string DecryptConfiguration(string configJson)
    {
        using var doc = JsonDocument.Parse(configJson);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            return configJson;

        if (!root.TryGetProperty("clientSecret", out var secretProp))
            return configJson;

        var encryptedBase64 = secretProp.GetString();
        if (string.IsNullOrEmpty(encryptedBase64))
            return configJson;

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);
            var decryptedSecret = _encryptor.Decrypt(encryptedBytes);

            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Name == "clientSecret")
                        writer.WriteString("clientSecret", decryptedSecret);
                    else
                        prop.WriteTo(writer);
                }
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            // If decryption fails (e.g., not yet encrypted), return as-is
            return configJson;
        }
    }

    private static string GenerateUsername(string? email)
    {
        if (string.IsNullOrEmpty(email))
            return $"sso-user-{Guid.NewGuid().ToString("N")[..8]}";

        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex].ToLowerInvariant() : email.ToLowerInvariant();
    }
}

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Courier.Domain.Common;
using Courier.Domain.Encryption;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.AuthProviders;

public class AuthProvidersService
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private readonly AuditService _audit;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthProvidersService(
        CourierDbContext db,
        ICredentialEncryptor encryptor,
        AuditService audit,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _encryptor = encryptor;
        _audit = audit;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<PagedApiResponse<AuthProviderDto>> ListAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.AuthProviders
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var providers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var providerIds = providers.Select(p => p.Id).ToList();

        var linkCounts = await _db.SsoUserLinks
            .Where(l => providerIds.Contains(l.ProviderId))
            .GroupBy(l => l.ProviderId)
            .Select(g => new { ProviderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProviderId, x => x.Count, ct);

        var items = providers
            .Select(p => MapToDto(p, linkCounts.GetValueOrDefault(p.Id, 0)))
            .ToList();

        return new PagedApiResponse<AuthProviderDto>
        {
            Data = items,
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    public async Task<ApiResponse<AuthProviderDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var provider = await _db.AuthProviders.FirstOrDefaultAsync(p => p.Id == id, ct);

        if (provider is null)
        {
            return new ApiResponse<AuthProviderDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Auth provider with id '{id}' not found.")
            };
        }

        var linkedUserCount = await _db.SsoUserLinks.CountAsync(l => l.ProviderId == id, ct);

        return new ApiResponse<AuthProviderDto> { Data = MapToDto(provider, linkedUserCount) };
    }

    public async Task<ApiResponse<AuthProviderDto>> CreateAsync(
        CreateAuthProviderRequest request,
        CancellationToken ct = default)
    {
        var configJson = JsonSerializer.Serialize(request.Configuration);
        var encryptedConfigJson = EncryptClientSecret(configJson);

        var slug = await GenerateSlug(request.Name, ct);

        var provider = new AuthProvider
        {
            Id = Guid.CreateVersion7(),
            Type = request.Type,
            Name = request.Name,
            Slug = slug,
            IsEnabled = request.IsEnabled,
            Configuration = encryptedConfigJson,
            AutoProvision = request.AutoProvision,
            DefaultRole = request.DefaultRole,
            AllowLocalPassword = request.AllowLocalPassword,
            RoleMapping = request.RoleMapping.HasValue
                ? JsonSerializer.Serialize(request.RoleMapping.Value)
                : "{}",
            DisplayOrder = request.DisplayOrder,
            IconUrl = request.IconUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.AuthProviders.Add(provider);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditableEntityType.AuthProvider,
            provider.Id,
            "auth_provider_created",
            details: new { provider.Name, provider.Type, provider.Slug },
            ct: ct);

        return new ApiResponse<AuthProviderDto> { Data = MapToDto(provider, 0) };
    }

    public async Task<ApiResponse<AuthProviderDto>> UpdateAsync(
        Guid id,
        UpdateAuthProviderRequest request,
        CancellationToken ct = default)
    {
        var provider = await _db.AuthProviders.FindAsync([id], ct);

        if (provider is null)
        {
            return new ApiResponse<AuthProviderDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Auth provider with id '{id}' not found.")
            };
        }

        if (request.Type is not null)
            provider.Type = request.Type;

        if (request.Name is not null)
            provider.Name = request.Name;

        if (request.IsEnabled.HasValue)
            provider.IsEnabled = request.IsEnabled.Value;

        if (request.AutoProvision.HasValue)
            provider.AutoProvision = request.AutoProvision.Value;

        if (request.DefaultRole is not null)
            provider.DefaultRole = request.DefaultRole;

        if (request.AllowLocalPassword.HasValue)
            provider.AllowLocalPassword = request.AllowLocalPassword.Value;

        if (request.DisplayOrder.HasValue)
            provider.DisplayOrder = request.DisplayOrder.Value;

        if (request.IconUrl is not null)
            provider.IconUrl = request.IconUrl;

        if (request.RoleMapping.HasValue)
            provider.RoleMapping = JsonSerializer.Serialize(request.RoleMapping.Value);

        if (request.Configuration.HasValue)
        {
            var newConfigJson = JsonSerializer.Serialize(request.Configuration.Value);
            provider.Configuration = MergeConfiguration(provider.Configuration, newConfigJson);
        }

        provider.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditableEntityType.AuthProvider,
            id,
            "auth_provider_updated",
            ct: ct);

        var linkedUserCount = await _db.SsoUserLinks.CountAsync(l => l.ProviderId == id, ct);

        return new ApiResponse<AuthProviderDto> { Data = MapToDto(provider, linkedUserCount) };
    }

    public async Task<ApiResponse> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var provider = await _db.AuthProviders.FindAsync([id], ct);

        if (provider is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Auth provider with id '{id}' not found.")
            };
        }

        provider.IsDeleted = true;
        provider.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditableEntityType.AuthProvider,
            id,
            "auth_provider_deleted",
            ct: ct);

        return new ApiResponse();
    }

    public async Task<ApiResponse<TestConnectionResultDto>> TestConnectionAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var provider = await _db.AuthProviders.FirstOrDefaultAsync(p => p.Id == id, ct);

        if (provider is null)
        {
            return new ApiResponse<TestConnectionResultDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Auth provider with id '{id}' not found.")
            };
        }

        var decryptedConfig = DecryptClientSecret(provider.Configuration);
        using var configDoc = JsonDocument.Parse(decryptedConfig);
        var config = configDoc.RootElement;

        if (provider.Type == "oidc")
        {
            return await TestOidcConnectionAsync(config, ct);
        }
        else if (provider.Type == "saml")
        {
            return TestSamlConnection(config);
        }

        return new ApiResponse<TestConnectionResultDto>
        {
            Data = new TestConnectionResultDto
            {
                Success = false,
                Message = $"Unknown provider type '{provider.Type}'."
            }
        };
    }

    public async Task<ApiResponse<List<LoginOptionDto>>> GetLoginOptionsAsync(CancellationToken ct = default)
    {
        var providers = await _db.AuthProviders
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name)
            .Select(p => new LoginOptionDto
            {
                Id = p.Id,
                Type = p.Type,
                Name = p.Name,
                Slug = p.Slug,
                IconUrl = p.IconUrl,
            })
            .ToListAsync(ct);

        return new ApiResponse<List<LoginOptionDto>> { Data = providers };
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<string> GenerateSlug(string name, CancellationToken ct)
    {
        var baseSlug = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrEmpty(baseSlug))
            baseSlug = "provider";

        var candidate = baseSlug;
        var suffix = 2;

        while (await _db.AuthProviders.AnyAsync(p => p.Slug == candidate, ct))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private string EncryptClientSecret(string configJson)
    {
        using var doc = JsonDocument.Parse(configJson);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            return configJson;

        if (!root.TryGetProperty("clientSecret", out var secretProp))
            return configJson;

        var secretValue = secretProp.GetString();
        if (string.IsNullOrEmpty(secretValue))
            return configJson;

        var encryptedBytes = _encryptor.Encrypt(secretValue);
        var encryptedBase64 = Convert.ToBase64String(encryptedBytes);

        using var ms = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name == "clientSecret")
                    writer.WriteString("clientSecret", encryptedBase64);
                else
                    prop.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private string DecryptClientSecret(string configJson)
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

            using var ms = new System.IO.MemoryStream();
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

    private string MergeConfiguration(string existingConfigJson, string newConfigJson)
    {
        using var newDoc = JsonDocument.Parse(newConfigJson);
        var newRoot = newDoc.RootElement;

        if (newRoot.ValueKind != JsonValueKind.Object)
            return EncryptClientSecret(newConfigJson);

        // Check if the new config has a clientSecret that should be preserved or replaced
        if (!newRoot.TryGetProperty("clientSecret", out var newSecretProp))
        {
            // No clientSecret in new config — just encrypt as normal
            return EncryptClientSecret(newConfigJson);
        }

        var newSecretValue = newSecretProp.GetString();

        // If clientSecret is null, empty, or the redaction placeholder — preserve existing encrypted value
        if (string.IsNullOrEmpty(newSecretValue) || newSecretValue == "••••••••")
        {
            // Extract existing encrypted clientSecret from stored config
            using var existingDoc = JsonDocument.Parse(existingConfigJson);
            var existingRoot = existingDoc.RootElement;

            string? existingEncryptedSecret = null;
            if (existingRoot.TryGetProperty("clientSecret", out var existingSecretProp))
                existingEncryptedSecret = existingSecretProp.GetString();

            // Build merged config using new values but preserving encrypted secret
            using var ms = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                foreach (var prop in newRoot.EnumerateObject())
                {
                    if (prop.Name == "clientSecret")
                    {
                        if (existingEncryptedSecret is not null)
                            writer.WriteString("clientSecret", existingEncryptedSecret);
                    }
                    else
                    {
                        prop.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        // New non-placeholder clientSecret provided — encrypt it
        return EncryptClientSecret(newConfigJson);
    }

    private string RedactConfiguration(string configJson)
    {
        using var doc = JsonDocument.Parse(configJson);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            return configJson;

        if (!root.TryGetProperty("clientSecret", out _))
            return configJson;

        using var ms = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name == "clientSecret")
                    writer.WriteString("clientSecret", "••••••••");
                else
                    prop.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private async Task<ApiResponse<TestConnectionResultDto>> TestOidcConnectionAsync(
        JsonElement config,
        CancellationToken ct)
    {
        if (!config.TryGetProperty("authority", out var authorityProp))
        {
            return new ApiResponse<TestConnectionResultDto>
            {
                Data = new TestConnectionResultDto
                {
                    Success = false,
                    Message = "OIDC configuration is missing the 'authority' field."
                }
            };
        }

        var authority = authorityProp.GetString()?.TrimEnd('/');
        if (string.IsNullOrEmpty(authority))
        {
            return new ApiResponse<TestConnectionResultDto>
            {
                Data = new TestConnectionResultDto
                {
                    Success = false,
                    Message = "OIDC 'authority' value is empty."
                }
            };
        }

        var discoveryUrl = $"{authority}/.well-known/openid-configuration";

        try
        {
            var httpClient = _httpClientFactory.CreateClient("SsoDiscovery");
            httpClient.Timeout = TimeSpan.FromSeconds(15);

            var response = await httpClient.GetAsync(discoveryUrl, ct);

            if (!response.IsSuccessStatusCode)
            {
                return new ApiResponse<TestConnectionResultDto>
                {
                    Data = new TestConnectionResultDto
                    {
                        Success = false,
                        Message = $"Discovery endpoint returned HTTP {(int)response.StatusCode}.",
                        Details = JsonSerializer.Deserialize<JsonElement>($"{{\"url\":\"{discoveryUrl}\",\"statusCode\":{(int)response.StatusCode}}}")
                    }
                };
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            using var discoveryDoc = JsonDocument.Parse(content);
            var issuer = discoveryDoc.RootElement.TryGetProperty("issuer", out var issuerProp)
                ? issuerProp.GetString()
                : null;

            var detailsJson = $"{{\"url\":\"{discoveryUrl}\",\"issuer\":{JsonSerializer.Serialize(issuer)}}}";

            return new ApiResponse<TestConnectionResultDto>
            {
                Data = new TestConnectionResultDto
                {
                    Success = true,
                    Message = "OIDC discovery endpoint is reachable.",
                    Details = JsonSerializer.Deserialize<JsonElement>(detailsJson)
                }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<TestConnectionResultDto>
            {
                Data = new TestConnectionResultDto
                {
                    Success = false,
                    Message = $"Failed to reach OIDC discovery endpoint: {ex.Message}",
                    Details = JsonSerializer.Deserialize<JsonElement>($"{{\"url\":\"{discoveryUrl}\"}}")
                }
            };
        }
    }

    private ApiResponse<TestConnectionResultDto> TestSamlConnection(JsonElement config)
    {
        if (!config.TryGetProperty("certificate", out var certProp))
        {
            return new ApiResponse<TestConnectionResultDto>
            {
                Data = new TestConnectionResultDto
                {
                    Success = false,
                    Message = "SAML configuration is missing the 'certificate' field."
                }
            };
        }

        var certBase64 = certProp.GetString();
        if (string.IsNullOrEmpty(certBase64))
        {
            return new ApiResponse<TestConnectionResultDto>
            {
                Data = new TestConnectionResultDto
                {
                    Success = false,
                    Message = "SAML 'certificate' value is empty."
                }
            };
        }

        try
        {
            var certBytes = Convert.FromBase64String(certBase64);
            using var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(certBytes);

            var now = DateTime.UtcNow;
            var notBefore = cert.NotBefore.ToUniversalTime();
            var notAfter = cert.NotAfter.ToUniversalTime();
            var daysUntilExpiry = (notAfter - now).Days;

            if (now < notBefore)
            {
                return new ApiResponse<TestConnectionResultDto>
                {
                    Data = new TestConnectionResultDto
                    {
                        Success = false,
                        Message = $"SAML certificate is not yet valid. Valid from: {notBefore:yyyy-MM-dd}.",
                        Details = JsonSerializer.Deserialize<JsonElement>(
                            $"{{\"subject\":{JsonSerializer.Serialize(cert.Subject)},\"validFrom\":\"{notBefore:O}\",\"validTo\":\"{notAfter:O}\"}}")
                    }
                };
            }

            if (now > notAfter)
            {
                return new ApiResponse<TestConnectionResultDto>
                {
                    Data = new TestConnectionResultDto
                    {
                        Success = false,
                        Message = $"SAML certificate has expired on {notAfter:yyyy-MM-dd}.",
                        Details = JsonSerializer.Deserialize<JsonElement>(
                            $"{{\"subject\":{JsonSerializer.Serialize(cert.Subject)},\"validFrom\":\"{notBefore:O}\",\"validTo\":\"{notAfter:O}\"}}")
                    }
                };
            }

            var message = daysUntilExpiry <= 30
                ? $"SAML certificate is valid but expires in {daysUntilExpiry} day(s)."
                : "SAML certificate is valid.";

            return new ApiResponse<TestConnectionResultDto>
            {
                Data = new TestConnectionResultDto
                {
                    Success = true,
                    Message = message,
                    Details = JsonSerializer.Deserialize<JsonElement>(
                        $"{{\"subject\":{JsonSerializer.Serialize(cert.Subject)},\"validFrom\":\"{notBefore:O}\",\"validTo\":\"{notAfter:O}\",\"daysUntilExpiry\":{daysUntilExpiry}}}")
                }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<TestConnectionResultDto>
            {
                Data = new TestConnectionResultDto
                {
                    Success = false,
                    Message = $"Failed to parse SAML certificate: {ex.Message}"
                }
            };
        }
    }

    private AuthProviderDto MapToDto(AuthProvider entity, int linkedUserCount)
    {
        var redactedConfigJson = RedactConfiguration(entity.Configuration);
        var configElement = JsonSerializer.Deserialize<JsonElement>(redactedConfigJson);

        JsonElement? roleMappingElement = null;
        if (!string.IsNullOrEmpty(entity.RoleMapping) && entity.RoleMapping != "{}")
            roleMappingElement = JsonSerializer.Deserialize<JsonElement>(entity.RoleMapping);

        return new AuthProviderDto
        {
            Id = entity.Id,
            Type = entity.Type,
            Name = entity.Name,
            Slug = entity.Slug,
            IsEnabled = entity.IsEnabled,
            Configuration = configElement,
            AutoProvision = entity.AutoProvision,
            DefaultRole = entity.DefaultRole,
            AllowLocalPassword = entity.AllowLocalPassword,
            RoleMapping = roleMappingElement,
            DisplayOrder = entity.DisplayOrder,
            IconUrl = entity.IconUrl,
            LinkedUserCount = linkedUserCount,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }
}

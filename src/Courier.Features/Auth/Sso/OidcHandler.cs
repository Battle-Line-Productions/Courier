using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Courier.Features.Auth.Sso;

/// <summary>
/// Handles pure OIDC protocol logic: discovery, authorization URL building,
/// code exchange, and token validation. No database access.
/// </summary>
public class OidcHandler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public OidcHandler(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    /// <summary>
    /// Builds the authorization redirect URL for the OIDC provider.
    /// </summary>
    public async Task<string> BuildAuthorizationUrl(
        JsonElement config,
        string redirectUri,
        string state,
        string codeChallenge,
        string[]? additionalScopes,
        CancellationToken ct = default)
    {
        var authority = GetAuthority(config);
        var clientId = config.GetProperty("clientId").GetString()!;
        var configuredScopes = GetScopes(config);

        if (additionalScopes is { Length: > 0 })
            configuredScopes = [..configuredScopes, ..additionalScopes];

        var discovery = await FetchDiscoveryDocumentAsync(authority, ct);
        var authorizationEndpoint = discovery.GetProperty("authorization_endpoint").GetString()!;

        var scope = string.Join(" ", configuredScopes);
        var queryParams = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = scope,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
        };

        var query = string.Join("&", queryParams.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{authorizationEndpoint}?{query}";
    }

    /// <summary>
    /// Exchanges an authorization code for tokens and returns the extracted claims.
    /// </summary>
    public async Task<SsoClaimsPrincipal> ExchangeCodeAsync(
        JsonElement config,
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken ct)
    {
        var authority = GetAuthority(config);
        var clientId = config.GetProperty("clientId").GetString()!;

        // clientSecret is passed already decrypted by the caller
        var clientSecret = config.TryGetProperty("clientSecret", out var secretProp)
            ? secretProp.GetString() ?? string.Empty
            : string.Empty;

        var discovery = await FetchDiscoveryDocumentAsync(authority, ct);
        var tokenEndpoint = discovery.GetProperty("token_endpoint").GetString()!;

        var httpClient = _httpClientFactory.CreateClient("SsoDiscovery");

        var formValues = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code_verifier"] = codeVerifier,
        };

        using var response = await httpClient.PostAsync(
            tokenEndpoint,
            new FormUrlEncodedContent(formValues),
            ct);

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        using var tokenResponse = JsonDocument.Parse(responseBody);

        if (!tokenResponse.RootElement.TryGetProperty("id_token", out var idTokenProp))
            throw new InvalidOperationException("Token response did not contain an id_token.");

        var idToken = idTokenProp.GetString()
            ?? throw new InvalidOperationException("id_token value is null.");

        return await ValidateAndExtractClaimsAsync(idToken, config, ct);
    }

    /// <summary>
    /// Validates an ID token JWT using the provider's JWKS and extracts normalized claims.
    /// </summary>
    public async Task<SsoClaimsPrincipal> ValidateAndExtractClaimsAsync(
        string idToken,
        JsonElement config,
        CancellationToken ct)
    {
        var authority = GetAuthority(config);
        var clientId = config.GetProperty("clientId").GetString()!;

        var discovery = await FetchDiscoveryDocumentAsync(authority, ct);
        var jwksUri = discovery.GetProperty("jwks_uri").GetString()!;

        var signingKeys = await FetchJwksAsync(jwksUri, ct);

        var claimMappings = GetClaimMappings(config);

        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = authority,
            ValidAudience = clientId,
            IssuerSigningKeys = signingKeys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
        };

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(idToken, validationParameters);

        if (!result.IsValid)
            throw new SecurityTokenValidationException(
                $"ID token validation failed: {result.Exception?.Message}", result.Exception);

        var claims = result.ClaimsIdentity.Claims;

        var subjectId = claims.FirstOrDefault(c => c.Type is "sub" or
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
            ?? throw new InvalidOperationException("ID token is missing 'sub' claim.");

        var emailClaim = claimMappings.GetValueOrDefault("email", "email");
        var nameClaim = claimMappings.GetValueOrDefault("name", "name");
        var groupsClaim = claimMappings.GetValueOrDefault("groups", "groups");

        var email = claims.FirstOrDefault(c => c.Type == emailClaim)?.Value;
        var displayName = claims.FirstOrDefault(c => c.Type == nameClaim)?.Value;
        var groups = claims
            .Where(c => c.Type == groupsClaim)
            .Select(c => c.Value)
            .ToArray();

        return new SsoClaimsPrincipal(subjectId, email, displayName, groups);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string GetAuthority(JsonElement config)
    {
        var authority = config.GetProperty("authority").GetString()
            ?? throw new InvalidOperationException("OIDC config is missing 'authority'.");
        return authority.TrimEnd('/');
    }

    private static string[] GetScopes(JsonElement config)
    {
        if (config.TryGetProperty("scopes", out var scopesProp) &&
            scopesProp.ValueKind == JsonValueKind.Array)
        {
            return scopesProp.EnumerateArray()
                .Select(s => s.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToArray();
        }
        return ["openid", "profile", "email"];
    }

    private static Dictionary<string, string> GetClaimMappings(JsonElement config)
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (config.TryGetProperty("claimMappings", out var mappingsProp) &&
            mappingsProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in mappingsProp.EnumerateObject())
            {
                var value = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    mappings[prop.Name] = value;
            }
        }

        return mappings;
    }

    private async Task<JsonElement> FetchDiscoveryDocumentAsync(string authority, CancellationToken ct)
    {
        var cacheKey = $"oidc:discovery:{authority}";
        if (_cache.TryGetValue(cacheKey, out JsonElement cached))
            return cached;

        var httpClient = _httpClientFactory.CreateClient("SsoDiscovery");
        var discoveryUrl = $"{authority}/.well-known/openid-configuration";

        var response = await httpClient.GetAsync(discoveryUrl, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement.Clone();

        _cache.Set(cacheKey, root, TimeSpan.FromHours(24));
        return root;
    }

    private async Task<IEnumerable<SecurityKey>> FetchJwksAsync(string jwksUri, CancellationToken ct)
    {
        var cacheKey = $"oidc:jwks:{jwksUri}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<SecurityKey>? cachedKeys) && cachedKeys is not null)
            return cachedKeys;

        var httpClient = _httpClientFactory.CreateClient("SsoDiscovery");
        var response = await httpClient.GetAsync(jwksUri, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var jwks = new JsonWebKeySet(body);
        var keys = jwks.GetSigningKeys();

        _cache.Set(cacheKey, keys, TimeSpan.FromHours(1));
        return keys;
    }
}

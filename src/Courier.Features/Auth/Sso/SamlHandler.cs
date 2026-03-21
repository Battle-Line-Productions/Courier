using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Http;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.Extensions.Caching.Memory;

namespace Courier.Features.Auth.Sso;

/// <summary>
/// Handles pure SAML 2.0 protocol logic: building AuthnRequest redirects
/// and validating SAML responses. No database access.
/// </summary>
public class SamlHandler
{
    private readonly IMemoryCache _cache;

    public SamlHandler(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Builds the redirect URL containing a SAML AuthnRequest for the given provider config.
    /// </summary>
    public string BuildAuthnRequestUrl(
        JsonElement config,
        string relayState,
        string assertionConsumerUrl)
    {
        var entityId = config.GetProperty("entityId").GetString()
            ?? throw new InvalidOperationException("SAML config is missing 'entityId'.");

        var ssoUrl = config.GetProperty("ssoUrl").GetString()
            ?? throw new InvalidOperationException("SAML config is missing 'ssoUrl'.");

        var samlConfig = new Saml2Configuration
        {
            Issuer = entityId,
        };

        var authnRequest = new Saml2AuthnRequest(samlConfig)
        {
            Destination = new Uri(ssoUrl),
            AssertionConsumerServiceUrl = new Uri(assertionConsumerUrl),
        };

        var binding = new Saml2RedirectBinding();
        binding.RelayState = relayState;
        binding.Bind(authnRequest);

        return binding.RedirectLocation.AbsoluteUri;
    }

    /// <summary>
    /// Validates a base64-encoded SAML POST response and extracts normalized claims.
    /// </summary>
    public SsoClaimsPrincipal ValidateAndExtractClaims(
        string samlResponseBase64,
        JsonElement config,
        string assertionConsumerUrl)
    {
        var entityId = config.GetProperty("entityId").GetString()
            ?? throw new InvalidOperationException("SAML config is missing 'entityId'.");

        var certBase64 = config.GetProperty("certificate").GetString()
            ?? throw new InvalidOperationException("SAML config is missing 'certificate'.");

        var certBytes = Convert.FromBase64String(certBase64);
        var cert = X509CertificateLoader.LoadCertificate(certBytes);

        var samlConfig = new Saml2Configuration
        {
            Issuer = entityId,
        };
        samlConfig.AllowedAudienceUris.Add(entityId);
        samlConfig.SignatureValidationCertificates.Add(cert);

        // Construct a fake HttpRequest from the raw base64 POST body
        var httpRequest = new HttpRequest
        {
            Method = "POST",
            Form = new System.Collections.Specialized.NameValueCollection
            {
                ["SAMLResponse"] = samlResponseBase64,
            },
        };

        var binding = new Saml2PostBinding();
        var saml2AuthnResponse = new Saml2AuthnResponse(samlConfig);
        binding.ReadSamlResponse(httpRequest, saml2AuthnResponse);

        if (saml2AuthnResponse.Status != Saml2StatusCodes.Success)
        {
            throw new InvalidOperationException(
                $"SAML response status was not success: {saml2AuthnResponse.Status}. " +
                $"Message: {saml2AuthnResponse.StatusMessage}");
        }

        // Replay detection: store assertion ID with 5-minute TTL
        var assertionId = saml2AuthnResponse.Id?.Value;
        if (!string.IsNullOrEmpty(assertionId))
        {
            var replayCacheKey = $"saml:replay:{assertionId}";
            if (_cache.TryGetValue(replayCacheKey, out _))
                throw new InvalidOperationException("SAML assertion replay detected.");

            _cache.Set(replayCacheKey, true, TimeSpan.FromMinutes(5));
        }

        var attributeMappings = GetAttributeMappings(config);

        // Extract NameID as the subject identifier
        var nameId = saml2AuthnResponse.NameId?.Value
            ?? throw new InvalidOperationException("SAML response is missing NameID.");

        var claims = saml2AuthnResponse.ClaimsIdentity?.Claims ?? [];

        var emailAttr = attributeMappings.GetValueOrDefault("email", "email");
        var nameAttr = attributeMappings.GetValueOrDefault("name", "name");
        var groupsAttr = attributeMappings.GetValueOrDefault("groups", "groups");

        var email = claims.FirstOrDefault(c =>
            c.Type == emailAttr ||
            c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;

        var displayName = claims.FirstOrDefault(c =>
            c.Type == nameAttr ||
            c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;

        var groups = claims
            .Where(c => c.Type == groupsAttr)
            .Select(c => c.Value)
            .ToArray();

        return new SsoClaimsPrincipal(nameId, email, displayName, groups);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static Dictionary<string, string> GetAttributeMappings(JsonElement config)
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (config.TryGetProperty("attributeMappings", out var mappingsProp) &&
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
}

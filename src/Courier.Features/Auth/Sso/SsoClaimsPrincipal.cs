namespace Courier.Features.Auth.Sso;

/// <summary>
/// Normalized claims extracted from either OIDC or SAML identity providers.
/// </summary>
public record SsoClaimsPrincipal(
    string SubjectId,
    string? Email,
    string? DisplayName,
    string[] Groups);

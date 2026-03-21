using System.Security.Cryptography;
using System.Text.Json;
using Courier.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Auth.Sso;

[ApiController]
[Route("api/v1/auth/sso")]
public class SsoController : ControllerBase
{
    private readonly SsoService _ssoService;
    private readonly IDataProtector _protector;

    public SsoController(SsoService ssoService, IDataProtectionProvider dataProtectionProvider)
    {
        _ssoService = ssoService;
        _protector = dataProtectionProvider.CreateProtector("Courier.Sso.State");
    }

    [HttpGet("{providerId:guid}/login")]
    [AllowAnonymous]
    public async Task<IActionResult> InitiateLogin(Guid providerId, CancellationToken ct)
    {
        // Generate state (32 random bytes, base64url)
        var stateBytes = RandomNumberGenerator.GetBytes(32);
        var state = Convert.ToBase64String(stateBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        // Generate PKCE code_verifier and code_challenge
        var codeVerifierBytes = RandomNumberGenerator.GetBytes(32);
        var codeVerifier = Convert.ToBase64String(codeVerifierBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var challengeBytes = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(codeVerifier));
        var codeChallenge = Convert.ToBase64String(challengeBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        // Store state in encrypted cookie
        var stateCookie = new SsoStateCookie(state, codeVerifier, providerId);
        var cookieJson = JsonSerializer.Serialize(stateCookie);
        var protectedCookie = _protector.Protect(cookieJson);

        Response.Cookies.Append("sso_state", protectedCookie, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10),
            Path = "/api/v1/auth/sso",
        });

        var apiBaseUrl = _ssoService.GetApiBaseUrl(Request);
        var result = await _ssoService.InitiateLoginAsync(providerId, state, codeChallenge, apiBaseUrl, ct);

        if (result.Error is not null)
        {
            var frontendUrl = _ssoService.GetFrontendCallbackUrl();
            if (!string.IsNullOrEmpty(frontendUrl))
            {
                return Redirect($"{frontendUrl}?error={Uri.EscapeDataString(result.Error.Message)}");
            }

            return BadRequest(new ApiResponse<object> { Error = result.Error });
        }

        return Redirect(result.Data!);
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> OidcCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken ct)
    {
        var frontendUrl = _ssoService.GetFrontendCallbackUrl() ?? "/";

        // Handle IdP-side errors
        if (!string.IsNullOrEmpty(error))
        {
            var msg = errorDescription ?? error;
            return Redirect($"{frontendUrl}?error={Uri.EscapeDataString(msg)}");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return Redirect($"{frontendUrl}?error={Uri.EscapeDataString("Missing code or state parameter.")}");
        }

        // Read and delete state cookie
        var stateCookie = ReadAndDeleteStateCookie();
        if (stateCookie is null)
        {
            return Redirect($"{frontendUrl}?error={Uri.EscapeDataString("SSO state cookie not found or invalid.")}");
        }

        // Validate state matches
        if (stateCookie.State != state)
        {
            return Redirect($"{frontendUrl}?error={Uri.EscapeDataString("SSO state mismatch. Please try again.")}");
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var apiBaseUrl = _ssoService.GetApiBaseUrl(Request);

        var result = await _ssoService.HandleOidcCallbackAsync(
            code, state, stateCookie.CodeVerifier, stateCookie.ProviderId, ipAddress, apiBaseUrl, ct);

        if (result.Error is not null)
        {
            return Redirect($"{frontendUrl}?error={Uri.EscapeDataString(result.Error.Message)}");
        }

        return Redirect($"{frontendUrl}?code={Uri.EscapeDataString(result.Data!)}");
    }

    [HttpPost("callback")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SamlCallback(
        [FromForm(Name = "SAMLResponse")] string? samlResponse,
        [FromForm(Name = "RelayState")] string? relayState,
        CancellationToken ct)
    {
        var frontendUrl = _ssoService.GetFrontendCallbackUrl() ?? "/";

        if (string.IsNullOrEmpty(samlResponse))
        {
            return Redirect($"{frontendUrl}?error={Uri.EscapeDataString("Missing SAML response.")}");
        }

        // Read and delete state cookie
        var stateCookie = ReadAndDeleteStateCookie();
        if (stateCookie is null)
        {
            return Redirect($"{frontendUrl}?error={Uri.EscapeDataString("SSO state cookie not found or invalid.")}");
        }

        // Validate RelayState matches stored state
        if (!string.IsNullOrEmpty(relayState) && stateCookie.State != relayState)
        {
            return Redirect($"{frontendUrl}?error={Uri.EscapeDataString("SSO state mismatch. Please try again.")}");
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var apiBaseUrl = _ssoService.GetApiBaseUrl(Request);

        var result = await _ssoService.HandleSamlCallbackAsync(
            samlResponse, stateCookie.ProviderId, ipAddress, apiBaseUrl, ct);

        if (result.Error is not null)
        {
            return Redirect($"{frontendUrl}?error={Uri.EscapeDataString(result.Error.Message)}");
        }

        return Redirect($"{frontendUrl}?code={Uri.EscapeDataString(result.Data!)}");
    }

    [HttpPost("exchange")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Exchange(
        [FromBody] SsoExchangeRequest request,
        CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _ssoService.ExchangeCodeAsync(request.Code, ipAddress, ct);

        if (result.Error is not null)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    private SsoStateCookie? ReadAndDeleteStateCookie()
    {
        if (!Request.Cookies.TryGetValue("sso_state", out var cookieValue) || string.IsNullOrEmpty(cookieValue))
            return null;

        // Delete cookie immediately
        Response.Cookies.Delete("sso_state", new CookieOptions
        {
            Path = "/api/v1/auth/sso",
        });

        try
        {
            var unprotected = _protector.Unprotect(cookieValue);
            return JsonSerializer.Deserialize<SsoStateCookie>(unprotected);
        }
        catch
        {
            return null;
        }
    }

    private record SsoStateCookie(string State, string CodeVerifier, Guid ProviderId);
}

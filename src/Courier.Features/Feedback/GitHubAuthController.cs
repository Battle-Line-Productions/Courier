using System.Security.Claims;
using Courier.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Feedback;

[ApiController]
[Route("api/v1/auth/github")]
[Authorize]
public class GitHubAuthController : ControllerBase
{
    private readonly GitHubAuthService _authService;

    public GitHubAuthController(GitHubAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("authorize")]
    public ActionResult<ApiResponse<GitHubOAuthUrlResponse>> GetAuthUrl()
    {
        var state = Guid.NewGuid().ToString("N");
        var result = _authService.GetOAuthUrl(state);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("callback")]
    public async Task<ActionResult<ApiResponse<GitHubLinkResponse>>> Callback(
        [FromBody] GitHubCallbackRequest request,
        CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _authService.LinkAccountAsync(userId, request.Code, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.GitHubOAuthFailed => BadRequest(result),
                ErrorCodes.GitHubNotConfigured => StatusCode(503, result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpDelete("unlink")]
    public async Task<ActionResult<ApiResponse>> Unlink(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _authService.UnlinkAccountAsync(userId, ct);
        return Ok(result);
    }
}

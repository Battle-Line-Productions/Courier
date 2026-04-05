using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Callbacks;

[ApiController]
[Route("api/v1/callbacks")]
[AllowAnonymous]
public class CallbacksController : ControllerBase
{
    private readonly StepCallbackService _callbackService;

    public CallbacksController(StepCallbackService callbackService)
    {
        _callbackService = callbackService;
    }

    [HttpPost("{callbackId:guid}")]
    public async Task<IActionResult> ReceiveCallback(
        Guid callbackId,
        [FromBody] CallbackRequest request,
        CancellationToken ct)
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new { error = "Missing or invalid Authorization header." });

        var key = authHeader["Bearer ".Length..].Trim();

        var (success, error) = await _callbackService.ProcessCallbackAsync(callbackId, key, request, ct);

        return error switch
        {
            null => Ok(new { acknowledged = true }),
            "not_found" => NotFound(new { error = "Callback not found." }),
            "unauthorized" => Unauthorized(new { error = "Invalid callback key." }),
            "already_completed" => Conflict(new { error = "Callback already completed." }),
            "expired" => StatusCode(410, new { error = "Callback has expired." }),
            _ => StatusCode(500, new { error = "Unexpected error." }),
        };
    }
}

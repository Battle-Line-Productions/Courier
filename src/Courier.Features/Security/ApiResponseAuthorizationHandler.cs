using Courier.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace Courier.Features.Security;

/// <summary>
/// Returns 403 responses in the standard ApiResponse envelope format,
/// consistent with all other API error responses. Uses existing ErrorCodes.Forbidden (10008).
/// </summary>
public class ApiResponseAuthorizationHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var response = new ApiResponse
            {
                Error = new ApiError(
                    ErrorCodes.Forbidden,
                    "Forbidden",
                    "You do not have permission to perform this action."),
            };

            await context.Response.WriteAsJsonAsync(response);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}

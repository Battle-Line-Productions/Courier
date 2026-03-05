using Courier.Domain.Common;
using Courier.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Courier.Features.Auth;

public class SetupGuardMiddleware
{
    private readonly RequestDelegate _next;
    private static volatile bool _setupCompleted;

    public SetupGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, CourierDbContext db)
    {
        if (_setupCompleted)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // Allow setup, auth, health, and swagger endpoints through
        if (path.StartsWith("/api/v1/setup") ||
            path.StartsWith("/api/v1/auth") ||
            path.StartsWith("/health") ||
            path.StartsWith("/swagger"))
        {
            await _next(context);
            return;
        }

        // Check DB for setup status
        var setting = await db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "auth.setup_completed");

        if (setting?.Value == "true")
        {
            _setupCompleted = true;
            await _next(context);
            return;
        }

        // Setup not completed — return 503
        context.Response.StatusCode = 503;
        context.Response.ContentType = "application/json";

        var response = new ApiResponse
        {
            Error = ErrorMessages.Create(ErrorCodes.SetupNotCompleted, "Initial setup has not been completed. Please visit /setup to configure.")
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }

    /// <summary>
    /// Call this after setup completes to clear the cache.
    /// </summary>
    public static void InvalidateCache()
    {
        _setupCompleted = false;
    }
}

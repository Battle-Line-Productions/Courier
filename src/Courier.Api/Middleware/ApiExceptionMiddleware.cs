using System.Net;
using Courier.Domain.Common;

namespace Courier.Api.Middleware;

public class ApiExceptionMiddleware : IMiddleware
{
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(ILogger<ApiExceptionMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var reference = $"err_{Guid.NewGuid().ToString("N")[..8]}";
            _logger.LogError(ex, "Unhandled exception. Reference: {Reference}", reference);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new ApiResponse
            {
                Error = ErrorMessages.Create(
                    ErrorCodes.InternalServerError,
                    $"An unexpected error occurred. Reference: {reference}")
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}

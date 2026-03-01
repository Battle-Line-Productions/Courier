namespace Courier.Features.AzureFunctions;

public record AzureFunctionTriggerResult(
    bool Success,
    DateTime? TriggerTimeUtc,
    string? ErrorMessage = null);

public record FunctionExecutionResult(
    bool Success,
    double? DurationMs,
    string? InvocationId,
    string? OperationId);

public record AzureFunctionTraceDto
{
    public DateTime Timestamp { get; init; }
    public string Message { get; init; } = string.Empty;
    public int SeverityLevel { get; init; }
}

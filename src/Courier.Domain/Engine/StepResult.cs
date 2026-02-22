namespace Courier.Domain.Engine;

public record StepResult
{
    public bool Success { get; init; }
    public long BytesProcessed { get; init; }
    public Dictionary<string, object>? Outputs { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorStackTrace { get; init; }

    public static StepResult Ok(long bytesProcessed = 0, Dictionary<string, object>? outputs = null)
        => new() { Success = true, BytesProcessed = bytesProcessed, Outputs = outputs };

    public static StepResult Fail(string message, string? stackTrace = null)
        => new() { Success = false, ErrorMessage = message, ErrorStackTrace = stackTrace };
}

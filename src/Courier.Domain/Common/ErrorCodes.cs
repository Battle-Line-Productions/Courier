namespace Courier.Domain.Common;

public static class ErrorCodes
{
    // General (1000-1999)
    public const int ValidationFailed = 1000;
    public const int InvalidRequestFormat = 1001;
    public const int ResourceNotFound = 1030;
    public const int StateConflict = 1050;
    public const int DuplicateResource = 1052;
    public const int InternalServerError = 1099;

    // Job System (2000-2999)
    public const int JobNotEnabled = 2000;
    public const int JobHasNoSteps = 2001;
    public const int StepTypeNotRegistered = 2002;
    public const int InvalidStepOrder = 2003;
    public const int ExecutionNotFound = 2010;
}

public static class ErrorMessages
{
    private static readonly Dictionary<int, string> SystemMessages = new()
    {
        [ErrorCodes.ValidationFailed] = "Validation failed",
        [ErrorCodes.InvalidRequestFormat] = "Invalid request format",
        [ErrorCodes.ResourceNotFound] = "Resource not found",
        [ErrorCodes.StateConflict] = "State conflict",
        [ErrorCodes.DuplicateResource] = "Duplicate resource",
        [ErrorCodes.InternalServerError] = "Internal server error",
        [ErrorCodes.JobNotEnabled] = "Job not enabled",
        [ErrorCodes.JobHasNoSteps] = "Job has no steps",
        [ErrorCodes.StepTypeNotRegistered] = "Step type not registered",
        [ErrorCodes.InvalidStepOrder] = "Invalid step order",
        [ErrorCodes.ExecutionNotFound] = "Execution not found",
    };

    public static ApiError Create(int code, string message, IReadOnlyList<FieldError>? details = null)
    {
        var systemMessage = SystemMessages.GetValueOrDefault(code, "Unknown error");
        return new ApiError(code, systemMessage, message, details);
    }
}

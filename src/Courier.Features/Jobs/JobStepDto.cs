namespace Courier.Features.Jobs;

public record AddJobStepRequest
{
    public required string Name { get; init; }
    public required string TypeKey { get; init; }
    public int StepOrder { get; init; }
    public string Configuration { get; init; } = "{}";
    public int TimeoutSeconds { get; init; } = 300;
    public string? Alias { get; init; }
}

public record ReplaceJobStepsRequest
{
    public required List<StepInput> Steps { get; init; }
}

public record StepInput
{
    public required string Name { get; init; }
    public required string TypeKey { get; init; }
    public int StepOrder { get; init; }
    public string Configuration { get; init; } = "{}";
    public int TimeoutSeconds { get; init; } = 300;
    public string? Alias { get; init; }
}

public record JobStepDto
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public int StepOrder { get; init; }
    public required string Name { get; init; }
    public required string TypeKey { get; init; }
    public string Configuration { get; init; } = "{}";
    public int TimeoutSeconds { get; init; }
    public string? Alias { get; init; }
}

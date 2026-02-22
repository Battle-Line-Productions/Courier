namespace Courier.Features.Jobs;

public record AddJobStepRequest
{
    public required string Name { get; init; }
    public required string TypeKey { get; init; }
    public int StepOrder { get; init; }
    public string Configuration { get; init; } = "{}";
    public int TimeoutSeconds { get; init; } = 300;
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
}

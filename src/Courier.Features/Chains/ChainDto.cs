namespace Courier.Features.Chains;

public record JobChainDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool IsEnabled { get; init; }
    public List<JobChainMemberDto> Members { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record JobChainMemberDto
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public string JobName { get; init; } = string.Empty;
    public int ExecutionOrder { get; init; }
    public Guid? DependsOnMemberId { get; init; }
    public bool RunOnUpstreamFailure { get; init; }
}

public record ChainExecutionDto
{
    public Guid Id { get; init; }
    public Guid ChainId { get; init; }
    public string State { get; init; } = string.Empty;
    public string TriggeredBy { get; init; } = string.Empty;
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<ChainJobExecutionDto> JobExecutions { get; init; } = [];
}

public record ChainJobExecutionDto
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public string JobName { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public record CreateChainRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public record UpdateChainRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public record ReplaceChainMembersRequest
{
    public List<ChainMemberInput> Members { get; init; } = [];
}

public record ChainMemberInput
{
    public Guid JobId { get; init; }
    public int ExecutionOrder { get; init; }
    public int? DependsOnMemberIndex { get; init; }
    public bool RunOnUpstreamFailure { get; init; }
}

public record TriggerChainRequest
{
    public string TriggeredBy { get; init; } = "system";
}

namespace Courier.Features.Jobs;

public record JobDependencyDto
{
    public Guid Id { get; init; }
    public Guid UpstreamJobId { get; init; }
    public string UpstreamJobName { get; init; } = string.Empty;
    public Guid DownstreamJobId { get; init; }
    public bool RunOnFailure { get; init; }
}

public record AddJobDependencyRequest
{
    public Guid UpstreamJobId { get; init; }
    public bool RunOnFailure { get; init; }
}

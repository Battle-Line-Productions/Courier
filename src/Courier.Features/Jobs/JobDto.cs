using Courier.Features.Tags;

namespace Courier.Features.Jobs;

public record CreateJobRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}

public record UpdateJobRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}

public record JobDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int CurrentVersion { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public List<TagSummaryDto> Tags { get; init; } = [];
}

public record JobVersionDto
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public int VersionNumber { get; init; }
    public string ConfigSnapshot { get; init; } = "{}";
    public string? CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
}

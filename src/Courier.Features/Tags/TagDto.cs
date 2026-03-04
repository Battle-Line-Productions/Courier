namespace Courier.Features.Tags;

public record TagDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Color { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record TagSummaryDto
{
    public required string Name { get; init; }
    public string? Color { get; init; }
}

public record CreateTagRequest
{
    public required string Name { get; init; }
    public string? Color { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
}

public record UpdateTagRequest
{
    public required string Name { get; init; }
    public string? Color { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
}

public record TagEntityDto
{
    public Guid EntityId { get; init; }
    public required string EntityType { get; init; }
}

public record BulkTagAssignmentRequest
{
    public required List<TagAssignment> Assignments { get; init; }
}

public record TagAssignment
{
    public Guid TagId { get; init; }
    public required string EntityType { get; init; }
    public Guid EntityId { get; init; }
}

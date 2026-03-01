namespace Courier.Features.AuditLog;

public record AuditLogEntryDto
{
    public Guid Id { get; init; }
    public required string EntityType { get; init; }
    public Guid EntityId { get; init; }
    public required string Operation { get; init; }
    public required string PerformedBy { get; init; }
    public DateTime PerformedAt { get; init; }
    public string Details { get; init; } = "{}";
}

public record AuditLogFilter
{
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? Operation { get; init; }
    public string? PerformedBy { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

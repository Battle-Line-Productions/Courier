namespace Courier.Features.Dashboard;

public record DashboardSummaryDto
{
    public int TotalJobs { get; init; }
    public int TotalConnections { get; init; }
    public int TotalMonitors { get; init; }
    public int TotalPgpKeys { get; init; }
    public int TotalSshKeys { get; init; }
    public int Executions24H { get; init; }
    public int ExecutionsSucceeded24H { get; init; }
    public int ExecutionsFailed24H { get; init; }
    public int Executions7D { get; init; }
    public int ExecutionsSucceeded7D { get; init; }
    public int ExecutionsFailed7D { get; init; }
}

public record RecentExecutionDto
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public string? JobName { get; init; }
    public required string State { get; init; }
    public required string TriggeredBy { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record ExpiringKeyDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string KeyType { get; init; }
    public string? Fingerprint { get; init; }
    public DateTime ExpiresAt { get; init; }
    public int DaysUntilExpiry { get; init; }
}

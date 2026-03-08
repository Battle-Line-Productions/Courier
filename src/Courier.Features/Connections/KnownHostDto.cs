namespace Courier.Features.Connections;

public record KnownHostDto
{
    public Guid Id { get; init; }
    public Guid ConnectionId { get; init; }
    public required string KeyType { get; init; }
    public required string Fingerprint { get; init; }
    public bool IsApproved { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTime FirstSeen { get; init; }
    public DateTime LastSeen { get; init; }
}

public record CreateKnownHostRequest
{
    public required string KeyType { get; init; }
    public required string Fingerprint { get; init; }
}

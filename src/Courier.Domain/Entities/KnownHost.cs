namespace Courier.Domain.Entities;

public class KnownHost
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string KeyType { get; set; } = string.Empty;
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
    public Connection Connection { get; set; } = null!;
}

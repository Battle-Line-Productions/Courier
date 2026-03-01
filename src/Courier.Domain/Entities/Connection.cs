namespace Courier.Domain.Entities;

public class Connection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Group { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string AuthMethod { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public byte[]? PasswordEncrypted { get; set; }
    public byte[]? ClientSecretEncrypted { get; set; }
    public string? Properties { get; set; }
    public Guid? SshKeyId { get; set; }
    public string HostKeyPolicy { get; set; } = "trust_on_first_use";
    public string? StoredHostFingerprint { get; set; }
    public string? SshAlgorithms { get; set; }
    public bool PassiveMode { get; set; } = true;
    public string? TlsVersionFloor { get; set; }
    public string TlsCertPolicy { get; set; } = "system_trust";
    public string? TlsPinnedThumbprint { get; set; }
    public int ConnectTimeoutSec { get; set; } = 30;
    public int OperationTimeoutSec { get; set; } = 300;
    public int KeepaliveIntervalSec { get; set; } = 60;
    public int TransportRetries { get; set; } = 2;
    public string Status { get; set; } = "active";
    public bool FipsOverride { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<KnownHost> KnownHosts { get; set; } = [];
}

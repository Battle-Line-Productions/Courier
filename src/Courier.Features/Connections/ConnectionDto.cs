namespace Courier.Features.Connections;

public record ConnectionDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Group { get; init; }
    public required string Protocol { get; init; }
    public required string Host { get; init; }
    public int Port { get; init; }
    public required string AuthMethod { get; init; }
    public required string Username { get; init; }
    public bool HasPassword { get; init; }
    public bool HasClientSecret { get; init; }
    public Guid? SshKeyId { get; init; }
    public string? Properties { get; init; }
    public required string HostKeyPolicy { get; init; }
    public string? StoredHostFingerprint { get; init; }
    public string? SshAlgorithms { get; init; }
    public bool PassiveMode { get; init; }
    public string? TlsVersionFloor { get; init; }
    public required string TlsCertPolicy { get; init; }
    public string? TlsPinnedThumbprint { get; init; }
    public int ConnectTimeoutSec { get; init; }
    public int OperationTimeoutSec { get; init; }
    public int KeepaliveIntervalSec { get; init; }
    public int TransportRetries { get; init; }
    public required string Status { get; init; }
    public bool FipsOverride { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record CreateConnectionRequest
{
    public required string Name { get; init; }
    public string? Group { get; init; }
    public required string Protocol { get; init; }
    public required string Host { get; init; }
    public int? Port { get; init; }
    public required string AuthMethod { get; init; }
    public required string Username { get; init; }
    public string? Password { get; init; }
    public string? ClientSecret { get; init; }
    public Guid? SshKeyId { get; init; }
    public string? Properties { get; init; }
    public string? HostKeyPolicy { get; init; }
    public string? SshAlgorithms { get; init; }
    public bool? PassiveMode { get; init; }
    public string? TlsVersionFloor { get; init; }
    public string? TlsCertPolicy { get; init; }
    public string? TlsPinnedThumbprint { get; init; }
    public int? ConnectTimeoutSec { get; init; }
    public int? OperationTimeoutSec { get; init; }
    public int? KeepaliveIntervalSec { get; init; }
    public int? TransportRetries { get; init; }
    public bool? FipsOverride { get; init; }
    public string? Notes { get; init; }
}

public record UpdateConnectionRequest
{
    public required string Name { get; init; }
    public string? Group { get; init; }
    public required string Protocol { get; init; }
    public required string Host { get; init; }
    public int? Port { get; init; }
    public required string AuthMethod { get; init; }
    public required string Username { get; init; }
    public string? Password { get; init; }
    public string? ClientSecret { get; init; }
    public Guid? SshKeyId { get; init; }
    public string? Properties { get; init; }
    public string? HostKeyPolicy { get; init; }
    public string? SshAlgorithms { get; init; }
    public bool? PassiveMode { get; init; }
    public string? TlsVersionFloor { get; init; }
    public string? TlsCertPolicy { get; init; }
    public string? TlsPinnedThumbprint { get; init; }
    public int? ConnectTimeoutSec { get; init; }
    public int? OperationTimeoutSec { get; init; }
    public int? KeepaliveIntervalSec { get; init; }
    public int? TransportRetries { get; init; }
    public string? Status { get; init; }
    public bool? FipsOverride { get; init; }
    public string? Notes { get; init; }
}

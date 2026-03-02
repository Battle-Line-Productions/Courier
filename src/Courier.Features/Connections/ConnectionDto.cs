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

public record ConnectionTestDto
{
    public bool Connected { get; init; }
    public double LatencyMs { get; init; }
    public string? ServerBanner { get; init; }
    public SshAlgorithmDto? SupportedAlgorithms { get; init; }
    public TlsCertificateDto? TlsCertificate { get; init; }
    public string? Error { get; init; }
}

public record SshAlgorithmDto
{
    public IReadOnlyList<string> Cipher { get; init; } = [];
    public IReadOnlyList<string> Kex { get; init; } = [];
    public IReadOnlyList<string> Mac { get; init; } = [];
    public IReadOnlyList<string> HostKey { get; init; } = [];
}

public record TlsCertificateDto
{
    public string Subject { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public DateTime ValidFrom { get; init; }
    public DateTime ValidTo { get; init; }
    public string Thumbprint { get; init; } = string.Empty;
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

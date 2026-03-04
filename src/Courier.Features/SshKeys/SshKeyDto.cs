using Courier.Features.Tags;

namespace Courier.Features.SshKeys;

public record SshKeyDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string KeyType { get; init; }
    public string? Fingerprint { get; init; }
    public required string Status { get; init; }
    public bool HasPublicKey { get; init; }
    public bool HasPrivateKey { get; init; }
    public string? Notes { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public List<TagSummaryDto> Tags { get; init; } = [];
}

public record GenerateSshKeyRequest
{
    public required string Name { get; init; }
    public required string KeyType { get; init; }
    public string? Passphrase { get; init; }
    public string? Notes { get; init; }
}

public record ImportSshKeyRequest
{
    public required string Name { get; init; }
    public string? Passphrase { get; init; }
}

public record UpdateSshKeyRequest
{
    public string? Name { get; init; }
    public string? Notes { get; init; }
}

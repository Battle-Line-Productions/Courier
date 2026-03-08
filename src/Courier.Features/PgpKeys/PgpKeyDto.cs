using Courier.Features.Tags;

namespace Courier.Features.PgpKeys;

public record PgpKeyDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Fingerprint { get; init; }
    public string? ShortKeyId { get; init; }
    public required string Algorithm { get; init; }
    public required string KeyType { get; init; }
    public string? Purpose { get; init; }
    public required string Status { get; init; }
    public bool HasPublicKey { get; init; }
    public bool HasPrivateKey { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public Guid? SuccessorKeyId { get; init; }
    public string? SuccessorKeyName { get; init; }
    public string? CreatedBy { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public List<TagSummaryDto> Tags { get; init; } = [];
}

public record GeneratePgpKeyRequest
{
    public required string Name { get; init; }
    public required string Algorithm { get; init; }
    public string? Purpose { get; init; }
    public string? Passphrase { get; init; }
    public string? RealName { get; init; }
    public string? Email { get; init; }
    public int? ExpiresInDays { get; init; }
}

public record ImportPgpKeyRequest
{
    public required string Name { get; init; }
    public string? Purpose { get; init; }
    public string? Passphrase { get; init; }
}

public record UpdatePgpKeyRequest
{
    public string? Name { get; init; }
    public string? Purpose { get; init; }
    public string? Notes { get; init; }
}

public record SetSuccessorRequest(Guid SuccessorKeyId);

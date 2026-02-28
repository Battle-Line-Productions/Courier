namespace Courier.Domain.Entities;

public class PgpKey
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Fingerprint { get; set; }
    public string? ShortKeyId { get; set; }
    public string Algorithm { get; set; } = string.Empty;
    public string KeyType { get; set; } = "key_pair";
    public string? Purpose { get; set; }
    public string Status { get; set; } = "active";
    public string? PublicKeyData { get; set; }
    public byte[]? PrivateKeyData { get; set; }
    public byte[]? PassphraseHash { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? SuccessorKeyId { get; set; }
    public string? CreatedBy { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

namespace Courier.Domain.Entities;

public class SshKey
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyType { get; set; } = string.Empty;
    public string? PublicKeyData { get; set; }
    public byte[]? PrivateKeyData { get; set; }
    public byte[]? PassphraseHash { get; set; }
    public string? Fingerprint { get; set; }
    public string Status { get; set; } = "active";
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

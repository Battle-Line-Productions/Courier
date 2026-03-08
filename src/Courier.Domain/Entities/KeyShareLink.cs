namespace Courier.Domain.Entities;

public class KeyShareLink
{
    public Guid Id { get; set; }
    public Guid KeyId { get; set; }
    public string KeyType { get; set; } = string.Empty; // "pgp" or "ssh"
    public string TokenHash { get; set; } = string.Empty;
    public string TokenSalt { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

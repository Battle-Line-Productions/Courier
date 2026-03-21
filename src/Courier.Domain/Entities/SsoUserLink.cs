namespace Courier.Domain.Entities;

public class SsoUserLink
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProviderId { get; set; }
    public string SubjectId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime LinkedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public AuthProvider Provider { get; set; } = null!;
}

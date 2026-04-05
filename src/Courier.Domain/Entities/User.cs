namespace Courier.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string Role { get; set; } = "viewer";
    public bool IsActive { get; set; } = true;
    public bool IsSsoUser { get; set; }
    public Guid? SsoProviderId { get; set; }
    public string? SsoSubjectId { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public AuthProvider? SsoProvider { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = [];
    public List<SsoUserLink> SsoUserLinks { get; set; } = [];
}

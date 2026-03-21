namespace Courier.Domain.Entities;

public class AuthProvider
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty; // "oidc" or "saml"
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Configuration { get; set; } = "{}"; // JSONB - encrypted clientSecret inside
    public bool AutoProvision { get; set; } = true;
    public string DefaultRole { get; set; } = "viewer";
    public bool AllowLocalPassword { get; set; }
    public string RoleMapping { get; set; } = "{}"; // JSONB
    public int DisplayOrder { get; set; }
    public string? IconUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    [Obsolete("Use SsoUserLinks instead")]
    public List<User> Users { get; set; } = [];
    public List<SsoUserLink> SsoUserLinks { get; set; } = [];
}

namespace Courier.Domain.Entities;

public class AuthProvider
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Configuration { get; set; } = "{}";
    public bool AutoProvision { get; set; } = true;
    public string DefaultRole { get; set; } = "viewer";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<User> Users { get; set; } = [];
}

using System.Text.Json;

namespace Courier.Features.AuthProviders;

public record AuthProviderDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public JsonElement Configuration { get; init; }
    public bool AutoProvision { get; init; }
    public string DefaultRole { get; init; } = string.Empty;
    public bool AllowLocalPassword { get; init; }
    public JsonElement? RoleMapping { get; init; }
    public int DisplayOrder { get; init; }
    public string? IconUrl { get; init; }
    public int LinkedUserCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record LoginOptionDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? IconUrl { get; init; }
}

public record TestConnectionResultDto
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public JsonElement? Details { get; init; }
}

public record CreateAuthProviderRequest
{
    public string Type { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public JsonElement Configuration { get; init; }
    public bool AutoProvision { get; init; }
    public string DefaultRole { get; init; } = "viewer";
    public bool AllowLocalPassword { get; init; }
    public JsonElement? RoleMapping { get; init; }
    public int DisplayOrder { get; init; }
    public string? IconUrl { get; init; }
}

public record UpdateAuthProviderRequest
{
    public string? Type { get; init; }
    public string? Name { get; init; }
    public bool? IsEnabled { get; init; }
    public JsonElement? Configuration { get; init; }
    public bool? AutoProvision { get; init; }
    public string? DefaultRole { get; init; }
    public bool? AllowLocalPassword { get; init; }
    public JsonElement? RoleMapping { get; init; }
    public int? DisplayOrder { get; init; }
    public string? IconUrl { get; init; }
}

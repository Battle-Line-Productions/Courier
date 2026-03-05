namespace Courier.Features.Users;

public record UserDto
{
    public Guid Id { get; init; }
    public required string Username { get; init; }
    public string? Email { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }
    public bool IsActive { get; init; }
    public bool IsSsoUser { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record CreateUserRequest
{
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public required string Password { get; init; }
    public required string Role { get; init; }
}

public record UpdateUserRequest
{
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public required string Role { get; init; }
    public bool IsActive { get; init; }
}

public record NewPasswordRequest
{
    public required string Password { get; init; }
}

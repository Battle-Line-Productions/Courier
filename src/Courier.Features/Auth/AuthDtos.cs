namespace Courier.Features.Auth;

public record LoginRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

public record LoginResponse
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required int ExpiresIn { get; init; }
    public required UserProfileDto User { get; init; }
}

public record RefreshRequest
{
    public required string RefreshToken { get; init; }
}

public record UserProfileDto
{
    public Guid Id { get; init; }
    public required string Username { get; init; }
    public string? Email { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }
}

public record ChangePasswordRequest
{
    public required string CurrentPassword { get; init; }
    public required string NewPassword { get; init; }
}

using Courier.Features.Auth;

namespace Courier.Features.Setup;

public record SetupStatusDto
{
    public bool IsCompleted { get; init; }
}

public record InitializeSetupRequest
{
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public required string Password { get; init; }
    public required string ConfirmPassword { get; init; }
}

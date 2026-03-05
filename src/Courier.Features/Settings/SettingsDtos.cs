namespace Courier.Features.Settings;

public record AuthSettingsDto
{
    public int SessionTimeoutMinutes { get; init; }
    public int RefreshTokenDays { get; init; }
    public int PasswordMinLength { get; init; }
    public int MaxLoginAttempts { get; init; }
    public int LockoutDurationMinutes { get; init; }
}

public record UpdateAuthSettingsRequest
{
    public int SessionTimeoutMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
    public int PasswordMinLength { get; init; } = 8;
    public int MaxLoginAttempts { get; init; } = 5;
    public int LockoutDurationMinutes { get; init; } = 15;
}

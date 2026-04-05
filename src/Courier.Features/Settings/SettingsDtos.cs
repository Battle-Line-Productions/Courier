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

public record SmtpSettingsDto
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string Username { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "Courier";
    public bool IsConfigured { get; init; }
}

public record UpdateSmtpSettingsRequest
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string Username { get; init; } = string.Empty;
    public string? Password { get; init; }
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "Courier";
}

public record SmtpTestResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

using Courier.Domain.Common;
using Courier.Infrastructure.Data;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Courier.Features.Settings;

public class SettingsService
{
    private readonly CourierDbContext _db;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(CourierDbContext db, ILogger<SettingsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string> GetSettingAsync(string key, CancellationToken ct = default)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        return setting?.Value ?? string.Empty;
    }

    public async Task<ApiResponse<AuthSettingsDto>> GetAuthSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _db.SystemSettings
            .Where(s => s.Key.StartsWith("auth."))
            .ToListAsync(ct);

        var dict = settings.ToDictionary(s => s.Key, s => s.Value);

        return new ApiResponse<AuthSettingsDto>
        {
            Data = new AuthSettingsDto
            {
                SessionTimeoutMinutes = int.TryParse(dict.GetValueOrDefault("auth.session_timeout_minutes"), out var stm) ? stm : 15,
                RefreshTokenDays = int.TryParse(dict.GetValueOrDefault("auth.refresh_token_days"), out var rtd) ? rtd : 7,
                PasswordMinLength = int.TryParse(dict.GetValueOrDefault("auth.password_min_length"), out var pml) ? pml : 8,
                MaxLoginAttempts = int.TryParse(dict.GetValueOrDefault("auth.max_login_attempts"), out var mla) ? mla : 5,
                LockoutDurationMinutes = int.TryParse(dict.GetValueOrDefault("auth.lockout_duration_minutes"), out var ldm) ? ldm : 15,
            }
        };
    }

    public async Task<ApiResponse<AuthSettingsDto>> UpdateAuthSettingsAsync(
        UpdateAuthSettingsRequest request, string performedBy, CancellationToken ct = default)
    {
        var updates = new Dictionary<string, string>
        {
            ["auth.session_timeout_minutes"] = request.SessionTimeoutMinutes.ToString(),
            ["auth.refresh_token_days"] = request.RefreshTokenDays.ToString(),
            ["auth.password_min_length"] = request.PasswordMinLength.ToString(),
            ["auth.max_login_attempts"] = request.MaxLoginAttempts.ToString(),
            ["auth.lockout_duration_minutes"] = request.LockoutDurationMinutes.ToString(),
        };

        foreach (var (key, value) in updates)
        {
            var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (setting is not null)
            {
                setting.Value = value;
                setting.UpdatedAt = DateTime.UtcNow;
                setting.UpdatedBy = performedBy;
            }
        }

        await _db.SaveChangesAsync(ct);

        return await GetAuthSettingsAsync(ct);
    }

    public async Task<ApiResponse<SmtpSettingsDto>> GetSmtpSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _db.SystemSettings
            .Where(s => s.Key.StartsWith("smtp."))
            .ToListAsync(ct);

        var dict = settings.ToDictionary(s => s.Key, s => s.Value);

        var host = dict.GetValueOrDefault("smtp.host", "");
        var fromAddress = dict.GetValueOrDefault("smtp.from_address", "");

        return new ApiResponse<SmtpSettingsDto>
        {
            Data = new SmtpSettingsDto
            {
                Host = host,
                Port = int.TryParse(dict.GetValueOrDefault("smtp.port"), out var p) ? p : 587,
                UseSsl = !bool.TryParse(dict.GetValueOrDefault("smtp.use_ssl"), out var ssl) || ssl,
                Username = dict.GetValueOrDefault("smtp.username", ""),
                FromAddress = fromAddress,
                FromName = dict.GetValueOrDefault("smtp.from_name", "Courier"),
                IsConfigured = !string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(fromAddress),
            }
        };
    }

    public async Task<ApiResponse<SmtpSettingsDto>> UpdateSmtpSettingsAsync(
        UpdateSmtpSettingsRequest request, string performedBy, CancellationToken ct = default)
    {
        var updates = new Dictionary<string, string>
        {
            ["smtp.host"] = request.Host,
            ["smtp.port"] = request.Port.ToString(),
            ["smtp.use_ssl"] = request.UseSsl.ToString().ToLowerInvariant(),
            ["smtp.username"] = request.Username,
            ["smtp.from_address"] = request.FromAddress,
            ["smtp.from_name"] = request.FromName,
        };

        if (request.Password is not null)
            updates["smtp.password"] = request.Password;

        foreach (var (key, value) in updates)
        {
            var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (setting is not null)
            {
                setting.Value = value;
                setting.UpdatedAt = DateTime.UtcNow;
                setting.UpdatedBy = performedBy;
            }
        }

        await _db.SaveChangesAsync(ct);

        return await GetSmtpSettingsAsync(ct);
    }

    public async Task<ApiResponse<SmtpTestResult>> TestSmtpConnectionAsync(CancellationToken ct = default)
    {
        var settings = await _db.SystemSettings
            .Where(s => s.Key.StartsWith("smtp."))
            .ToListAsync(ct);

        var dict = settings.ToDictionary(s => s.Key, s => s.Value);

        var host = dict.GetValueOrDefault("smtp.host", "");
        var port = int.TryParse(dict.GetValueOrDefault("smtp.port"), out var p) ? p : 587;
        var useSsl = !bool.TryParse(dict.GetValueOrDefault("smtp.use_ssl"), out var ssl) || ssl;
        var username = dict.GetValueOrDefault("smtp.username", "");
        var password = dict.GetValueOrDefault("smtp.password", "");
        var fromAddress = dict.GetValueOrDefault("smtp.from_address", "");

        if (string.IsNullOrWhiteSpace(host))
            return new ApiResponse<SmtpTestResult>
            {
                Data = new SmtpTestResult { Success = false, ErrorMessage = "SMTP host is not configured." }
            };

        if (string.IsNullOrWhiteSpace(fromAddress))
            return new ApiResponse<SmtpTestResult>
            {
                Data = new SmtpTestResult { Success = false, ErrorMessage = "From address is not configured." }
            };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, useSsl, ct);

            if (!string.IsNullOrWhiteSpace(username))
                await client.AuthenticateAsync(username, password, ct);

            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("SMTP connection test succeeded for {Host}:{Port}", host, port);
            return new ApiResponse<SmtpTestResult>
            {
                Data = new SmtpTestResult { Success = true }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP connection test failed for {Host}:{Port}", host, port);
            return new ApiResponse<SmtpTestResult>
            {
                Data = new SmtpTestResult { Success = false, ErrorMessage = ex.Message }
            };
        }
    }

    public async Task<Notifications.SmtpSettings> GetSmtpSettingsForChannelAsync(CancellationToken ct = default)
    {
        var settings = await _db.SystemSettings
            .Where(s => s.Key.StartsWith("smtp."))
            .ToListAsync(ct);

        var dict = settings.ToDictionary(s => s.Key, s => s.Value);

        return new Notifications.SmtpSettings
        {
            Host = dict.GetValueOrDefault("smtp.host", ""),
            Port = int.TryParse(dict.GetValueOrDefault("smtp.port"), out var p) ? p : 587,
            UseSsl = !bool.TryParse(dict.GetValueOrDefault("smtp.use_ssl"), out var ssl) || ssl,
            Username = dict.GetValueOrDefault("smtp.username"),
            Password = dict.GetValueOrDefault("smtp.password"),
            FromAddress = dict.GetValueOrDefault("smtp.from_address", ""),
            FromName = dict.GetValueOrDefault("smtp.from_name", "Courier"),
        };
    }
}

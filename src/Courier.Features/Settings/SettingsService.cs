using Courier.Domain.Common;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Settings;

public class SettingsService
{
    private readonly CourierDbContext _db;

    public SettingsService(CourierDbContext db)
    {
        _db = db;
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
}

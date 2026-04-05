using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Features.Settings;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Auth;

public class AuthService
{
    private readonly CourierDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly SettingsService _settings;
    private readonly AuditService _audit;

    public AuthService(CourierDbContext db, JwtTokenService jwt, SettingsService settings, AuditService audit)
    {
        _db = db;
        _jwt = jwt;
        _settings = settings;
        _audit = audit;
    }

    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, ct);

        if (user is null)
        {
            return new ApiResponse<LoginResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidCredentials, "Invalid username or password.")
            };
        }

        if (!user.IsActive)
        {
            return new ApiResponse<LoginResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.AccountDisabled, "This account has been disabled.")
            };
        }

        // Check lockout
        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
        {
            return new ApiResponse<LoginResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.AccountLocked, "Account is locked. Try again later.")
            };
        }

        // Verify password
        if (user.PasswordHash is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            // Increment failed count
            user.FailedLoginCount++;

            var maxAttemptsStr = await _settings.GetSettingAsync("auth.max_login_attempts", ct);
            var maxAttempts = int.TryParse(maxAttemptsStr, out var ma) ? ma : 5;

            if (user.FailedLoginCount >= maxAttempts)
            {
                var lockoutStr = await _settings.GetSettingAsync("auth.lockout_duration_minutes", ct);
                var lockoutMinutes = int.TryParse(lockoutStr, out var lm) ? lm : 15;
                user.LockedUntil = DateTime.UtcNow.AddMinutes(lockoutMinutes);
            }

            await _db.SaveChangesAsync(ct);

            return new ApiResponse<LoginResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidCredentials, "Invalid username or password.")
            };
        }

        // Successful login — reset failed count
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Generate tokens
        var accessToken = await _jwt.GenerateAccessTokenAsync(user, ct);
        var refreshToken = JwtTokenService.GenerateRefreshToken();

        var refreshTokenDaysStr = await _settings.GetSettingAsync("auth.refresh_token_days", ct);
        var refreshTokenDays = int.TryParse(refreshTokenDaysStr, out var rtd) ? rtd : 7;

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = JwtTokenService.HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress,
        };

        _db.RefreshTokens.Add(refreshTokenEntity);
        await _db.SaveChangesAsync(ct);

        var timeoutStr = await _settings.GetSettingAsync("auth.session_timeout_minutes", ct);
        var expiresIn = int.TryParse(timeoutStr, out var mins) ? mins * 60 : 900;

        await _audit.LogAsync(AuditableEntityType.User, user.Id, "Login", details: new { IpAddress = ipAddress }, ct: ct);

        return new ApiResponse<LoginResponse>
        {
            Data = new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = expiresIn,
                User = MapToProfile(user),
            }
        };
    }

    public async Task<ApiResponse<LoginResponse>> LoginViaSsoAsync(Guid userId, string? ipAddress, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return new ApiResponse<LoginResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.UserNotFound, "User not found.")
            };
        }

        if (!user.IsActive)
        {
            return new ApiResponse<LoginResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.AccountDisabled, "This account has been disabled.")
            };
        }

        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
        {
            return new ApiResponse<LoginResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.AccountLocked, "Account is locked. Try again later.")
            };
        }

        // Successful SSO login — reset failed count
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Generate tokens
        var accessToken = await _jwt.GenerateAccessTokenAsync(user, ct);
        var refreshToken = JwtTokenService.GenerateRefreshToken();

        var refreshTokenDaysStr = await _settings.GetSettingAsync("auth.refresh_token_days", ct);
        var refreshTokenDays = int.TryParse(refreshTokenDaysStr, out var rtd) ? rtd : 7;

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = JwtTokenService.HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress,
        };

        _db.RefreshTokens.Add(refreshTokenEntity);
        await _db.SaveChangesAsync(ct);

        var timeoutStr = await _settings.GetSettingAsync("auth.session_timeout_minutes", ct);
        var expiresIn = int.TryParse(timeoutStr, out var mins) ? mins * 60 : 900;

        await _audit.LogAsync(AuditableEntityType.User, user.Id, "SsoLogin", details: new { IpAddress = ipAddress }, ct: ct);

        return new ApiResponse<LoginResponse>
        {
            Data = new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = expiresIn,
                User = MapToProfile(user),
            }
        };
    }

    public async Task<ApiResponse<LoginResponse>> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken ct = default)
    {
        var tokenHash = JwtTokenService.HashToken(refreshToken);
        var storedToken = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (storedToken is null)
        {
            return new ApiResponse<LoginResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidRefreshToken, "Invalid refresh token.")
            };
        }

        if (storedToken.RevokedAt.HasValue)
        {
            return new ApiResponse<LoginResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidRefreshToken, "Refresh token has been revoked.")
            };
        }

        if (storedToken.ExpiresAt < DateTime.UtcNow)
        {
            return new ApiResponse<LoginResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.RefreshTokenExpired, "Refresh token has expired.")
            };
        }

        if (!storedToken.User.IsActive)
        {
            return new ApiResponse<LoginResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.AccountDisabled, "This account has been disabled.")
            };
        }

        // Rotate: revoke old, create new
        storedToken.RevokedAt = DateTime.UtcNow;

        var newRefreshToken = JwtTokenService.GenerateRefreshToken();

        var refreshTokenDaysStr = await _settings.GetSettingAsync("auth.refresh_token_days", ct);
        var refreshTokenDays = int.TryParse(refreshTokenDaysStr, out var rtd) ? rtd : 7;

        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = storedToken.UserId,
            TokenHash = JwtTokenService.HashToken(newRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress,
        };

        storedToken.ReplacedById = newRefreshTokenEntity.Id;

        _db.RefreshTokens.Add(newRefreshTokenEntity);
        await _db.SaveChangesAsync(ct);

        var accessToken = await _jwt.GenerateAccessTokenAsync(storedToken.User, ct);

        var timeoutStr = await _settings.GetSettingAsync("auth.session_timeout_minutes", ct);
        var expiresIn = int.TryParse(timeoutStr, out var mins) ? mins * 60 : 900;

        return new ApiResponse<LoginResponse>
        {
            Data = new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresIn = expiresIn,
                User = MapToProfile(storedToken.User),
            }
        };
    }

    public async Task<ApiResponse> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = JwtTokenService.HashToken(refreshToken);
        var storedToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (storedToken is not null && !storedToken.RevokedAt.HasValue)
        {
            storedToken.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return new ApiResponse();
    }

    public async Task<ApiResponse<UserProfileDto>> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.SsoProvider)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return new ApiResponse<UserProfileDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.UserNotFound, "User not found.")
            };
        }

        return new ApiResponse<UserProfileDto> { Data = MapToProfile(user) };
    }

    public async Task<ApiResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.UserNotFound, "User not found.")
            };
        }

        // SSO users may be blocked from setting a local password
        if (user.IsSsoUser)
        {
            var ssoLink = await _db.SsoUserLinks
                .Include(l => l.Provider)
                .FirstOrDefaultAsync(l => l.UserId == user.Id, ct);

            if (ssoLink?.Provider is { AllowLocalPassword: false })
            {
                return new ApiResponse { Error = ErrorMessages.Create(ErrorCodes.SsoLocalPasswordNotAllowed,
                    "Your SSO provider does not allow local passwords.") };
            }
        }

        if (user.PasswordHash is null || !PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidCurrentPassword, "Current password is incorrect.")
            };
        }

        // Validate new password strength
        var minLengthStr = await _settings.GetSettingAsync("auth.password_min_length", ct);
        var minLength = int.TryParse(minLengthStr, out var ml) ? ml : 8;

        if (request.NewPassword.Length < minLength)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.WeakPassword, $"Password must be at least {minLength} characters.")
            };
        }

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Revoke all refresh tokens for this user (force re-login on other devices)
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
            token.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.User, userId, "PasswordChanged", ct: ct);

        return new ApiResponse();
    }

    private static UserProfileDto MapToProfile(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Role = user.Role,
        IsSsoUser = user.IsSsoUser,
        SsoProviderName = user.SsoProvider?.Name,
        AllowLocalPassword = !user.IsSsoUser || (user.SsoProvider?.AllowLocalPassword ?? true),
        LastLoginAt = user.LastLoginAt,
    };
}

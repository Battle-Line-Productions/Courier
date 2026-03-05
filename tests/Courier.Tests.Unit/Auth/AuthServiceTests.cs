using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Features.Auth;
using Courier.Features.AuditLog;
using Courier.Features.Settings;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Courier.Tests.Unit.Auth;

public class AuthServiceTests
{
    private static (CourierDbContext db, AuthService service) CreateService()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new CourierDbContext(options);

        // Seed required auth settings
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "auth.session_timeout_minutes", Value = "15", UpdatedAt = DateTime.UtcNow, UpdatedBy = "system" },
            new SystemSetting { Key = "auth.refresh_token_days", Value = "7", UpdatedAt = DateTime.UtcNow, UpdatedBy = "system" },
            new SystemSetting { Key = "auth.password_min_length", Value = "8", UpdatedAt = DateTime.UtcNow, UpdatedBy = "system" },
            new SystemSetting { Key = "auth.max_login_attempts", Value = "5", UpdatedAt = DateTime.UtcNow, UpdatedBy = "system" },
            new SystemSetting { Key = "auth.lockout_duration_minutes", Value = "15", UpdatedAt = DateTime.UtcNow, UpdatedBy = "system" }
        );
        db.SaveChanges();

        var settingsService = new SettingsService(db);
        var auditService = new AuditService(db);
        var jwtSettings = Options.Create(new JwtSettings
        {
            Secret = "test-secret-key-minimum-32-chars-long-for-hmac!!",
            Issuer = "test-issuer",
            Audience = "test-audience",
        });
        var jwtService = new JwtTokenService(jwtSettings, settingsService);
        var authService = new AuthService(db, jwtService, settingsService, auditService);

        return (db, authService);
    }

    private static User CreateTestUser(
        string username = "testuser",
        string password = "TestPassword123!",
        string role = "admin",
        bool isActive = true)
    {
        return new User
        {
            Id = Guid.CreateVersion7(),
            Username = username,
            DisplayName = $"Test {username}",
            PasswordHash = PasswordHasher.Hash(password),
            Role = role,
            IsActive = isActive,
            PasswordChangedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    // ── Login tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokens()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new LoginRequest { Username = "testuser", Password = "TestPassword123!" };

        // Act
        var result = await service.LoginAsync(request, "127.0.0.1");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.AccessToken.ShouldNotBeNullOrEmpty();
        result.Data.RefreshToken.ShouldNotBeNullOrEmpty();
        result.Data.ExpiresIn.ShouldBeGreaterThan(0);
        result.Data.User.Username.ShouldBe("testuser");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsInvalidCredentials()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new LoginRequest { Username = "testuser", Password = "WrongPassword!" };

        // Act
        var result = await service.LoginAsync(request, "127.0.0.1");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task LoginAsync_NonExistentUser_ReturnsInvalidCredentials()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var request = new LoginRequest { Username = "nonexistent", Password = "Whatever123!" };

        // Act
        var result = await service.LoginAsync(request, "127.0.0.1");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task LoginAsync_DisabledAccount_ReturnsAccountDisabled()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser(isActive: false);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new LoginRequest { Username = "testuser", Password = "TestPassword123!" };

        // Act
        var result = await service.LoginAsync(request, "127.0.0.1");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.AccountDisabled);
    }

    [Fact]
    public async Task LoginAsync_LockedAccount_ReturnsAccountLocked()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser();
        user.LockedUntil = DateTime.UtcNow.AddMinutes(30); // locked for 30 more minutes
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new LoginRequest { Username = "testuser", Password = "TestPassword123!" };

        // Act
        var result = await service.LoginAsync(request, "127.0.0.1");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.AccountLocked);
    }

    [Fact]
    public async Task LoginAsync_IncrementsFailedCountAndLocksAfterMaxAttempts()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new LoginRequest { Username = "testuser", Password = "WrongPassword!" };

        // Act — attempt 5 bad logins (max_login_attempts = 5)
        for (var i = 0; i < 5; i++)
        {
            await service.LoginAsync(request, "127.0.0.1");
        }

        // Assert — user should now be locked
        var updatedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        updatedUser.FailedLoginCount.ShouldBe(5);
        updatedUser.LockedUntil.ShouldNotBeNull();
        updatedUser.LockedUntil!.Value.ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_SuccessfulLogin_ResetsFailedCount()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser();
        user.FailedLoginCount = 3;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new LoginRequest { Username = "testuser", Password = "TestPassword123!" };

        // Act
        var result = await service.LoginAsync(request, "127.0.0.1");

        // Assert
        result.Success.ShouldBeTrue();

        var updatedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        updatedUser.FailedLoginCount.ShouldBe(0);
        updatedUser.LockedUntil.ShouldBeNull();
    }

    // ── Refresh tests ────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_ValidToken_ReturnsNewTokens()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // First login to get a refresh token
        var loginResult = await service.LoginAsync(
            new LoginRequest { Username = "testuser", Password = "TestPassword123!" },
            "127.0.0.1");
        loginResult.Success.ShouldBeTrue();

        var originalRefreshToken = loginResult.Data!.RefreshToken;

        // Act
        var result = await service.RefreshAsync(originalRefreshToken, "127.0.0.1");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.AccessToken.ShouldNotBeNullOrEmpty();
        result.Data.RefreshToken.ShouldNotBeNullOrEmpty();
        result.Data.RefreshToken.ShouldNotBe(originalRefreshToken); // rotated
    }

    [Fact]
    public async Task RefreshAsync_RevokedToken_ReturnsError()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Login and then revoke the token
        var loginResult = await service.LoginAsync(
            new LoginRequest { Username = "testuser", Password = "TestPassword123!" },
            "127.0.0.1");
        var refreshToken = loginResult.Data!.RefreshToken;

        // Revoke it
        var storedToken = await db.RefreshTokens.FirstAsync();
        storedToken.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Act
        var result = await service.RefreshAsync(refreshToken, "127.0.0.1");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.InvalidRefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_InvalidToken_ReturnsError()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        // Act
        var result = await service.RefreshAsync("totally-invalid-token", "127.0.0.1");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.InvalidRefreshToken);
    }

    // ── Logout tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task LogoutAsync_RevokesRefreshToken()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var loginResult = await service.LoginAsync(
            new LoginRequest { Username = "testuser", Password = "TestPassword123!" },
            "127.0.0.1");
        var refreshToken = loginResult.Data!.RefreshToken;

        // Act
        var result = await service.LogoutAsync(refreshToken);

        // Assert
        result.Success.ShouldBeTrue();

        var storedToken = await db.RefreshTokens.FirstAsync();
        storedToken.RevokedAt.ShouldNotBeNull();
    }

    // ── ChangePassword tests ─────────────────────────────────────────────

    [Fact]
    public async Task ChangePasswordAsync_CorrectCurrentPassword_Succeeds()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser(password: "OldPassword123!");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword456!",
        };

        // Act
        var result = await service.ChangePasswordAsync(user.Id, request);

        // Assert
        result.Success.ShouldBeTrue();

        var updatedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        PasswordHasher.Verify("NewPassword456!", updatedUser.PasswordHash!).ShouldBeTrue();
        PasswordHasher.Verify("OldPassword123!", updatedUser.PasswordHash!).ShouldBeFalse();
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ReturnsError()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser(password: "OldPassword123!");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "WrongPassword!",
            NewPassword = "NewPassword456!",
        };

        // Act
        var result = await service.ChangePasswordAsync(user.Id, request);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.InvalidCurrentPassword);
    }

    [Fact]
    public async Task ChangePasswordAsync_WeakNewPassword_ReturnsError()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser(password: "OldPassword123!");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "short", // less than 8 chars
        };

        // Act
        var result = await service.ChangePasswordAsync(user.Id, request);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.WeakPassword);
    }

    [Fact]
    public async Task ChangePasswordAsync_RevokesAllRefreshTokens()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser(password: "OldPassword123!");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Login to create a refresh token
        await service.LoginAsync(
            new LoginRequest { Username = "testuser", Password = "OldPassword123!" },
            "127.0.0.1");

        var activeTokensBefore = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .CountAsync();
        activeTokensBefore.ShouldBe(1);

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword456!",
        };

        // Act
        await service.ChangePasswordAsync(user.Id, request);

        // Assert — all refresh tokens should be revoked
        var activeTokensAfter = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .CountAsync();
        activeTokensAfter.ShouldBe(0);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ExistingUser_ReturnsProfile()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetCurrentUserAsync(user.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Username.ShouldBe("testuser");
    }

    [Fact]
    public async Task GetCurrentUserAsync_NonExistentUser_ReturnsError()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        // Act
        var result = await service.GetCurrentUserAsync(Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.UserNotFound);
    }
}

using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Features.Auth;
using Courier.Features.AuditLog;
using Courier.Features.Setup;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Setup;

public class SetupServiceTests
{
    private static (CourierDbContext db, SetupService service) CreateService(bool seedSetupSetting = true)
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new CourierDbContext(options);

        if (seedSetupSetting)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = "auth.setup_completed",
                Value = "false",
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = "system",
            });
            db.SaveChanges();
        }

        var audit = new AuditService(db);
        var service = new SetupService(db, audit);
        return (db, service);
    }

    [Fact]
    public async Task GetStatusAsync_NoSettingExists_ReturnsFalse()
    {
        // Arrange — no setup setting seeded
        var (db, service) = CreateService(seedSetupSetting: false);
        using var _ = db;

        // Act
        var result = await service.GetStatusAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_SettingIsFalse_ReturnsFalse()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        // Act
        var result = await service.GetStatusAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_SettingIsTrue_ReturnsTrue()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var setting = await db.SystemSettings.FirstAsync(s => s.Key == "auth.setup_completed");
        setting.Value = "true";
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetStatusAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_CreatesAdminUserAndSetsSetupCompleted()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var request = new InitializeSetupRequest
        {
            Username = "admin",
            DisplayName = "Admin User",
            Email = "admin@example.com",
            Password = "SecurePassword123!",
            ConfirmPassword = "SecurePassword123!",
        };

        // Act
        var result = await service.InitializeAsync(request);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Username.ShouldBe("admin");
        result.Data.DisplayName.ShouldBe("Admin User");
        result.Data.Email.ShouldBe("admin@example.com");
        result.Data.Role.ShouldBe("admin");

        // Verify user was persisted
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        user.ShouldNotBeNull();
        user.Role.ShouldBe("admin");
        user.IsActive.ShouldBeTrue();
        user.PasswordHash.ShouldNotBeNullOrEmpty();

        // Verify password was hashed (not stored as plaintext)
        user.PasswordHash.ShouldStartWith("$argon2id$");
        PasswordHasher.Verify("SecurePassword123!", user.PasswordHash).ShouldBeTrue();

        // Verify setup_completed was set to true
        var setting = await db.SystemSettings.FirstAsync(s => s.Key == "auth.setup_completed");
        setting.Value.ShouldBe("true");
    }

    [Fact]
    public async Task InitializeAsync_WhenSetupAlreadyCompleted_ReturnsError()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var setting = await db.SystemSettings.FirstAsync(s => s.Key == "auth.setup_completed");
        setting.Value = "true";
        await db.SaveChangesAsync();

        var request = new InitializeSetupRequest
        {
            Username = "admin",
            DisplayName = "Admin User",
            Password = "SecurePassword123!",
            ConfirmPassword = "SecurePassword123!",
        };

        // Act
        var result = await service.InitializeAsync(request);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(ErrorCodes.SetupAlreadyCompleted);
    }

    [Fact]
    public async Task InitializeAsync_CreatesUserWithCorrectRoleAndHashedPassword()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var request = new InitializeSetupRequest
        {
            Username = "myadmin",
            DisplayName = "My Admin",
            Password = "TestPassword!",
            ConfirmPassword = "TestPassword!",
        };

        // Act
        var result = await service.InitializeAsync(request);

        // Assert
        result.Success.ShouldBeTrue();

        var user = await db.Users.FirstAsync(u => u.Username == "myadmin");
        user.Role.ShouldBe("admin");
        user.PasswordHash.ShouldNotBeNull();
        PasswordHasher.Verify("TestPassword!", user.PasswordHash!).ShouldBeTrue();
        PasswordHasher.Verify("WrongPassword", user.PasswordHash!).ShouldBeFalse();
    }
}

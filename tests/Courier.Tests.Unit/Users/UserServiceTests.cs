using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Features.Auth;
using Courier.Features.AuditLog;
using Courier.Features.Settings;
using Courier.Features.Users;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Courier.Tests.Unit.Users;

public class UserServiceTests
{
    private static (CourierDbContext db, UserService service) CreateService()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new CourierDbContext(options);

        // Seed required auth settings
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "auth.password_min_length", Value = "8", UpdatedAt = DateTime.UtcNow, UpdatedBy = "system" },
            new SystemSetting { Key = "auth.max_login_attempts", Value = "5", UpdatedAt = DateTime.UtcNow, UpdatedBy = "system" },
            new SystemSetting { Key = "auth.lockout_duration_minutes", Value = "15", UpdatedAt = DateTime.UtcNow, UpdatedBy = "system" }
        );
        db.SaveChanges();

        var audit = new AuditService(db);
        var settings = new SettingsService(db, NullLogger<SettingsService>.Instance);
        var service = new UserService(db, audit, settings);
        return (db, service);
    }

    private static User CreateTestUser(string username = "testuser", string role = "viewer", bool isActive = true)
    {
        return new User
        {
            Id = Guid.CreateVersion7(),
            Username = username,
            DisplayName = $"Test {username}",
            PasswordHash = PasswordHasher.Hash("TestPassword123!"),
            Role = role,
            IsActive = isActive,
            PasswordChangedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task ListAsync_ReturnsUsers()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        db.Users.Add(CreateTestUser("alice"));
        db.Users.Add(CreateTestUser("bob"));
        await db.SaveChangesAsync();

        // Act
        var result = await service.ListAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.Count.ShouldBe(2);
        result.Pagination.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_WithSearch_FiltersResults()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        db.Users.Add(CreateTestUser("alice"));
        db.Users.Add(CreateTestUser("bob"));
        await db.SaveChangesAsync();

        // Act
        var result = await service.ListAsync(search: "alice");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.Count.ShouldBe(1);
        result.Data[0].Username.ShouldBe("alice");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingUser_ReturnsUser()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser("testuser");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(user.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Username.ShouldBe("testuser");
        result.Data.Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentUser_ReturnsError()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.UserNotFound);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesUserWithHashedPassword()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var request = new CreateUserRequest
        {
            Username = "newuser",
            DisplayName = "New User",
            Email = "new@example.com",
            Password = "SecurePassword123!",
            Role = "operator",
        };

        // Act
        var result = await service.CreateAsync(request, "admin");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Username.ShouldBe("newuser");
        result.Data.DisplayName.ShouldBe("New User");
        result.Data.Role.ShouldBe("operator");
        result.Data.IsActive.ShouldBeTrue();

        // Verify password was hashed
        var user = await db.Users.FirstAsync(u => u.Username == "newuser");
        user.PasswordHash.ShouldNotBeNull();
        user.PasswordHash.ShouldStartWith("$argon2id$");
        PasswordHasher.Verify("SecurePassword123!", user.PasswordHash).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateAsync_DuplicateUsername_ReturnsError()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        db.Users.Add(CreateTestUser("existinguser"));
        await db.SaveChangesAsync();

        var request = new CreateUserRequest
        {
            Username = "existinguser",
            DisplayName = "Duplicate",
            Password = "SecurePassword123!",
            Role = "viewer",
        };

        // Act
        var result = await service.CreateAsync(request, "admin");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.DuplicateUsername);
    }

    [Fact]
    public async Task CreateAsync_WeakPassword_ReturnsError()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var request = new CreateUserRequest
        {
            Username = "newuser",
            DisplayName = "New User",
            Password = "short", // less than 8 chars
            Role = "viewer",
        };

        // Act
        var result = await service.CreateAsync(request, "admin");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.WeakPassword);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesUserFields()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser("updateme", role: "viewer");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new UpdateUserRequest
        {
            DisplayName = "Updated Name",
            Email = "updated@example.com",
            Role = "operator",
            IsActive = true,
        };

        // Act
        var result = await service.UpdateAsync(user.Id, request, Guid.NewGuid());

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.DisplayName.ShouldBe("Updated Name");
        result.Data.Email.ShouldBe("updated@example.com");
        result.Data.Role.ShouldBe("operator");
    }

    [Fact]
    public async Task DeleteAsync_ExistingUser_SoftDeletesUser()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser("deleteme", role: "viewer");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var performedById = Guid.NewGuid(); // different from user being deleted

        // Act
        var result = await service.DeleteAsync(user.Id, performedById);

        // Assert
        result.Success.ShouldBeTrue();

        // Verify soft delete — need to bypass global query filter
        var deletedUser = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        deletedUser.ShouldNotBeNull();
        deletedUser!.IsDeleted.ShouldBeTrue();
        deletedUser.DeletedAt.ShouldNotBeNull();
        deletedUser.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_Self_ReturnsCannotDeleteSelfError()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser("selfdelete");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act — try to delete self
        var result = await service.DeleteAsync(user.Id, user.Id);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.CannotDeleteSelf);
    }

    [Fact]
    public async Task UpdateAsync_DemoteLastAdmin_ReturnsError()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var admin = CreateTestUser("onlyadmin", role: "admin");
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var request = new UpdateUserRequest
        {
            DisplayName = admin.DisplayName,
            Role = "viewer", // demoting from admin
            IsActive = true,
        };

        // Act
        var result = await service.UpdateAsync(admin.Id, request, Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.CannotDemoteLastAdmin);
    }

    [Fact]
    public async Task UpdateAsync_DemoteAdminWhenOthersExist_Succeeds()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var admin1 = CreateTestUser("admin1", role: "admin");
        var admin2 = CreateTestUser("admin2", role: "admin");
        db.Users.AddRange(admin1, admin2);
        await db.SaveChangesAsync();

        var request = new UpdateUserRequest
        {
            DisplayName = admin1.DisplayName,
            Role = "viewer",
            IsActive = true,
        };

        // Act
        var result = await service.UpdateAsync(admin1.Id, request, admin2.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Role.ShouldBe("viewer");
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidRequest_ChangesPasswordHash()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser("resetme");
        var originalHash = user.PasswordHash;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new NewPasswordRequest { Password = "NewSecurePassword123!" };

        // Act
        var result = await service.ResetPasswordAsync(user.Id, request, "admin");

        // Assert
        result.Success.ShouldBeTrue();

        var updatedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        updatedUser.PasswordHash.ShouldNotBe(originalHash);
        PasswordHasher.Verify("NewSecurePassword123!", updatedUser.PasswordHash!).ShouldBeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_WeakPassword_ReturnsError()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var user = CreateTestUser("resetme");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new NewPasswordRequest { Password = "short" };

        // Act
        var result = await service.ResetPasswordAsync(user.Id, request, "admin");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.WeakPassword);
    }

    [Fact]
    public async Task ResetPasswordAsync_NonExistentUser_ReturnsError()
    {
        // Arrange
        var (db, service) = CreateService();
        using var _ = db;

        var request = new NewPasswordRequest { Password = "SecurePassword123!" };

        // Act
        var result = await service.ResetPasswordAsync(Guid.NewGuid(), request, "admin");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.UserNotFound);
    }
}

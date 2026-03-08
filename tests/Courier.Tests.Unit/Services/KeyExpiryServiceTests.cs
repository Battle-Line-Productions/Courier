using Courier.Domain.Entities;
using Courier.Features.AuditLog;
using Courier.Features.Notifications;
using Courier.Infrastructure.Data;
using Courier.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Services;

public class KeyExpiryServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    private static (IServiceScopeFactory scopeFactory, CourierDbContext db) CreateScopeFactory()
    {
        var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var dispatcher = Substitute.For<NotificationDispatcher>(
            db,
            Enumerable.Empty<INotificationChannel>(),
            NullLogger<NotificationDispatcher>.Instance);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(CourierDbContext)).Returns(db);
        serviceProvider.GetService(typeof(AuditService)).Returns(audit);
        serviceProvider.GetService(typeof(NotificationDispatcher)).Returns(dispatcher);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        return (scopeFactory, db);
    }

    private static KeyExpiryService CreateService(IServiceScopeFactory scopeFactory)
    {
        return new KeyExpiryService(scopeFactory, NullLogger<KeyExpiryService>.Instance);
    }

    private static PgpKey CreatePgpKey(string status = "active", DateTime? expiresAt = null, bool isDeleted = false)
    {
        return new PgpKey
        {
            Id = Guid.NewGuid(),
            Name = $"TestKey-{Guid.NewGuid():N}",
            Algorithm = "RSA",
            Status = status,
            ExpiresAt = expiresAt,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow.AddDays(-90),
            UpdatedAt = DateTime.UtcNow.AddDays(-90),
        };
    }

    [Fact]
    public async Task CheckKeyExpirations_ActiveKeyWithinWarningWindow_StatusChangesToExpiring()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var key = CreatePgpKey(status: "active", expiresAt: DateTime.UtcNow.AddDays(15));
        db.PgpKeys.Add(key);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act — run one iteration then cancel
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updatedKey = await db.PgpKeys.IgnoreQueryFilters().FirstAsync(k => k.Id == key.Id);
        updatedKey.Status.ShouldBe("expiring");
    }

    [Fact]
    public async Task CheckKeyExpirations_ActiveKeyPastExpiry_StatusChangesToRetired()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var key = CreatePgpKey(status: "active", expiresAt: DateTime.UtcNow.AddDays(-1));
        db.PgpKeys.Add(key);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updatedKey = await db.PgpKeys.IgnoreQueryFilters().FirstAsync(k => k.Id == key.Id);
        updatedKey.Status.ShouldBe("retired");
    }

    [Fact]
    public async Task CheckKeyExpirations_ExpiringKeyPastExpiry_StatusChangesToRetired()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var key = CreatePgpKey(status: "expiring", expiresAt: DateTime.UtcNow.AddDays(-1));
        db.PgpKeys.Add(key);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updatedKey = await db.PgpKeys.IgnoreQueryFilters().FirstAsync(k => k.Id == key.Id);
        updatedKey.Status.ShouldBe("retired");
    }

    [Fact]
    public async Task CheckKeyExpirations_AlreadyRetiredKey_NotReprocessed()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var key = CreatePgpKey(status: "retired", expiresAt: DateTime.UtcNow.AddDays(-10));
        var originalUpdatedAt = key.UpdatedAt;
        db.PgpKeys.Add(key);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updatedKey = await db.PgpKeys.IgnoreQueryFilters().FirstAsync(k => k.Id == key.Id);
        updatedKey.Status.ShouldBe("retired");
        updatedKey.UpdatedAt.ShouldBe(originalUpdatedAt);
    }

    [Fact]
    public async Task CheckKeyExpirations_RevokedKey_NotAffected()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var key = CreatePgpKey(status: "revoked", expiresAt: DateTime.UtcNow.AddDays(-5));
        db.PgpKeys.Add(key);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updatedKey = await db.PgpKeys.IgnoreQueryFilters().FirstAsync(k => k.Id == key.Id);
        updatedKey.Status.ShouldBe("revoked");
    }

    [Fact]
    public async Task CheckKeyExpirations_MultipleKeys_AllProcessedInSingleScan()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var expiringKey1 = CreatePgpKey(status: "active", expiresAt: DateTime.UtcNow.AddDays(10));
        var expiringKey2 = CreatePgpKey(status: "active", expiresAt: DateTime.UtcNow.AddDays(5));
        var expiredKey = CreatePgpKey(status: "active", expiresAt: DateTime.UtcNow.AddDays(-2));
        db.PgpKeys.AddRange(expiringKey1, expiringKey2, expiredKey);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var keys = await db.PgpKeys.IgnoreQueryFilters().ToListAsync();
        keys.First(k => k.Id == expiringKey1.Id).Status.ShouldBe("expiring");
        keys.First(k => k.Id == expiringKey2.Id).Status.ShouldBe("expiring");
        keys.First(k => k.Id == expiredKey.Id).Status.ShouldBe("retired");
    }

    [Fact]
    public async Task CheckKeyExpirations_KeyWithNoExpiryDate_Ignored()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var key = CreatePgpKey(status: "active", expiresAt: null);
        db.PgpKeys.Add(key);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updatedKey = await db.PgpKeys.IgnoreQueryFilters().FirstAsync(k => k.Id == key.Id);
        updatedKey.Status.ShouldBe("active");
    }

    [Fact]
    public async Task CheckKeyExpirations_StatusChange_UpdatedAtIsSet()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var key = CreatePgpKey(status: "active", expiresAt: DateTime.UtcNow.AddDays(10));
        var originalUpdatedAt = key.UpdatedAt;
        db.PgpKeys.Add(key);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updatedKey = await db.PgpKeys.IgnoreQueryFilters().FirstAsync(k => k.Id == key.Id);
        updatedKey.UpdatedAt.ShouldBeGreaterThan(originalUpdatedAt);
    }

    [Fact]
    public async Task CheckKeyExpirations_OnlyActiveKeysTransitionToExpiring()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        // Expiring key within warning window should NOT transition to "expiring" again
        var alreadyExpiring = CreatePgpKey(status: "expiring", expiresAt: DateTime.UtcNow.AddDays(10));
        var retiredKey = CreatePgpKey(status: "retired", expiresAt: DateTime.UtcNow.AddDays(10));
        var revokedKey = CreatePgpKey(status: "revoked", expiresAt: DateTime.UtcNow.AddDays(10));
        db.PgpKeys.AddRange(alreadyExpiring, retiredKey, revokedKey);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var keys = await db.PgpKeys.IgnoreQueryFilters().ToListAsync();
        keys.First(k => k.Id == alreadyExpiring.Id).Status.ShouldBe("expiring");
        keys.First(k => k.Id == retiredKey.Id).Status.ShouldBe("retired");
        keys.First(k => k.Id == revokedKey.Id).Status.ShouldBe("revoked");
    }

    [Fact]
    public async Task CheckKeyExpirations_DeletedKey_Ignored()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var key = CreatePgpKey(status: "active", expiresAt: DateTime.UtcNow.AddDays(10), isDeleted: true);
        db.PgpKeys.Add(key);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updatedKey = await db.PgpKeys.IgnoreQueryFilters().FirstAsync(k => k.Id == key.Id);
        updatedKey.Status.ShouldBe("active");
    }
}

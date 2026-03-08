using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Features.AuditLog;
using Courier.Features.Connections;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Connections;

public class KnownHostServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    private static Connection CreateConnection(CourierDbContext db)
    {
        var connection = new Connection
        {
            Id = Guid.CreateVersion7(),
            Name = "test-connection",
            Protocol = "sftp",
            Host = "sftp.example.com",
            Port = 22,
            AuthMethod = "password",
            Username = "testuser",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Connections.Add(connection);
        db.SaveChanges();
        return connection;
    }

    private static KnownHostService CreateService(CourierDbContext db)
    {
        var audit = new AuditService(db);
        return new KnownHostService(db, audit);
    }

    // --- GetByConnectionIdAsync ---

    [Fact]
    public async Task GetByConnectionIdAsync_ConnectionExists_ReturnsKnownHosts()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var connection = CreateConnection(db);
        var service = CreateService(db);

        await service.CreateAsync(connection.Id, new CreateKnownHostRequest
        {
            KeyType = "ssh-rsa",
            Fingerprint = "SHA256:abc123"
        });

        // Act
        var result = await service.GetByConnectionIdAsync(connection.Id);

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.Count.ShouldBe(1);
        result.Data[0].Fingerprint.ShouldBe("SHA256:abc123");
        result.Error.ShouldBeNull();
    }

    [Fact]
    public async Task GetByConnectionIdAsync_ConnectionNotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        // Act
        var result = await service.GetByConnectionIdAsync(Guid.NewGuid());

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }

    // --- GetByIdAsync ---

    [Fact]
    public async Task GetByIdAsync_Exists_ReturnsKnownHost()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var connection = CreateConnection(db);
        var service = CreateService(db);

        var created = await service.CreateAsync(connection.Id, new CreateKnownHostRequest
        {
            KeyType = "ssh-ed25519",
            Fingerprint = "SHA256:xyz789"
        });

        // Act
        var result = await service.GetByIdAsync(created.Data!.Id);

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.Fingerprint.ShouldBe("SHA256:xyz789");
        result.Data.KeyType.ShouldBe("ssh-ed25519");
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.KnownHostNotFound);
    }

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesKnownHost()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var connection = CreateConnection(db);
        var service = CreateService(db);

        // Act
        var result = await service.CreateAsync(connection.Id, new CreateKnownHostRequest
        {
            KeyType = "ssh-rsa",
            Fingerprint = "SHA256:newfingerprint"
        });

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.ConnectionId.ShouldBe(connection.Id);
        result.Data.KeyType.ShouldBe("ssh-rsa");
        result.Data.Fingerprint.ShouldBe("SHA256:newfingerprint");
        result.Data.IsApproved.ShouldBeFalse();
        result.Data.ApprovedBy.ShouldBeNull();
        result.Data.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_ConnectionNotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        // Act
        var result = await service.CreateAsync(Guid.NewGuid(), new CreateKnownHostRequest
        {
            KeyType = "ssh-rsa",
            Fingerprint = "SHA256:test"
        });

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }

    [Fact]
    public async Task CreateAsync_DuplicateFingerprint_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var connection = CreateConnection(db);
        var service = CreateService(db);

        await service.CreateAsync(connection.Id, new CreateKnownHostRequest
        {
            KeyType = "ssh-rsa",
            Fingerprint = "SHA256:duplicate"
        });

        // Act
        var result = await service.CreateAsync(connection.Id, new CreateKnownHostRequest
        {
            KeyType = "ssh-rsa",
            Fingerprint = "SHA256:duplicate"
        });

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.DuplicateKnownHostFingerprint);
    }

    // --- DeleteAsync ---

    [Fact]
    public async Task DeleteAsync_Exists_RemovesKnownHost()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var connection = CreateConnection(db);
        var service = CreateService(db);

        var created = await service.CreateAsync(connection.Id, new CreateKnownHostRequest
        {
            KeyType = "ssh-rsa",
            Fingerprint = "SHA256:todelete"
        });

        // Act
        var result = await service.DeleteAsync(created.Data!.Id);

        // Assert
        result.Error.ShouldBeNull();
        var remaining = await db.KnownHosts.CountAsync();
        remaining.ShouldBe(0);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        // Act
        var result = await service.DeleteAsync(Guid.NewGuid());

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.KnownHostNotFound);
    }

    // --- ApproveAsync ---

    [Fact]
    public async Task ApproveAsync_ValidHost_SetsApprovedBy()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var connection = CreateConnection(db);
        var service = CreateService(db);

        var created = await service.CreateAsync(connection.Id, new CreateKnownHostRequest
        {
            KeyType = "ssh-rsa",
            Fingerprint = "SHA256:toapprove"
        });

        // Act
        var result = await service.ApproveAsync(created.Data!.Id, "admin@example.com");

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.IsApproved.ShouldBeTrue();
        result.Data.ApprovedBy.ShouldBe("admin@example.com");
    }

    [Fact]
    public async Task ApproveAsync_AlreadyApproved_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var connection = CreateConnection(db);
        var service = CreateService(db);

        var created = await service.CreateAsync(connection.Id, new CreateKnownHostRequest
        {
            KeyType = "ssh-rsa",
            Fingerprint = "SHA256:alreadyapproved"
        });
        await service.ApproveAsync(created.Data!.Id, "admin@example.com");

        // Act
        var result = await service.ApproveAsync(created.Data.Id, "other@example.com");

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.KnownHostAlreadyApproved);
    }

    [Fact]
    public async Task ApproveAsync_NotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        // Act
        var result = await service.ApproveAsync(Guid.NewGuid(), "admin@example.com");

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.KnownHostNotFound);
    }
}

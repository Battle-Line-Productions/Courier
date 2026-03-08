using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Features.Keys;
using Courier.Features.Settings;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Keys;

public class KeyShareServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    private static void EnableShareLinks(CourierDbContext db)
    {
        db.SystemSettings.Add(new SystemSetting
        {
            Key = "security.public_key_share_links_enabled",
            Value = "true",
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "system",
        });
        db.SaveChanges();
    }

    private static void SetMaxShareLinkDays(CourierDbContext db, int days)
    {
        db.SystemSettings.Add(new SystemSetting
        {
            Key = "security.max_share_link_days",
            Value = days.ToString(),
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "system",
        });
        db.SaveChanges();
    }

    private static PgpKey SeedPgpKey(CourierDbContext db, string? publicKeyData = "-----BEGIN PGP PUBLIC KEY-----")
    {
        var key = new PgpKey
        {
            Id = Guid.CreateVersion7(),
            Name = "test-pgp-key",
            Algorithm = "rsa",
            KeyType = "key_pair",
            PublicKeyData = publicKeyData,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.PgpKeys.Add(key);
        db.SaveChanges();
        return key;
    }

    private static SshKey SeedSshKey(CourierDbContext db, string? publicKeyData = "ssh-rsa AAAA...")
    {
        var key = new SshKey
        {
            Id = Guid.CreateVersion7(),
            Name = "test-ssh-key",
            KeyType = "rsa",
            PublicKeyData = publicKeyData,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.SshKeys.Add(key);
        db.SaveChanges();
        return key;
    }

    private static KeyShareService CreateService(CourierDbContext db)
    {
        var settings = new SettingsService(db);
        return new KeyShareService(db, settings);
    }

    // --- CreateShareLinkAsync ---

    [Fact]
    public async Task CreateShareLinkAsync_ValidPgpKey_ReturnsToken()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        EnableShareLinks(db);
        var pgpKey = SeedPgpKey(db);
        var service = CreateService(db);

        // Act
        var result = await service.CreateShareLinkAsync(pgpKey.Id, "pgp", "admin", expiryDays: 7, CancellationToken.None);

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.Token.ShouldNotBeNullOrEmpty();
        result.Data.LinkId.ShouldNotBe(Guid.Empty);
        result.Data.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public async Task CreateShareLinkAsync_ValidSshKey_ReturnsToken()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        EnableShareLinks(db);
        var sshKey = SeedSshKey(db);
        var service = CreateService(db);

        // Act
        var result = await service.CreateShareLinkAsync(sshKey.Id, "ssh", "admin", expiryDays: 7, CancellationToken.None);

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.Token.ShouldNotBeNullOrEmpty();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public async Task CreateShareLinkAsync_FeatureDisabled_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        // Do NOT enable share links
        var pgpKey = SeedPgpKey(db);
        var service = CreateService(db);

        // Act
        var result = await service.CreateShareLinkAsync(pgpKey.Id, "pgp", "admin", expiryDays: 7, CancellationToken.None);

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.ShareLinksDisabled);
    }

    [Fact]
    public async Task CreateShareLinkAsync_KeyNotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        EnableShareLinks(db);
        var service = CreateService(db);

        // Act
        var result = await service.CreateShareLinkAsync(Guid.NewGuid(), "pgp", "admin", expiryDays: 7, CancellationToken.None);

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.KeyNotFound);
    }

    [Fact]
    public async Task CreateShareLinkAsync_ExpiryDaysCappedAtMax()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        EnableShareLinks(db);
        SetMaxShareLinkDays(db, 10);
        var pgpKey = SeedPgpKey(db);
        var service = CreateService(db);

        // Act
        var result = await service.CreateShareLinkAsync(pgpKey.Id, "pgp", "admin", expiryDays: 30, CancellationToken.None);

        // Assert
        result.Data.ShouldNotBeNull();
        // ExpiresAt should be approximately 10 days from now (capped), not 30
        var daysDiff = (result.Data.ExpiresAt - DateTime.UtcNow).TotalDays;
        daysDiff.ShouldBeLessThanOrEqualTo(11); // small margin for test timing
        daysDiff.ShouldBeGreaterThan(9);
    }

    [Fact]
    public async Task CreateShareLinkAsync_TokensAreUnique()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        EnableShareLinks(db);
        var pgpKey = SeedPgpKey(db);
        var service = CreateService(db);

        // Act
        var result1 = await service.CreateShareLinkAsync(pgpKey.Id, "pgp", "admin", expiryDays: 7, CancellationToken.None);
        var result2 = await service.CreateShareLinkAsync(pgpKey.Id, "pgp", "admin", expiryDays: 7, CancellationToken.None);

        // Assert
        result1.Data!.Token.ShouldNotBe(result2.Data!.Token);
    }

    // --- RevokeShareLinkAsync ---

    [Fact]
    public async Task RevokeShareLinkAsync_ValidLink_SetsRevokedAt()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        EnableShareLinks(db);
        var pgpKey = SeedPgpKey(db);
        var service = CreateService(db);
        var created = await service.CreateShareLinkAsync(pgpKey.Id, "pgp", "admin", expiryDays: 7, CancellationToken.None);

        // Act
        var result = await service.RevokeShareLinkAsync(created.Data!.LinkId, CancellationToken.None);

        // Assert
        result.Error.ShouldBeNull();
        var link = await db.KeyShareLinks.FindAsync(created.Data.LinkId);
        link.ShouldNotBeNull();
        link.RevokedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task RevokeShareLinkAsync_AlreadyRevoked_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        EnableShareLinks(db);
        var pgpKey = SeedPgpKey(db);
        var service = CreateService(db);
        var created = await service.CreateShareLinkAsync(pgpKey.Id, "pgp", "admin", expiryDays: 7, CancellationToken.None);
        await service.RevokeShareLinkAsync(created.Data!.LinkId, CancellationToken.None);

        // Act
        var result = await service.RevokeShareLinkAsync(created.Data.LinkId, CancellationToken.None);

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.ShareLinkRevoked);
    }

    [Fact]
    public async Task RevokeShareLinkAsync_NotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        // Act
        var result = await service.RevokeShareLinkAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.ShareLinkNotFound);
    }

    // --- GetSharedKeyAsync ---

    [Fact]
    public async Task GetSharedKeyAsync_ValidToken_ReturnsPgpPublicKey()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        EnableShareLinks(db);
        var pgpKey = SeedPgpKey(db, publicKeyData: "PGP-PUBLIC-KEY-DATA");
        var service = CreateService(db);
        var created = await service.CreateShareLinkAsync(pgpKey.Id, "pgp", "admin", expiryDays: 7, CancellationToken.None);

        // Act
        var result = await service.GetSharedKeyAsync(created.Data!.Token, "pgp", CancellationToken.None);

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.PublicKey.ShouldBe("PGP-PUBLIC-KEY-DATA");
        result.Data.KeyType.ShouldBe("pgp");
        result.Data.Name.ShouldBe("test-pgp-key");
    }

    [Fact]
    public async Task GetSharedKeyAsync_ValidToken_ReturnsSshPublicKey()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        EnableShareLinks(db);
        var sshKey = SeedSshKey(db, publicKeyData: "ssh-rsa AAAA-KEY-DATA");
        var service = CreateService(db);
        var created = await service.CreateShareLinkAsync(sshKey.Id, "ssh", "admin", expiryDays: 7, CancellationToken.None);

        // Act
        var result = await service.GetSharedKeyAsync(created.Data!.Token, "ssh", CancellationToken.None);

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.PublicKey.ShouldBe("ssh-rsa AAAA-KEY-DATA");
        result.Data.KeyType.ShouldBe("ssh");
    }

    [Fact]
    public async Task GetSharedKeyAsync_FeatureDisabled_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        // Feature NOT enabled
        var service = CreateService(db);

        // Act
        var result = await service.GetSharedKeyAsync("some-token", "pgp", CancellationToken.None);

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.ShareLinksDisabled);
    }

    [Fact]
    public async Task GetSharedKeyAsync_InvalidToken_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        EnableShareLinks(db);
        var service = CreateService(db);

        // Act
        var result = await service.GetSharedKeyAsync("invalid-token-value", "pgp", CancellationToken.None);

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.ShareLinkInvalidToken);
    }

    [Fact]
    public async Task GetSharedKeyAsync_RevokedToken_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        EnableShareLinks(db);
        var pgpKey = SeedPgpKey(db);
        var service = CreateService(db);
        var created = await service.CreateShareLinkAsync(pgpKey.Id, "pgp", "admin", expiryDays: 7, CancellationToken.None);
        await service.RevokeShareLinkAsync(created.Data!.LinkId, CancellationToken.None);

        // Act
        var result = await service.GetSharedKeyAsync(created.Data.Token, "pgp", CancellationToken.None);

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.ShareLinkInvalidToken);
    }

    // --- ListShareLinksAsync ---

    [Fact]
    public async Task ListShareLinksAsync_ReturnsAllLinksForKey()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        EnableShareLinks(db);
        var pgpKey = SeedPgpKey(db);
        var service = CreateService(db);

        await service.CreateShareLinkAsync(pgpKey.Id, "pgp", "admin", expiryDays: 7, CancellationToken.None);
        await service.CreateShareLinkAsync(pgpKey.Id, "pgp", "admin", expiryDays: 14, CancellationToken.None);

        // Act
        var result = await service.ListShareLinksAsync(pgpKey.Id, "pgp", CancellationToken.None);

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.Count.ShouldBe(2);
        result.Data.ShouldAllBe(l => l.Status == "active");
    }
}

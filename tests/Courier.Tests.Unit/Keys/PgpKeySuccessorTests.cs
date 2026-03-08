using Courier.Domain.Common;
using Courier.Domain.Encryption;
using Courier.Domain.Entities;
using Courier.Features.AuditLog;
using Courier.Features.PgpKeys;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Keys;

public class PgpKeySuccessorTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    private static ICredentialEncryptor CreateMockEncryptor()
    {
        var encryptor = Substitute.For<ICredentialEncryptor>();
        encryptor.Encrypt(Arg.Any<string>()).Returns(ci => System.Text.Encoding.UTF8.GetBytes($"enc:{ci.Arg<string>()}"));
        encryptor.Decrypt(Arg.Any<byte[]>()).Returns(ci =>
        {
            var bytes = ci.Arg<byte[]>();
            var str = System.Text.Encoding.UTF8.GetString(bytes);
            return str.StartsWith("enc:") ? str[4..] : str;
        });
        return encryptor;
    }

    private static PgpKey CreatePgpKeyEntity(string name = "Key", string status = "active")
    {
        return new PgpKey
        {
            Id = Guid.NewGuid(),
            Name = name,
            Algorithm = "rsa_2048",
            KeyType = "key_pair",
            Status = status,
            Fingerprint = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task SetSuccessorAsync_ValidKeys_Succeeds()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor(), new AuditService(db));

        var keyA = CreatePgpKeyEntity("Key A");
        var keyB = CreatePgpKeyEntity("Key B");
        db.PgpKeys.AddRange(keyA, keyB);
        await db.SaveChangesAsync();

        // Act
        var result = await service.SetSuccessorAsync(keyA.Id, keyB.Id);

        // Assert
        result.Success.ShouldBeTrue();
        var updated = await db.PgpKeys.FindAsync(keyA.Id);
        updated!.SuccessorKeyId.ShouldBe(keyB.Id);
    }

    [Fact]
    public async Task SetSuccessorAsync_SelfReference_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor(), new AuditService(db));

        var key = CreatePgpKeyEntity("Key A");
        db.PgpKeys.Add(key);
        await db.SaveChangesAsync();

        // Act
        var result = await service.SetSuccessorAsync(key.Id, key.Id);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.KeySuccessorSelfReference);
    }

    [Fact]
    public async Task SetSuccessorAsync_RetiredSuccessor_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor(), new AuditService(db));

        var keyA = CreatePgpKeyEntity("Key A");
        var keyB = CreatePgpKeyEntity("Key B", status: "retired");
        db.PgpKeys.AddRange(keyA, keyB);
        await db.SaveChangesAsync();

        // Act
        var result = await service.SetSuccessorAsync(keyA.Id, keyB.Id);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.KeySuccessorInvalidStatus);
    }

    [Fact]
    public async Task SetSuccessorAsync_RevokedSuccessor_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor(), new AuditService(db));

        var keyA = CreatePgpKeyEntity("Key A");
        var keyB = CreatePgpKeyEntity("Key B", status: "revoked");
        db.PgpKeys.AddRange(keyA, keyB);
        await db.SaveChangesAsync();

        // Act
        var result = await service.SetSuccessorAsync(keyA.Id, keyB.Id);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.KeySuccessorInvalidStatus);
    }

    [Fact]
    public async Task SetSuccessorAsync_CircularChain_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor(), new AuditService(db));

        var keyA = CreatePgpKeyEntity("Key A");
        var keyB = CreatePgpKeyEntity("Key B");
        var keyC = CreatePgpKeyEntity("Key C");
        db.PgpKeys.AddRange(keyA, keyB, keyC);
        await db.SaveChangesAsync();

        // Set up chain: A -> B -> C
        await service.SetSuccessorAsync(keyA.Id, keyB.Id);
        await service.SetSuccessorAsync(keyB.Id, keyC.Id);

        // Act - try to close the loop: C -> A
        var result = await service.SetSuccessorAsync(keyC.Id, keyA.Id);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.KeySuccessorCircularChain);
    }

    [Fact]
    public async Task SetSuccessorAsync_NonExistentKey_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor(), new AuditService(db));

        var key = CreatePgpKeyEntity("Key A");
        db.PgpKeys.Add(key);
        await db.SaveChangesAsync();

        // Act
        var result = await service.SetSuccessorAsync(Guid.NewGuid(), key.Id);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.KeyNotFound);
    }

    [Fact]
    public async Task SetSuccessorAsync_NonExistentSuccessor_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor(), new AuditService(db));

        var key = CreatePgpKeyEntity("Key A");
        db.PgpKeys.Add(key);
        await db.SaveChangesAsync();

        // Act
        var result = await service.SetSuccessorAsync(key.Id, Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.KeyNotFound);
    }

    [Fact]
    public async Task SetSuccessorAsync_ActiveSuccessor_Succeeds()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor(), new AuditService(db));

        var keyA = CreatePgpKeyEntity("Key A");
        var keyB = CreatePgpKeyEntity("Key B", status: "active");
        db.PgpKeys.AddRange(keyA, keyB);
        await db.SaveChangesAsync();

        // Act
        var result = await service.SetSuccessorAsync(keyA.Id, keyB.Id);

        // Assert
        result.Success.ShouldBeTrue();
    }
}

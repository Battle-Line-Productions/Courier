using Courier.Domain.Encryption;
using Courier.Features.PgpKeys;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.PgpKeys;

public class PgpKeyServiceTests
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

    private static GeneratePgpKeyRequest MakeGenerateRequest(
        string name = "Test PGP Key",
        string algorithm = "rsa_2048",
        string? passphrase = null) => new()
    {
        Name = name,
        Algorithm = algorithm,
        Passphrase = passphrase,
        RealName = "Test User",
        Email = "test@example.com",
    };

    [Fact]
    public async Task Generate_Rsa2048_ReturnsSuccessWithFingerprint()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());

        var result = await service.GenerateAsync(MakeGenerateRequest());

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Name.ShouldBe("Test PGP Key");
        result.Data.Algorithm.ShouldBe("rsa_2048");
        result.Data.KeyType.ShouldBe("key_pair");
        result.Data.Status.ShouldBe("active");
        result.Data.Fingerprint.ShouldNotBeNullOrEmpty();
        result.Data.ShortKeyId.ShouldNotBeNullOrEmpty();
        result.Data.HasPublicKey.ShouldBeTrue();
        result.Data.HasPrivateKey.ShouldBeTrue();
    }

    [Fact]
    public async Task Generate_EncryptsPrivateKey()
    {
        using var db = CreateInMemoryContext();
        var encryptor = CreateMockEncryptor();
        var service = new PgpKeyService(db, encryptor);

        await service.GenerateAsync(MakeGenerateRequest());

        // Encrypt is called at least once for the private key
        encryptor.Received().Encrypt(Arg.Any<string>());
    }

    [Fact]
    public async Task Generate_WithPassphrase_EncryptsPassphrase()
    {
        using var db = CreateInMemoryContext();
        var encryptor = CreateMockEncryptor();
        var service = new PgpKeyService(db, encryptor);

        await service.GenerateAsync(MakeGenerateRequest(passphrase: "my-secret"));

        encryptor.Received().Encrypt("my-secret");
    }

    [Fact]
    public async Task Generate_WithExpiresInDays_SetsExpiresAt()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        var request = MakeGenerateRequest() with { ExpiresInDays = 365 };

        var result = await service.GenerateAsync(request);

        result.Data!.ExpiresAt.ShouldNotBeNull();
        result.Data.ExpiresAt!.Value.ShouldBeGreaterThan(DateTime.UtcNow.AddDays(364));
    }

    [Fact]
    public async Task GetById_ExistingKey_ReturnsKey()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        var created = await service.GenerateAsync(MakeGenerateRequest("Find Me"));

        var result = await service.GetByIdAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();
        result.Data!.Name.ShouldBe("Find Me");
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(4000);
    }

    [Fact]
    public async Task List_ReturnsAllKeys()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        await service.GenerateAsync(MakeGenerateRequest("Key A"));
        await service.GenerateAsync(MakeGenerateRequest("Key B"));

        var result = await service.ListAsync();

        result.Success.ShouldBeTrue();
        result.Data.Count.ShouldBe(2);
        result.Pagination.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task List_FilterByStatus_ReturnsMatching()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        var created = await service.GenerateAsync(MakeGenerateRequest("To Retire"));
        await service.RetireAsync(created.Data!.Id);
        await service.GenerateAsync(MakeGenerateRequest("Still Active"));

        var result = await service.ListAsync(status: "active");

        result.Data.Count.ShouldBe(1);
        result.Data[0].Name.ShouldBe("Still Active");
    }

    [Fact]
    public async Task List_FilterBySearch_MatchesNameOrFingerprint()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        await service.GenerateAsync(MakeGenerateRequest("Alpha Key"));
        await service.GenerateAsync(MakeGenerateRequest("Beta Key"));

        var result = await service.ListAsync(search: "alpha");

        result.Data.Count.ShouldBe(1);
        result.Data[0].Name.ShouldBe("Alpha Key");
    }

    [Fact]
    public async Task Update_ExistingKey_ReturnsUpdated()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        var created = await service.GenerateAsync(MakeGenerateRequest("Old Name"));

        var result = await service.UpdateAsync(created.Data!.Id, new UpdatePgpKeyRequest
        {
            Name = "New Name",
            Purpose = "Updated purpose",
            Notes = "Some notes",
        });

        result.Success.ShouldBeTrue();
        result.Data!.Name.ShouldBe("New Name");
        result.Data.Purpose.ShouldBe("Updated purpose");
        result.Data.Notes.ShouldBe("Some notes");
    }

    [Fact]
    public async Task Delete_ExistingKey_PurgesKeyMaterial()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        var created = await service.GenerateAsync(MakeGenerateRequest("To Delete"));

        var result = await service.DeleteAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();

        // Verify soft delete and material purge
        var deleted = await db.PgpKeys.IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == created.Data.Id);
        deleted.ShouldNotBeNull();
        deleted!.IsDeleted.ShouldBeTrue();
        deleted.Status.ShouldBe("deleted");
        deleted.PublicKeyData.ShouldBeNull();
        deleted.PrivateKeyData.ShouldBeNull();
        deleted.PassphraseHash.ShouldBeNull();
    }

    [Fact]
    public async Task ExportPublicKey_ExistingKey_ReturnsArmoredData()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        var created = await service.GenerateAsync(MakeGenerateRequest());

        var result = await service.ExportPublicKeyAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();
        result.Data!.ShouldContain("BEGIN PGP PUBLIC KEY BLOCK");
    }

    [Fact]
    public async Task Retire_ActiveKey_SetsRetired()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        var created = await service.GenerateAsync(MakeGenerateRequest());

        var result = await service.RetireAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();
        result.Data!.Status.ShouldBe("retired");
    }

    [Fact]
    public async Task Retire_AlreadyRetired_ReturnsError()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        var created = await service.GenerateAsync(MakeGenerateRequest());
        await service.RetireAsync(created.Data!.Id);

        var result = await service.RetireAsync(created.Data!.Id);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(4010);
    }

    [Fact]
    public async Task Revoke_ActiveKey_PurgesPrivateKey()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        var created = await service.GenerateAsync(MakeGenerateRequest());

        var result = await service.RevokeAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();
        result.Data!.Status.ShouldBe("revoked");
        result.Data.HasPrivateKey.ShouldBeFalse();

        // Verify private key purged in DB
        var revoked = await db.PgpKeys.IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == created.Data.Id);
        revoked!.PrivateKeyData.ShouldBeNull();
        revoked.PassphraseHash.ShouldBeNull();
    }

    [Fact]
    public async Task Activate_RetiredKey_SetsActive()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        var created = await service.GenerateAsync(MakeGenerateRequest());
        await service.RetireAsync(created.Data!.Id);

        var result = await service.ActivateAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();
        result.Data!.Status.ShouldBe("active");
    }

    [Fact]
    public async Task Activate_RevokedKey_ReturnsError()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        var created = await service.GenerateAsync(MakeGenerateRequest());
        await service.RevokeAsync(created.Data!.Id);

        var result = await service.ActivateAsync(created.Data!.Id);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(4030);
    }

    [Fact]
    public async Task GetById_NeverReturnsKeyMaterial()
    {
        using var db = CreateInMemoryContext();
        var service = new PgpKeyService(db, CreateMockEncryptor());
        var created = await service.GenerateAsync(MakeGenerateRequest());

        var result = await service.GetByIdAsync(created.Data!.Id);

        // PgpKeyDto has no PublicKeyData or PrivateKeyData fields — only HasPublicKey/HasPrivateKey bools
        result.Data!.GetType().GetProperty("PublicKeyData").ShouldBeNull();
        result.Data.GetType().GetProperty("PrivateKeyData").ShouldBeNull();
        result.Data.HasPublicKey.ShouldBeTrue();
        result.Data.HasPrivateKey.ShouldBeTrue();
    }
}

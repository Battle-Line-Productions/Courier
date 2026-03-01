using Courier.Domain.Encryption;
using Courier.Features.AuditLog;
using Courier.Features.SshKeys;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.SshKeys;

public class SshKeyServiceTests
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

    private static GenerateSshKeyRequest MakeGenerateRequest(
        string name = "Test SSH Key",
        string keyType = "ed25519",
        string? passphrase = null) => new()
    {
        Name = name,
        KeyType = keyType,
        Passphrase = passphrase,
    };

    [Fact]
    public async Task Generate_Ed25519_ReturnsSuccessWithFingerprint()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));

        var result = await service.GenerateAsync(MakeGenerateRequest());

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Name.ShouldBe("Test SSH Key");
        result.Data.KeyType.ShouldBe("ed25519");
        result.Data.Status.ShouldBe("active");
        result.Data.Fingerprint.ShouldStartWith("SHA256:");
        result.Data.HasPublicKey.ShouldBeTrue();
        result.Data.HasPrivateKey.ShouldBeTrue();
    }

    [Fact]
    public async Task Generate_Rsa2048_ReturnsSuccessWithSshRsaPrefix()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));

        var result = await service.GenerateAsync(MakeGenerateRequest(keyType: "rsa_2048"));

        result.Success.ShouldBeTrue();
        result.Data!.KeyType.ShouldBe("rsa_2048");
        result.Data.Fingerprint.ShouldStartWith("SHA256:");
    }

    [Fact]
    public async Task Generate_Rsa4096_ReturnsSuccess()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));

        var result = await service.GenerateAsync(MakeGenerateRequest(keyType: "rsa_4096"));

        result.Success.ShouldBeTrue();
        result.Data!.KeyType.ShouldBe("rsa_4096");
    }

    [Fact]
    public async Task Generate_Ecdsa256_ReturnsSuccess()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));

        var result = await service.GenerateAsync(MakeGenerateRequest(keyType: "ecdsa_256"));

        result.Success.ShouldBeTrue();
        result.Data!.KeyType.ShouldBe("ecdsa_256");
        result.Data.Fingerprint.ShouldStartWith("SHA256:");
    }

    [Fact]
    public async Task Generate_EncryptsPrivateKey()
    {
        using var db = CreateInMemoryContext();
        var encryptor = CreateMockEncryptor();
        var service = new SshKeyService(db, encryptor, new AuditService(db));

        await service.GenerateAsync(MakeGenerateRequest());

        encryptor.Received().Encrypt(Arg.Any<string>());
    }

    [Fact]
    public async Task Generate_WithPassphrase_EncryptsPassphrase()
    {
        using var db = CreateInMemoryContext();
        var encryptor = CreateMockEncryptor();
        var service = new SshKeyService(db, encryptor, new AuditService(db));

        await service.GenerateAsync(MakeGenerateRequest(passphrase: "my-secret"));

        encryptor.Received().Encrypt("my-secret");
    }

    [Fact]
    public async Task GetById_ExistingKey_ReturnsKey()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));
        var created = await service.GenerateAsync(MakeGenerateRequest("Find Me"));

        var result = await service.GetByIdAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();
        result.Data!.Name.ShouldBe("Find Me");
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(4000);
    }

    [Fact]
    public async Task List_ReturnsAllKeys()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));
        await service.GenerateAsync(MakeGenerateRequest("Key A"));
        await service.GenerateAsync(MakeGenerateRequest("Key B"));

        var result = await service.ListAsync();

        result.Data.Count.ShouldBe(2);
        result.Pagination.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task List_FilterBySearch_MatchesName()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));
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
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));
        var created = await service.GenerateAsync(MakeGenerateRequest("Old Name"));

        var result = await service.UpdateAsync(created.Data!.Id, new UpdateSshKeyRequest
        {
            Name = "New Name",
            Notes = "Some notes",
        });

        result.Success.ShouldBeTrue();
        result.Data!.Name.ShouldBe("New Name");
        result.Data.Notes.ShouldBe("Some notes");
    }

    [Fact]
    public async Task Delete_ExistingKey_PurgesKeyMaterial()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));
        var created = await service.GenerateAsync(MakeGenerateRequest("To Delete"));

        var result = await service.DeleteAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();

        var deleted = await db.SshKeys.IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == created.Data.Id);
        deleted.ShouldNotBeNull();
        deleted!.IsDeleted.ShouldBeTrue();
        deleted.Status.ShouldBe("deleted");
        deleted.PublicKeyData.ShouldBeNull();
        deleted.PrivateKeyData.ShouldBeNull();
        deleted.PassphraseHash.ShouldBeNull();
    }

    [Fact]
    public async Task ExportPublicKey_ExistingKey_ReturnsOpenSshFormat()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));
        var created = await service.GenerateAsync(MakeGenerateRequest());

        var result = await service.ExportPublicKeyAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();
        result.Data.ShouldStartWith("ssh-ed25519");
    }

    [Fact]
    public async Task Retire_ActiveKey_SetsRetired()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));
        var created = await service.GenerateAsync(MakeGenerateRequest());

        var result = await service.RetireAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();
        result.Data!.Status.ShouldBe("retired");
    }

    [Fact]
    public async Task Activate_RetiredKey_SetsActive()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));
        var created = await service.GenerateAsync(MakeGenerateRequest());
        await service.RetireAsync(created.Data!.Id);

        var result = await service.ActivateAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();
        result.Data!.Status.ShouldBe("active");
    }

    [Fact]
    public async Task Activate_AlreadyActive_ReturnsError()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));
        var created = await service.GenerateAsync(MakeGenerateRequest());

        var result = await service.ActivateAsync(created.Data!.Id);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(4012);
    }

    [Fact]
    public async Task ExportPublicKey_RsaKey_ReturnsSshRsaFormat()
    {
        using var db = CreateInMemoryContext();
        var service = new SshKeyService(db, CreateMockEncryptor(), new AuditService(db));
        var created = await service.GenerateAsync(MakeGenerateRequest(keyType: "rsa_2048"));

        var result = await service.ExportPublicKeyAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();
        result.Data!.ShouldStartWith("ssh-rsa");
    }
}

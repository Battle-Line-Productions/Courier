using Courier.Domain.Encryption;
using Courier.Features.Connections;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Connections;

public class ConnectionServiceTests
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

    private static CreateConnectionRequest MakeSftpRequest(string name = "Test SFTP", string? password = "secret") => new()
    {
        Name = name,
        Protocol = "sftp",
        Host = "sftp.example.com",
        AuthMethod = "password",
        Username = "user1",
        Password = password,
    };

    private static CreateConnectionRequest MakeFtpRequest(string name = "Test FTP") => new()
    {
        Name = name,
        Protocol = "ftp",
        Host = "ftp.example.com",
        AuthMethod = "password",
        Username = "user1",
        Password = "secret",
    };

    private static CreateConnectionRequest MakeFtpsRequest(string name = "Test FTPS") => new()
    {
        Name = name,
        Protocol = "ftps",
        Host = "ftps.example.com",
        AuthMethod = "password",
        Username = "user1",
        Password = "secret",
    };

    [Fact]
    public async Task Create_ValidSftpRequest_ReturnsSuccessWithDefaultPort22()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());

        var result = await service.CreateAsync(MakeSftpRequest());

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Name.ShouldBe("Test SFTP");
        result.Data.Protocol.ShouldBe("sftp");
        result.Data.Port.ShouldBe(22);
        result.Data.Id.ShouldNotBe(Guid.Empty);
        result.Data.Status.ShouldBe("active");
    }

    [Fact]
    public async Task Create_ValidFtpRequest_ReturnsSuccessWithDefaultPort21()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());

        var result = await service.CreateAsync(MakeFtpRequest());

        result.Success.ShouldBeTrue();
        result.Data!.Port.ShouldBe(21);
    }

    [Fact]
    public async Task Create_ValidFtpsRequest_ReturnsSuccessWithDefaultPort990()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());

        var result = await service.CreateAsync(MakeFtpsRequest());

        result.Success.ShouldBeTrue();
        result.Data!.Port.ShouldBe(990);
    }

    [Fact]
    public async Task Create_CustomPort_UsesProvidedPort()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());
        var request = MakeSftpRequest() with { Port = 2222 };

        var result = await service.CreateAsync(request);

        result.Data!.Port.ShouldBe(2222);
    }

    [Fact]
    public async Task Create_WithPassword_EncryptsPassword()
    {
        using var db = CreateInMemoryContext();
        var encryptor = CreateMockEncryptor();
        var service = new ConnectionService(db, encryptor);

        var result = await service.CreateAsync(MakeSftpRequest());

        result.Data!.HasPassword.ShouldBeTrue();
        encryptor.Received(1).Encrypt("secret");
    }

    [Fact]
    public async Task Create_WithoutPassword_HasPasswordFalse()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());
        var request = MakeSftpRequest(password: null);

        var result = await service.CreateAsync(request);

        result.Data!.HasPassword.ShouldBeFalse();
    }

    [Fact]
    public async Task GetById_ExistingConnection_ReturnsConnection()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());
        var created = await service.CreateAsync(MakeSftpRequest("Find Me"));

        var result = await service.GetByIdAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();
        result.Data!.Name.ShouldBe("Find Me");
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFoundError()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(1030);
    }

    [Fact]
    public async Task GetById_NeverReturnsPasswordBytes()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());
        var created = await service.CreateAsync(MakeSftpRequest());

        var result = await service.GetByIdAsync(created.Data!.Id);

        result.Data!.HasPassword.ShouldBeTrue();
        // ConnectionDto has no PasswordEncrypted field — only HasPassword bool
        result.Data.GetType().GetProperty("PasswordEncrypted").ShouldBeNull();
    }

    [Fact]
    public async Task List_ReturnsAllConnections()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());
        await service.CreateAsync(MakeSftpRequest("Conn A"));
        await service.CreateAsync(MakeFtpRequest("Conn B"));

        var result = await service.ListAsync();

        result.Success.ShouldBeTrue();
        result.Data.Count.ShouldBe(2);
        result.Pagination.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task List_FilterByProtocol_ReturnsMatching()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());
        await service.CreateAsync(MakeSftpRequest("SFTP One"));
        await service.CreateAsync(MakeFtpRequest("FTP One"));

        var result = await service.ListAsync(protocol: "sftp");

        result.Data.Count.ShouldBe(1);
        result.Data[0].Protocol.ShouldBe("sftp");
    }

    [Fact]
    public async Task List_FilterBySearch_MatchesNameOrHost()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());
        await service.CreateAsync(MakeSftpRequest("Alpha Server"));
        await service.CreateAsync(MakeFtpRequest("Beta Server"));

        var result = await service.ListAsync(search: "alpha");

        result.Data.Count.ShouldBe(1);
        result.Data[0].Name.ShouldBe("Alpha Server");
    }

    [Fact]
    public async Task List_FilterByStatus_ReturnsMatching()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());
        var created = await service.CreateAsync(MakeSftpRequest("Active One"));
        await service.UpdateAsync(created.Data!.Id, new UpdateConnectionRequest
        {
            Name = "Active One", Protocol = "sftp", Host = "sftp.example.com",
            AuthMethod = "password", Username = "user1", Status = "disabled"
        });
        await service.CreateAsync(MakeSftpRequest("Active Two"));

        var result = await service.ListAsync(status: "active");

        result.Data.Count.ShouldBe(1);
        result.Data[0].Name.ShouldBe("Active Two");
    }

    [Fact]
    public async Task Update_ExistingConnection_ReturnsUpdated()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());
        var created = await service.CreateAsync(MakeSftpRequest("Old Name"));

        var result = await service.UpdateAsync(created.Data!.Id, new UpdateConnectionRequest
        {
            Name = "New Name",
            Protocol = "sftp",
            Host = "new-host.example.com",
            AuthMethod = "password",
            Username = "newuser",
        });

        result.Success.ShouldBeTrue();
        result.Data!.Name.ShouldBe("New Name");
        result.Data.Host.ShouldBe("new-host.example.com");
        result.Data.Username.ShouldBe("newuser");
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNotFound()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateConnectionRequest
        {
            Name = "X", Protocol = "sftp", Host = "x", AuthMethod = "password", Username = "x"
        });

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(1030);
    }

    [Fact]
    public async Task Update_PasswordNull_NoChange()
    {
        using var db = CreateInMemoryContext();
        var encryptor = CreateMockEncryptor();
        var service = new ConnectionService(db, encryptor);
        var created = await service.CreateAsync(MakeSftpRequest());

        // Update with Password = null (no change)
        var result = await service.UpdateAsync(created.Data!.Id, new UpdateConnectionRequest
        {
            Name = "Same", Protocol = "sftp", Host = "sftp.example.com",
            AuthMethod = "password", Username = "user1", Password = null
        });

        result.Data!.HasPassword.ShouldBeTrue();
        // Encrypt was called once (for create), not again for update
        encryptor.Received(1).Encrypt(Arg.Any<string>());
    }

    [Fact]
    public async Task Update_PasswordEmpty_ClearsPassword()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());
        var created = await service.CreateAsync(MakeSftpRequest());

        var result = await service.UpdateAsync(created.Data!.Id, new UpdateConnectionRequest
        {
            Name = "Same", Protocol = "sftp", Host = "sftp.example.com",
            AuthMethod = "password", Username = "user1", Password = ""
        });

        result.Data!.HasPassword.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_PasswordNewValue_ReEncrypts()
    {
        using var db = CreateInMemoryContext();
        var encryptor = CreateMockEncryptor();
        var service = new ConnectionService(db, encryptor);
        var created = await service.CreateAsync(MakeSftpRequest());

        await service.UpdateAsync(created.Data!.Id, new UpdateConnectionRequest
        {
            Name = "Same", Protocol = "sftp", Host = "sftp.example.com",
            AuthMethod = "password", Username = "user1", Password = "new-secret"
        });

        encryptor.Received(1).Encrypt("secret"); // create
        encryptor.Received(1).Encrypt("new-secret"); // update
    }

    [Fact]
    public async Task Delete_ExistingConnection_SoftDeletes()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());
        var created = await service.CreateAsync(MakeSftpRequest("To Delete"));

        var result = await service.DeleteAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();

        // Verify soft delete — bypass query filter
        var deleted = await db.Connections.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == created.Data.Id);
        deleted.ShouldNotBeNull();
        deleted!.IsDeleted.ShouldBeTrue();
        deleted.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Delete_NonExistent_ReturnsNotFound()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());

        var result = await service.DeleteAsync(Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(1030);
    }

    [Fact]
    public async Task Create_DefaultTimeoutsApplied()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());

        var result = await service.CreateAsync(MakeSftpRequest());

        result.Data!.ConnectTimeoutSec.ShouldBe(30);
        result.Data.OperationTimeoutSec.ShouldBe(300);
        result.Data.KeepaliveIntervalSec.ShouldBe(60);
        result.Data.TransportRetries.ShouldBe(2);
    }

    [Fact]
    public async Task Create_DefaultHostKeyPolicyApplied()
    {
        using var db = CreateInMemoryContext();
        var service = new ConnectionService(db, CreateMockEncryptor());

        var result = await service.CreateAsync(MakeSftpRequest());

        result.Data!.HostKeyPolicy.ShouldBe("trust_on_first_use");
        result.Data.TlsCertPolicy.ShouldBe("system_trust");
    }
}

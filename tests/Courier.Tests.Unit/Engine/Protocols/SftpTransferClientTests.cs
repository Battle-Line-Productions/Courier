using System.Text;
using Courier.Domain.Entities;
using Courier.Features.Engine.Protocols;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Protocols;

public class SftpTransferClientTests
{
    private static Connection CreateTestConnection(string authMethod = "password") => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test SFTP",
        Protocol = "sftp",
        Host = "sftp.example.com",
        Port = 22,
        AuthMethod = authMethod,
        Username = "testuser",
        HostKeyPolicy = "always_trust",
        ConnectTimeoutSec = 30,
        OperationTimeoutSec = 300,
        KeepaliveIntervalSec = 0,
    };

    [Fact]
    public void Protocol_ReturnsSftp()
    {
        var connection = CreateTestConnection();
        var client = new SftpTransferClient(connection, Encoding.UTF8.GetBytes("pass"), null);

        client.Protocol.ShouldBe("sftp");
    }

    [Fact]
    public void IsConnected_BeforeConnect_ReturnsFalse()
    {
        var connection = CreateTestConnection();
        var client = new SftpTransferClient(connection, Encoding.UTF8.GetBytes("pass"), null);

        client.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_PasswordAuth_DoesNotThrow()
    {
        var connection = CreateTestConnection("password");
        Should.NotThrow(() => new SftpTransferClient(connection, Encoding.UTF8.GetBytes("pass"), null));
    }

    [Fact]
    public void Constructor_SshKeyAuth_DoesNotThrow()
    {
        var connection = CreateTestConnection("ssh_key");
        var fakeKey = Encoding.UTF8.GetBytes("-----BEGIN RSA PRIVATE KEY-----\nfake\n-----END RSA PRIVATE KEY-----");
        Should.NotThrow(() => new SftpTransferClient(connection, null, fakeKey));
    }

    [Fact]
    public void Constructor_PasswordAndSshKeyAuth_DoesNotThrow()
    {
        var connection = CreateTestConnection("password_and_ssh_key");
        var fakeKey = Encoding.UTF8.GetBytes("-----BEGIN RSA PRIVATE KEY-----\nfake\n-----END RSA PRIVATE KEY-----");
        Should.NotThrow(() => new SftpTransferClient(connection, Encoding.UTF8.GetBytes("pass"), fakeKey));
    }

    [Fact]
    public void Constructor_NullConnection_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new SftpTransferClient(null!, null, null));
    }

    [Fact]
    public async Task DisposeAsync_BeforeConnect_DoesNotThrow()
    {
        var connection = CreateTestConnection();
        var client = new SftpTransferClient(connection, Encoding.UTF8.GetBytes("pass"), null);

        await Should.NotThrowAsync(async () => await client.DisposeAsync());
    }
}

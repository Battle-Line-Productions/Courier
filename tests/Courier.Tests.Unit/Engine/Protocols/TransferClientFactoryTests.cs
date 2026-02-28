using System.Text;
using Courier.Domain.Entities;
using Courier.Features.Engine.Protocols;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Protocols;

public class TransferClientFactoryTests
{
    private readonly TransferClientFactory _factory = new();

    private static Connection CreateConnection(string protocol, int port = 22) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"Test {protocol}",
        Protocol = protocol,
        Host = "host.example.com",
        Port = port,
        AuthMethod = "password",
        Username = "testuser",
        ConnectTimeoutSec = 30,
        OperationTimeoutSec = 300,
    };

    [Fact]
    public void Create_Sftp_ReturnsSftpTransferClient()
    {
        var connection = CreateConnection("sftp", 22);
        var client = _factory.Create(connection, Encoding.UTF8.GetBytes("pass"), null);

        client.ShouldBeOfType<SftpTransferClient>();
        client.Protocol.ShouldBe("sftp");
    }

    [Fact]
    public void Create_Ftp_ReturnsFluentFtpTransferClient()
    {
        var connection = CreateConnection("ftp", 21);
        var client = _factory.Create(connection, Encoding.UTF8.GetBytes("pass"), null);

        client.ShouldBeOfType<FluentFtpTransferClient>();
        client.Protocol.ShouldBe("ftp");
    }

    [Fact]
    public void Create_Ftps_ExplicitPort_ReturnsFluentFtpTransferClientWithFtpsProtocol()
    {
        var connection = CreateConnection("ftps", 21);
        var client = _factory.Create(connection, Encoding.UTF8.GetBytes("pass"), null);

        client.ShouldBeOfType<FluentFtpTransferClient>();
        client.Protocol.ShouldBe("ftps");
    }

    [Fact]
    public void Create_Ftps_ImplicitPort990_ReturnsFluentFtpTransferClient()
    {
        var connection = CreateConnection("ftps", 990);
        var client = _factory.Create(connection, Encoding.UTF8.GetBytes("pass"), null);

        client.ShouldBeOfType<FluentFtpTransferClient>();
        client.Protocol.ShouldBe("ftps");
    }

    [Fact]
    public void Create_UnknownProtocol_ThrowsArgumentException()
    {
        var connection = CreateConnection("scp");

        var ex = Should.Throw<ArgumentException>(() =>
            _factory.Create(connection, null, null));

        ex.Message.ShouldContain("Unsupported protocol");
        ex.Message.ShouldContain("scp");
    }

    [Fact]
    public void Create_ProtocolIsCaseInsensitive()
    {
        var connection = CreateConnection("SFTP", 22);
        var client = _factory.Create(connection, Encoding.UTF8.GetBytes("pass"), null);

        client.ShouldBeOfType<SftpTransferClient>();
    }
}

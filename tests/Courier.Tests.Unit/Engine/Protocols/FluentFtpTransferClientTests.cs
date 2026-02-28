using System.Text;
using Courier.Domain.Entities;
using Courier.Features.Engine.Protocols;
using FluentFTP;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Protocols;

public class FluentFtpTransferClientTests
{
    private static Connection CreateTestConnection() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test FTP",
        Protocol = "ftp",
        Host = "ftp.example.com",
        Port = 21,
        AuthMethod = "password",
        Username = "testuser",
        PassiveMode = true,
        TlsCertPolicy = "system_trust",
        ConnectTimeoutSec = 30,
        OperationTimeoutSec = 300,
        KeepaliveIntervalSec = 0,
    };

    [Theory]
    [InlineData(FtpEncryptionMode.None, "ftp")]
    [InlineData(FtpEncryptionMode.Explicit, "ftps")]
    [InlineData(FtpEncryptionMode.Implicit, "ftps")]
    public void Protocol_ReturnsCorrectValue_ForEncryptionMode(FtpEncryptionMode mode, string expected)
    {
        var connection = CreateTestConnection();
        var client = new FluentFtpTransferClient(connection, Encoding.UTF8.GetBytes("pass"), mode);

        client.Protocol.ShouldBe(expected);
    }

    [Fact]
    public void IsConnected_BeforeConnect_ReturnsFalse()
    {
        var connection = CreateTestConnection();
        var client = new FluentFtpTransferClient(connection, Encoding.UTF8.GetBytes("pass"), FtpEncryptionMode.None);

        client.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_NullConnection_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(
            () => new FluentFtpTransferClient(null!, null, FtpEncryptionMode.None));
    }

    [Fact]
    public async Task DisposeAsync_BeforeConnect_DoesNotThrow()
    {
        var connection = CreateTestConnection();
        var client = new FluentFtpTransferClient(connection, Encoding.UTF8.GetBytes("pass"), FtpEncryptionMode.None);

        await Should.NotThrowAsync(async () => await client.DisposeAsync());
    }
}

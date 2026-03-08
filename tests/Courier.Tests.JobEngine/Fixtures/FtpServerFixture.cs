using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentFTP;

namespace Courier.Tests.JobEngine.Fixtures;

public class FtpServerFixture : IAsyncLifetime
{
    private readonly IContainer _container;

    public string Host => _container.Hostname;
    public int ControlPort => _container.GetMappedPublicPort(21);
    public string Username => "testuser";
    public string Password => "testpass";

    public FtpServerFixture()
    {
        _container = new ContainerBuilder("fauria/vsftpd")
            .WithEnvironment("FTP_USER", "testuser")
            .WithEnvironment("FTP_PASS", "testpass")
            .WithEnvironment("PASV_ADDRESS", "127.0.0.1")
            .WithEnvironment("PASV_MIN_PORT", "21100")
            .WithEnvironment("PASV_MAX_PORT", "21110")
            .WithPortBinding(21, assignRandomHostPort: true)
            .WithPortBinding(21100, 21100)
            .WithPortBinding(21101, 21101)
            .WithPortBinding(21102, 21102)
            .WithPortBinding(21103, 21103)
            .WithPortBinding(21104, 21104)
            .WithPortBinding(21105, 21105)
            .WithPortBinding(21106, 21106)
            .WithPortBinding(21107, 21107)
            .WithPortBinding(21108, 21108)
            .WithPortBinding(21109, 21109)
            .WithPortBinding(21110, 21110)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(21))
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // Wait for FTP to be fully ready
        await WaitForFtpReady();
    }

    private async Task WaitForFtpReady()
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                using var client = new AsyncFtpClient(Host, Username, Password, ControlPort);
                client.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;
                await client.Connect();
                await client.GetListing("/");
                await client.Disconnect();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException(
            $"FTP server did not become ready within 30 seconds. Last error: {lastException?.GetType().Name}: {lastException?.Message}");
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public IContainer Container => _container;
}

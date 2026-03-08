using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Renci.SshNet;

namespace Courier.Tests.JobEngine.Fixtures;

public class SftpServerFixture : IAsyncLifetime
{
    private readonly IContainer _container;

    public string Host => _container.Hostname;
    public int Port => _container.GetMappedPublicPort(2222);
    public string Username => "testuser";
    public string Password => "testpass";

    public SftpServerFixture()
    {
        _container = new ContainerBuilder("linuxserver/openssh-server:latest")
            .WithEnvironment("PUID", "1000")
            .WithEnvironment("PGID", "1000")
            .WithEnvironment("USER_NAME", "testuser")
            .WithEnvironment("USER_PASSWORD", "testpass")
            .WithEnvironment("PASSWORD_ACCESS", "true")
            .WithPortBinding(2222, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(2222))
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // Create upload and download directories at paths used by tests
        await _container.ExecAsync(["mkdir", "-p", "/home/testuser/upload"]);
        await _container.ExecAsync(["mkdir", "-p", "/home/testuser/download"]);
        await _container.ExecAsync(["chown", "-R", "testuser:testuser", "/home/testuser"]);

        // Wait for SSH/SFTP to be fully ready
        await WaitForSftpReady();
    }

    private async Task WaitForSftpReady()
    {
        var host = Host;
        var port = Port;

        Exception? lastException = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                using var client = new SftpClient(host, port, Username, Password);
                client.HostKeyReceived += (_, e) => e.CanTrust = true;
                await Task.Run(() => client.Connect());
                client.ListDirectory("/");
                client.Disconnect();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException(
            $"SFTP server did not become ready within 30 seconds. Last error: {lastException?.GetType().Name}: {lastException?.Message}");
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public IContainer Container => _container;
}

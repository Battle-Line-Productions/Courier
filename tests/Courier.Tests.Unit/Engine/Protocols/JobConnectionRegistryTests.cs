using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Protocols;

public class JobConnectionRegistryTests
{
    private readonly ITransferClientFactory _factory;
    private readonly JobConnectionRegistry _registry;

    public JobConnectionRegistryTests()
    {
        _factory = Substitute.For<ITransferClientFactory>();
        _registry = new JobConnectionRegistry(_factory);
    }

    private static Connection CreateConnection() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Connection",
        Protocol = "sftp",
        Host = "host.example.com",
        Port = 22,
        AuthMethod = "password",
        Username = "testuser",
    };

    [Fact]
    public async Task GetOrOpenAsync_FirstCall_CreatesAndConnects()
    {
        var connection = CreateConnection();
        var mockClient = Substitute.For<ITransferClient>();
        mockClient.IsConnected.Returns(true);

        _factory.Create(connection, Arg.Any<byte[]?>(), Arg.Any<byte[]?>())
            .Returns(mockClient);

        var client = await _registry.GetOrOpenAsync(connection, null, null, CancellationToken.None);

        client.ShouldBe(mockClient);
        _factory.Received(1).Create(connection, Arg.Any<byte[]?>(), Arg.Any<byte[]?>());
        await mockClient.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrOpenAsync_SecondCall_ReusesConnection()
    {
        var connection = CreateConnection();
        var mockClient = Substitute.For<ITransferClient>();
        mockClient.IsConnected.Returns(true);

        _factory.Create(connection, Arg.Any<byte[]?>(), Arg.Any<byte[]?>())
            .Returns(mockClient);

        var client1 = await _registry.GetOrOpenAsync(connection, null, null, CancellationToken.None);
        var client2 = await _registry.GetOrOpenAsync(connection, null, null, CancellationToken.None);

        client1.ShouldBe(client2);
        _factory.Received(1).Create(connection, Arg.Any<byte[]?>(), Arg.Any<byte[]?>());
        // ConnectAsync should only be called once — the second call reuses the already-connected client
        await mockClient.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrOpenAsync_DisconnectedClient_Reconnects()
    {
        var connection = CreateConnection();
        var mockClient = Substitute.For<ITransferClient>();

        // IsConnected is only read when the client is found in the cache (second call onward).
        // Return false so that the registry detects a disconnected client and reconnects.
        mockClient.IsConnected.Returns(false);

        _factory.Create(connection, Arg.Any<byte[]?>(), Arg.Any<byte[]?>())
            .Returns(mockClient);

        // First call: creates + connects (IsConnected not checked on this path)
        await _registry.GetOrOpenAsync(connection, null, null, CancellationToken.None);
        // Second call: finds cached client, sees IsConnected==false, calls ConnectAsync again
        await _registry.GetOrOpenAsync(connection, null, null, CancellationToken.None);

        // Factory still only called once (client was cached), but ConnectAsync called twice
        _factory.Received(1).Create(connection, Arg.Any<byte[]?>(), Arg.Any<byte[]?>());
        await mockClient.Received(2).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrOpenAsync_DifferentConnections_CreatesSeparateClients()
    {
        var conn1 = CreateConnection();
        var conn2 = CreateConnection();

        var mockClient1 = Substitute.For<ITransferClient>();
        mockClient1.IsConnected.Returns(true);
        var mockClient2 = Substitute.For<ITransferClient>();
        mockClient2.IsConnected.Returns(true);

        _factory.Create(conn1, Arg.Any<byte[]?>(), Arg.Any<byte[]?>()).Returns(mockClient1);
        _factory.Create(conn2, Arg.Any<byte[]?>(), Arg.Any<byte[]?>()).Returns(mockClient2);

        var client1 = await _registry.GetOrOpenAsync(conn1, null, null, CancellationToken.None);
        var client2 = await _registry.GetOrOpenAsync(conn2, null, null, CancellationToken.None);

        client1.ShouldBe(mockClient1);
        client2.ShouldBe(mockClient2);
        client1.ShouldNotBe(client2);
    }

    [Fact]
    public async Task DisposeAsync_DisconnectsAndDisposesAllSessions()
    {
        var conn1 = CreateConnection();
        var conn2 = CreateConnection();

        var mockClient1 = Substitute.For<ITransferClient>();
        mockClient1.IsConnected.Returns(true);
        var mockClient2 = Substitute.For<ITransferClient>();
        mockClient2.IsConnected.Returns(true);

        _factory.Create(conn1, Arg.Any<byte[]?>(), Arg.Any<byte[]?>()).Returns(mockClient1);
        _factory.Create(conn2, Arg.Any<byte[]?>(), Arg.Any<byte[]?>()).Returns(mockClient2);

        await _registry.GetOrOpenAsync(conn1, null, null, CancellationToken.None);
        await _registry.GetOrOpenAsync(conn2, null, null, CancellationToken.None);

        await _registry.DisposeAsync();

        await mockClient1.Received(1).DisconnectAsync();
        await mockClient1.Received(1).DisposeAsync();
        await mockClient2.Received(1).DisconnectAsync();
        await mockClient2.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_ClientThrows_ContinuesDisposingOthers()
    {
        var conn1 = CreateConnection();
        var conn2 = CreateConnection();

        var mockClient1 = Substitute.For<ITransferClient>();
        mockClient1.IsConnected.Returns(true);
        mockClient1.DisconnectAsync().Returns(Task.FromException(new IOException("Network error")));

        var mockClient2 = Substitute.For<ITransferClient>();
        mockClient2.IsConnected.Returns(true);

        _factory.Create(conn1, Arg.Any<byte[]?>(), Arg.Any<byte[]?>()).Returns(mockClient1);
        _factory.Create(conn2, Arg.Any<byte[]?>(), Arg.Any<byte[]?>()).Returns(mockClient2);

        await _registry.GetOrOpenAsync(conn1, null, null, CancellationToken.None);
        await _registry.GetOrOpenAsync(conn2, null, null, CancellationToken.None);

        // Should not throw even if one client's disconnect fails
        await Should.NotThrowAsync(async () => await _registry.DisposeAsync());

        // Second client should still get disconnected and disposed
        await mockClient2.Received(1).DisconnectAsync();
        await mockClient2.Received(1).DisposeAsync();
    }
}

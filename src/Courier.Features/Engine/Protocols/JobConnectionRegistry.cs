using System.Collections.Concurrent;
using Courier.Domain.Entities;
using Courier.Domain.Protocols;

namespace Courier.Features.Engine.Protocols;

public class JobConnectionRegistry : IAsyncDisposable
{
    private readonly ITransferClientFactory _factory;
    private readonly ConcurrentDictionary<Guid, ITransferClient> _sessions = new();

    public JobConnectionRegistry(ITransferClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<ITransferClient> GetOrOpenAsync(
        Connection connection,
        byte[]? decryptedPassword,
        byte[]? sshPrivateKey,
        CancellationToken ct)
    {
        if (_sessions.TryGetValue(connection.Id, out var existing))
        {
            if (existing.IsConnected)
                return existing;
            await existing.ConnectAsync(ct);
            return existing;
        }

        var client = _factory.Create(connection, decryptedPassword, sshPrivateKey);
        await client.ConnectAsync(ct);
        _sessions[connection.Id] = client;
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _sessions.Values)
        {
            try { await client.DisconnectAsync(); }
            catch { /* best-effort cleanup */ }
            await client.DisposeAsync();
        }
        _sessions.Clear();
    }
}

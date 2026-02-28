using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Engine.Steps.Transfer;

public abstract class TransferStepBase : IJobStep
{
    protected readonly CourierDbContext Db;
    protected readonly ICredentialEncryptor Encryptor;
    protected readonly JobConnectionRegistry ConnectionRegistry;

    protected TransferStepBase(CourierDbContext db, ICredentialEncryptor encryptor, JobConnectionRegistry registry)
    {
        Db = db;
        Encryptor = encryptor;
        ConnectionRegistry = registry;
    }

    public abstract string TypeKey { get; }
    protected abstract string ExpectedProtocol { get; }

    public abstract Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct);
    public abstract Task<StepResult> ValidateAsync(StepConfiguration config);

    protected async Task<(ITransferClient? client, StepResult? error)> ResolveClientAsync(StepConfiguration config, CancellationToken ct)
    {
        var connectionId = Guid.Parse(config.GetString("connection_id"));
        var connection = await Db.Connections.FirstOrDefaultAsync(c => c.Id == connectionId, ct);

        if (connection is null)
            return (null, StepResult.Fail($"Connection {connectionId} not found"));

        if (!connection.Protocol.Equals(ExpectedProtocol, StringComparison.OrdinalIgnoreCase))
            return (null, StepResult.Fail($"Connection {connectionId} uses protocol '{connection.Protocol}', expected '{ExpectedProtocol}'"));

        byte[]? password = connection.PasswordEncrypted is not null
            ? System.Text.Encoding.UTF8.GetBytes(Encryptor.Decrypt(connection.PasswordEncrypted))
            : null;

        byte[]? sshKey = null;
        if (connection.SshKeyId.HasValue)
        {
            var key = await Db.SshKeys.FirstOrDefaultAsync(k => k.Id == connection.SshKeyId, ct);
            if (key?.PrivateKeyData is not null)
            {
                var pem = Encryptor.Decrypt(key.PrivateKeyData);
                sshKey = System.Text.Encoding.UTF8.GetBytes(pem);
            }
        }

        var client = await ConnectionRegistry.GetOrOpenAsync(connection, password, sshKey, ct);
        return (client, null);
    }

    protected static string ResolveContextRef(string value, JobContext context)
    {
        if (value.StartsWith("context:"))
        {
            var key = value["context:".Length..];
            return context.TryGet<string>(key, out var resolved) && resolved is not null
                ? resolved
                : throw new InvalidOperationException($"Context reference '{key}' not found");
        }
        return value;
    }

    protected StepResult ValidateRequired(StepConfiguration config, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!config.Has(key))
                return StepResult.Fail($"Missing required config: {key}");
        }
        return StepResult.Ok();
    }
}

using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Features.Engine.Protocols;
using Courier.Infrastructure.Data;

namespace Courier.Features.Engine.Steps.Transfer;

public class FtpsMkdirStep : TransferStepBase
{
    public FtpsMkdirStep(CourierDbContext db, ICredentialEncryptor encryptor, JobConnectionRegistry registry)
        : base(db, encryptor, registry) { }

    public override string TypeKey => "ftps.mkdir";
    protected override string ExpectedProtocol => "ftps";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var (client, error) = await ResolveClientAsync(config, ct);
        if (error is not null) return error;

        var remotePath = config.GetString("remote_path");
        await client!.CreateDirectoryAsync(remotePath, ct);
        return StepResult.Ok(outputs: new() { ["created_directory"] = remotePath });
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
        => Task.FromResult(ValidateRequired(config, "connection_id", "remote_path"));
}

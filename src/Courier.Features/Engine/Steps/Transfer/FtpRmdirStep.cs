using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Features.Engine.Protocols;
using Courier.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Courier.Features.Engine.Steps.Transfer;

public class FtpRmdirStep : TransferStepBase
{
    public FtpRmdirStep(CourierDbContext db, ICredentialEncryptor encryptor, JobConnectionRegistry registry, ILogger<FtpRmdirStep> logger)
        : base(db, encryptor, registry, logger) { }

    public override string TypeKey => "ftp.rmdir";
    protected override string ExpectedProtocol => "ftp";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var (client, error) = await ResolveClientAsync(config, ct);
        if (error is not null) return error;

        var remotePath = config.GetString("remote_path");
        var recursive = config.GetBoolOrDefault("recursive");
        await client!.DeleteDirectoryAsync(remotePath, recursive, ct);
        return StepResult.Ok();
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
        => Task.FromResult(ValidateRequired(config, "connection_id", "remote_path"));
}

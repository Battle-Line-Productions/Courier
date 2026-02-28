using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using Courier.Infrastructure.Data;

namespace Courier.Features.Engine.Steps.Transfer;

public class FtpsUploadStep : TransferStepBase
{
    public FtpsUploadStep(CourierDbContext db, ICredentialEncryptor encryptor, JobConnectionRegistry registry)
        : base(db, encryptor, registry) { }

    public override string TypeKey => "ftps.upload";
    protected override string ExpectedProtocol => "ftps";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var (client, error) = await ResolveClientAsync(config, ct);
        if (error is not null) return error;

        var localPath = ResolveContextRef(config.GetString("local_path"), context);
        var remotePath = config.GetString("remote_path");
        var request = new UploadRequest(
            localPath, remotePath,
            AtomicUpload: config.GetBoolOrDefault("atomic_upload", true),
            AtomicSuffix: config.GetStringOrDefault("atomic_suffix", ".tmp")!,
            ResumePartial: config.GetBoolOrDefault("resume_partial"));

        await client!.UploadAsync(request, progress: null, ct);
        var bytes = File.Exists(localPath) ? new FileInfo(localPath).Length : 0;
        return StepResult.Ok(bytes, new() { ["uploaded_file"] = remotePath });
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
        => Task.FromResult(ValidateRequired(config, "connection_id", "local_path", "remote_path"));
}

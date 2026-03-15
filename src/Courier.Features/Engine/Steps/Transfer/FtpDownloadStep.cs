using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using Courier.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Courier.Features.Engine.Steps.Transfer;

public class FtpDownloadStep : TransferStepBase
{
    public FtpDownloadStep(CourierDbContext db, ICredentialEncryptor encryptor, JobConnectionRegistry registry, ILogger<FtpDownloadStep> logger)
        : base(db, encryptor, registry, logger) { }

    public override string TypeKey => "ftp.download";
    protected override string ExpectedProtocol => "ftp";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var (client, error) = await ResolveClientAsync(config, ct);
        if (error is not null) return error;

        var remotePath = config.GetString("remote_path");
        var localPath = config.GetStringOrDefault("local_path");
        if (localPath is not null)
            localPath = ResolveContextRef(localPath, context);
        else if (context.TryGet<string>("workspace", out var ws))
            localPath = Path.Combine(ws!, Path.GetFileName(remotePath));
        else
            localPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(remotePath));

        var idempotency = config.GetStringOrDefault("idempotency", "overwrite");

        if (idempotency == "skip_if_exists" && File.Exists(localPath))
        {
            return StepResult.Ok(0, new()
            {
                ["downloaded_file"] = localPath,
                ["skipped"] = "true",
                ["reason"] = "file_exists"
            });
        }

        var request = new DownloadRequest(
            remotePath, localPath,
            ResumePartial: config.GetBoolOrDefault("resume_partial"),
            FilePattern: config.GetStringOrDefault("file_pattern", "*")!,
            DeleteAfterDownload: config.GetBoolOrDefault("delete_after_download"));

        await client!.DownloadAsync(request, progress: CreateProgressLogger("download"), ct);
        var bytes = File.Exists(localPath) ? new FileInfo(localPath).Length : 0;
        return StepResult.Ok(bytes, new() { ["downloaded_file"] = localPath });
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
        => Task.FromResult(ValidateRequired(config, "connection_id", "remote_path"));
}

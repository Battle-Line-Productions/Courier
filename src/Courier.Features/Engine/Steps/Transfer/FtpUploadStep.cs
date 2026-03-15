using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using Courier.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Courier.Features.Engine.Steps.Transfer;

public class FtpUploadStep : TransferStepBase
{
    public FtpUploadStep(CourierDbContext db, ICredentialEncryptor encryptor, JobConnectionRegistry registry, ILogger<FtpUploadStep> logger)
        : base(db, encryptor, registry, logger) { }

    public override string TypeKey => "ftp.upload";
    protected override string ExpectedProtocol => "ftp";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var (client, error) = await ResolveClientAsync(config, ct);
        if (error is not null) return error;

        var localPath = ResolveContextRef(config.GetString("local_path"), context);
        var remotePath = config.GetString("remote_path");
        var idempotency = config.GetStringOrDefault("idempotency", "overwrite");

        if (idempotency == "skip_if_exists")
        {
            var remoteDir = Path.GetDirectoryName(remotePath)?.Replace('\\', '/') ?? "/";
            var remoteFileName = Path.GetFileName(remotePath);
            var listing = await client!.ListDirectoryAsync(remoteDir, ct);
            if (listing.Any(f => !f.IsDirectory && f.Name == remoteFileName))
            {
                return StepResult.Ok(0, new()
                {
                    ["uploaded_file"] = remotePath,
                    ["skipped"] = "true",
                    ["reason"] = "file_exists"
                });
            }
        }

        var request = new UploadRequest(
            localPath, remotePath,
            AtomicUpload: config.GetBoolOrDefault("atomic_upload", true),
            AtomicSuffix: config.GetStringOrDefault("atomic_suffix", ".tmp")!,
            ResumePartial: config.GetBoolOrDefault("resume_partial"));

        await client!.UploadAsync(request, progress: CreateProgressLogger("upload"), ct);
        var bytes = File.Exists(localPath) ? new FileInfo(localPath).Length : 0;
        return StepResult.Ok(bytes, new() { ["uploaded_file"] = remotePath });
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
        => Task.FromResult(ValidateRequired(config, "connection_id", "local_path", "remote_path"));
}

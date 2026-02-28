using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using Courier.Infrastructure.Data;

namespace Courier.Features.Engine.Steps.Transfer;

public class SftpDownloadStep : TransferStepBase
{
    public SftpDownloadStep(CourierDbContext db, ICredentialEncryptor encryptor, JobConnectionRegistry registry)
        : base(db, encryptor, registry) { }

    public override string TypeKey => "sftp.download";
    protected override string ExpectedProtocol => "sftp";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var (client, error) = await ResolveClientAsync(config, ct);
        if (error is not null) return error;

        var remotePath = config.GetString("remote_path");
        var localPath = config.GetStringOrDefault("local_path")
            ?? Path.Combine(Path.GetTempPath(), Path.GetFileName(remotePath));

        var request = new DownloadRequest(
            remotePath, localPath,
            ResumePartial: config.GetBoolOrDefault("resume_partial"),
            FilePattern: config.GetStringOrDefault("file_pattern", "*")!,
            DeleteAfterDownload: config.GetBoolOrDefault("delete_after_download"));

        await client!.DownloadAsync(request, progress: null, ct);
        var bytes = File.Exists(localPath) ? new FileInfo(localPath).Length : 0;
        return StepResult.Ok(bytes, new() { ["downloaded_file"] = localPath });
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
        => Task.FromResult(ValidateRequired(config, "connection_id", "remote_path"));
}

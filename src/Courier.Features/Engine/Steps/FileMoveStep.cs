using Courier.Domain.Engine;

namespace Courier.Features.Engine.Steps;

public class FileMoveStep : IJobStep
{
    public string TypeKey => "file.move";

    public Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken)
    {
        var sourcePath = ContextResolver.Resolve(config.GetString("source_path"), context);
        var destPath = ContextResolver.Resolve(config.GetString("destination_path"), context);
        var idempotency = config.GetStringOrDefault("idempotency", "overwrite");

        if (!File.Exists(sourcePath))
        {
            // For move with idempotency: if source is gone but dest exists, the move already completed
            if (idempotency is "skip_if_exists" or "resume" && File.Exists(destPath))
            {
                return Task.FromResult(StepResult.Ok(
                    bytesProcessed: 0,
                    outputs: new Dictionary<string, object>
                    {
                        ["moved_file"] = destPath,
                        ["skipped"] = "true",
                        ["reason"] = "already_complete"
                    }));
            }

            return Task.FromResult(StepResult.Fail($"Source file not found: {sourcePath}"));
        }

        if (idempotency == "skip_if_exists" && File.Exists(destPath))
        {
            return Task.FromResult(StepResult.Ok(
                bytesProcessed: 0,
                outputs: new Dictionary<string, object>
                {
                    ["moved_file"] = destPath,
                    ["skipped"] = "true",
                    ["reason"] = "file_exists"
                }));
        }

        if (idempotency == "resume" && File.Exists(destPath))
        {
            var sourceSize = new FileInfo(sourcePath).Length;
            var destSize = new FileInfo(destPath).Length;
            if (sourceSize == destSize)
            {
                return Task.FromResult(StepResult.Ok(
                    bytesProcessed: 0,
                    outputs: new Dictionary<string, object>
                    {
                        ["moved_file"] = destPath,
                        ["skipped"] = "true",
                        ["reason"] = "already_complete"
                    }));
            }
        }

        var destDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        var fileInfo = new FileInfo(sourcePath);
        var bytes = fileInfo.Length;

        File.Move(sourcePath, destPath, overwrite: true);

        return Task.FromResult(StepResult.Ok(
            bytesProcessed: bytes,
            outputs: new Dictionary<string, object> { ["moved_file"] = destPath }));
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("source_path"))
            return Task.FromResult(StepResult.Fail("Missing required config: source_path"));
        if (!config.Has("destination_path"))
            return Task.FromResult(StepResult.Fail("Missing required config: destination_path"));
        return Task.FromResult(StepResult.Ok());
    }
}

using Courier.Domain.Engine;

namespace Courier.Features.Engine.Steps;

public class FileCopyStep : IJobStep
{
    public string TypeKey => "file.copy";

    public Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken)
    {
        var sourcePath = ContextResolver.Resolve(config.GetString("source_path"), context);
        var destPath = ContextResolver.Resolve(config.GetString("destination_path"), context);
        var idempotency = config.GetStringOrDefault("idempotency", "overwrite");

        // If destination is a directory, append the source filename
        if (Directory.Exists(destPath))
            destPath = Path.Combine(destPath, Path.GetFileName(sourcePath));

        if (!File.Exists(sourcePath))
            return Task.FromResult(StepResult.Fail($"Source file not found: {sourcePath}"));

        if (idempotency == "skip_if_exists" && File.Exists(destPath))
        {
            return Task.FromResult(StepResult.Ok(
                bytesProcessed: 0,
                outputs: new Dictionary<string, object>
                {
                    ["copied_file"] = destPath,
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
                        ["copied_file"] = destPath,
                        ["skipped"] = "true",
                        ["reason"] = "already_complete"
                    }));
            }
        }

        var destDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        File.Copy(sourcePath, destPath, overwrite: true);

        var fileInfo = new FileInfo(destPath);
        return Task.FromResult(StepResult.Ok(
            bytesProcessed: fileInfo.Length,
            outputs: new Dictionary<string, object> { ["copied_file"] = destPath }));
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

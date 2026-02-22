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
        var sourcePath = config.GetString("source_path");
        var destPath = config.GetString("destination_path");

        if (!File.Exists(sourcePath))
            return Task.FromResult(StepResult.Fail($"Source file not found: {sourcePath}"));

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

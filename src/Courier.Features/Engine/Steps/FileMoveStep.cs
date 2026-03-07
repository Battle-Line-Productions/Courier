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

        if (!File.Exists(sourcePath))
            return Task.FromResult(StepResult.Fail($"Source file not found: {sourcePath}"));

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

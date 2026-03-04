using Courier.Domain.Engine;

namespace Courier.Features.Engine.Steps.FileOps;

public class FileDeleteStep : IJobStep
{
    public string TypeKey => "file.delete";

    public Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken)
    {
        var path = ContextResolver.Resolve(config.GetString("path"), context);

        var failIfNotFound = config.GetBoolOrDefault("fail_if_not_found", false);
        var existed = System.IO.File.Exists(path);

        if (!existed && failIfNotFound)
            return Task.FromResult(StepResult.Fail($"File not found: {path}"));

        long bytesProcessed = 0;
        if (existed)
        {
            bytesProcessed = new FileInfo(path).Length;
            System.IO.File.Delete(path);
        }

        return Task.FromResult(StepResult.Ok(
            bytesProcessed,
            new Dictionary<string, object>
            {
                ["deleted_file"] = path,
                ["existed"] = existed,
            }));
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("path"))
            return Task.FromResult(StepResult.Fail("Missing required config: path"));
        return Task.FromResult(StepResult.Ok());
    }
}

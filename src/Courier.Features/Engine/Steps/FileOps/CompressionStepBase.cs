using Courier.Domain.Engine;
using Courier.Features.Engine.Compression;

namespace Courier.Features.Engine.Steps.FileOps;

public abstract class CompressionStepBase : IJobStep
{
    protected readonly CompressionProviderRegistry ProviderRegistry;

    protected CompressionStepBase(CompressionProviderRegistry providerRegistry)
    {
        ProviderRegistry = providerRegistry;
    }

    public abstract string TypeKey { get; }
    public abstract Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct);
    public abstract Task<StepResult> ValidateAsync(StepConfiguration config);

    protected static string ResolveContextRef(string value, JobContext context)
    {
        if (value.StartsWith("context:"))
        {
            var key = value["context:".Length..];
            return context.TryGet<string>(key, out var resolved) && resolved is not null
                ? resolved
                : throw new InvalidOperationException($"Context reference '{key}' not found");
        }
        return value;
    }

    protected static string[] ResolveSourcePaths(StepConfiguration config, JobContext context)
    {
        if (config.Has("source_paths"))
        {
            return config.GetStringArray("source_paths")
                .Select(p => ResolveContextRef(p, context))
                .ToArray();
        }

        var singlePath = ResolveContextRef(config.GetString("source_path"), context);
        return [singlePath];
    }
}

using Courier.Domain.Engine;
using Courier.Features.Engine.Compression;

namespace Courier.Features.Engine.Steps.FileOps;

public class FileZipStep : CompressionStepBase
{
    public FileZipStep(CompressionProviderRegistry providerRegistry) : base(providerRegistry) { }

    public override string TypeKey => "file.zip";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var sourcePaths = ResolveSourcePaths(config, context);
        var outputPath = ResolveContextRef(config.GetString("output_path"), context);
        var password = config.GetStringOrDefault("password");
        var format = config.GetStringOrDefault("format", "zip")!;

        // If output_path is a directory, generate a filename from the source file(s)
        if (Directory.Exists(outputPath))
        {
            var baseName = sourcePaths.Length == 1
                ? Path.GetFileNameWithoutExtension(sourcePaths[0])
                : "archive";
            var extension = format switch
            {
                "tar.gz" or "tgz" => ".tar.gz",
                "tar" => ".tar",
                "gz" or "gzip" => ".gz",
                _ => $".{format}"
            };
            outputPath = Path.Combine(outputPath, baseName + extension);
        }

        // Convert split_max_size_mb (megabytes) to bytes for the compression provider
        long? splitMaxSizeBytes = config.Has("split_max_size_mb")
            ? config.GetLong("split_max_size_mb") * 1024 * 1024
            : null;

        var provider = ProviderRegistry.GetProvider(format);

        var result = await provider.CompressAsync(
            new CompressRequest(sourcePaths, outputPath, password, splitMaxSizeBytes), null, ct);

        if (!result.Success)
            return StepResult.Fail(result.ErrorMessage!);

        var outputs = new Dictionary<string, object> { ["archive_path"] = result.OutputPath };

        if (result.SplitParts is { Count: > 1 })
        {
            outputs["split_parts"] = result.SplitParts;
            outputs["split_count"] = result.SplitParts.Count;
        }

        return StepResult.Ok(result.BytesProcessed, outputs);
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("source_path") && !config.Has("source_paths"))
            return Task.FromResult(StepResult.Fail("Missing required config: source_path or source_paths"));
        if (!config.Has("output_path"))
            return Task.FromResult(StepResult.Fail("Missing required config: output_path"));
        return Task.FromResult(StepResult.Ok());
    }
}

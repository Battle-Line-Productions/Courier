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

        var provider = ProviderRegistry.GetProvider(format);

        var result = await provider.CompressAsync(
            new CompressRequest(sourcePaths, outputPath, password), null, ct);

        return result.Success
            ? StepResult.Ok(result.BytesProcessed, new() { ["archive_path"] = result.OutputPath })
            : StepResult.Fail(result.ErrorMessage!);
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

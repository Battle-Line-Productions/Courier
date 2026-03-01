using Courier.Domain.Engine;
using Courier.Features.Engine.Compression;

namespace Courier.Features.Engine.Steps.FileOps;

public class FileUnzipStep : CompressionStepBase
{
    public FileUnzipStep(CompressionProviderRegistry providerRegistry) : base(providerRegistry) { }

    public override string TypeKey => "file.unzip";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var archivePath = ResolveContextRef(config.GetString("archive_path"), context);
        var outputDirectory = ResolveContextRef(config.GetString("output_directory"), context);
        var password = config.GetStringOrDefault("password");
        var format = config.GetStringOrDefault("format", "zip")!;

        var provider = ProviderRegistry.GetProvider(format);

        var result = await provider.DecompressAsync(
            new DecompressRequest(archivePath, outputDirectory, password), null, ct);

        if (!result.Success)
            return StepResult.Fail(result.ErrorMessage!);

        var outputs = new Dictionary<string, object>
        {
            ["extracted_directory"] = result.OutputPath,
        };

        if (result.ExtractedFiles is not null)
            outputs["extracted_files"] = result.ExtractedFiles;

        return StepResult.Ok(result.BytesProcessed, outputs);
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("archive_path"))
            return Task.FromResult(StepResult.Fail("Missing required config: archive_path"));
        if (!config.Has("output_directory"))
            return Task.FromResult(StepResult.Fail("Missing required config: output_directory"));
        return Task.FromResult(StepResult.Ok());
    }
}

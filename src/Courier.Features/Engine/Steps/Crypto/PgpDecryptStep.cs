using Courier.Domain.Engine;
using Courier.Features.Engine.Crypto;

namespace Courier.Features.Engine.Steps.Crypto;

public class PgpDecryptStep : CryptoStepBase
{
    public PgpDecryptStep(ICryptoProvider cryptoProvider) : base(cryptoProvider) { }

    public override string TypeKey => "pgp.decrypt";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var inputPath = ResolveContextRef(config.GetString("input_path"), context);
        var outputPath = config.GetStringOrDefault("output_path", inputPath + ".dec")!;
        outputPath = ResolveContextRef(outputPath, context);
        var privateKeyId = Guid.Parse(config.GetString("private_key_id"));

        // If output_path is a directory, generate filename from input file
        if (Directory.Exists(outputPath))
            outputPath = Path.Combine(outputPath, Path.GetFileName(inputPath) + ".dec");
        var verifySignature = config.GetBoolOrDefault("verify_signature", false);

        var result = await CryptoProvider.DecryptAsync(
            new DecryptRequest(inputPath, outputPath, privateKeyId, verifySignature), null, ct);

        if (!result.Success)
            return StepResult.Fail(result.ErrorMessage!);

        var outputs = new Dictionary<string, object> { ["decrypted_file"] = result.OutputPath };

        return StepResult.Ok(result.BytesProcessed, outputs);
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("input_path")) return Task.FromResult(StepResult.Fail("Missing required config: input_path"));
        if (!config.Has("private_key_id")) return Task.FromResult(StepResult.Fail("Missing required config: private_key_id"));
        return Task.FromResult(StepResult.Ok());
    }
}

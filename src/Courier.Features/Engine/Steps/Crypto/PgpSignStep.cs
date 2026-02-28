using Courier.Domain.Engine;
using Courier.Features.Engine.Crypto;

namespace Courier.Features.Engine.Steps.Crypto;

public class PgpSignStep : CryptoStepBase
{
    public PgpSignStep(ICryptoProvider cryptoProvider) : base(cryptoProvider) { }

    public override string TypeKey => "pgp.sign";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var inputPath = ResolveContextRef(config.GetString("input_path"), context);
        var modeStr = config.GetStringOrDefault("mode", "detached")!;
        var mode = ParseSignatureMode(modeStr);
        var defaultExt = mode == SignatureMode.Detached ? ".sig" : ".signed";
        var outputPath = config.GetStringOrDefault("output_path", inputPath + defaultExt)!;
        var signingKeyId = Guid.Parse(config.GetString("signing_key_id"));

        var result = await CryptoProvider.SignAsync(
            new SignRequest(inputPath, outputPath, signingKeyId, mode), null, ct);

        return result.Success
            ? StepResult.Ok(result.BytesProcessed, new() { ["signature_file"] = result.OutputPath })
            : StepResult.Fail(result.ErrorMessage!);
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("input_path")) return Task.FromResult(StepResult.Fail("Missing required config: input_path"));
        if (!config.Has("signing_key_id")) return Task.FromResult(StepResult.Fail("Missing required config: signing_key_id"));
        return Task.FromResult(StepResult.Ok());
    }

    private static SignatureMode ParseSignatureMode(string mode) => mode.ToLowerInvariant() switch
    {
        "detached" => SignatureMode.Detached,
        "inline" => SignatureMode.Inline,
        "clearsign" => SignatureMode.Clearsign,
        _ => throw new ArgumentException($"Unknown signature mode: {mode}")
    };
}

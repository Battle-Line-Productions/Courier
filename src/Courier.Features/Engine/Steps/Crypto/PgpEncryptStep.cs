using Courier.Domain.Engine;
using Courier.Features.Engine.Crypto;

namespace Courier.Features.Engine.Steps.Crypto;

public class PgpEncryptStep : CryptoStepBase
{
    public PgpEncryptStep(ICryptoProvider cryptoProvider) : base(cryptoProvider) { }

    public override string TypeKey => "pgp.encrypt";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var inputPath = ResolveContextRef(config.GetString("input_path"), context);
        var outputPath = config.GetStringOrDefault("output_path", inputPath + ".pgp")!;
        var recipientIds = config.GetStringArray("recipient_key_ids").Select(Guid.Parse).ToList();
        var signingKeyId = config.Has("signing_key_id") ? Guid.Parse(config.GetString("signing_key_id")) : (Guid?)null;
        var format = config.GetStringOrDefault("output_format", "binary") == "armored" ? OutputFormat.Armored : OutputFormat.Binary;

        var result = await CryptoProvider.EncryptAsync(
            new EncryptRequest(inputPath, outputPath, recipientIds, signingKeyId, format), null, ct);

        return result.Success
            ? StepResult.Ok(result.BytesProcessed, new() { ["encrypted_file"] = result.OutputPath })
            : StepResult.Fail(result.ErrorMessage!);
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("input_path")) return Task.FromResult(StepResult.Fail("Missing required config: input_path"));
        if (config.GetStringArray("recipient_key_ids").Length == 0)
            return Task.FromResult(StepResult.Fail("Missing required config: recipient_key_ids"));
        return Task.FromResult(StepResult.Ok());
    }
}

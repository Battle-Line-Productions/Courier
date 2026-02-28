using Courier.Domain.Engine;
using Courier.Features.Engine.Crypto;

namespace Courier.Features.Engine.Steps.Crypto;

public class PgpVerifyStep : CryptoStepBase
{
    public PgpVerifyStep(ICryptoProvider cryptoProvider) : base(cryptoProvider) { }

    public override string TypeKey => "pgp.verify";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var inputPath = ResolveContextRef(config.GetString("input_path"), context);
        var detachedSignaturePath = config.Has("detached_signature_path")
            ? config.GetString("detached_signature_path")
            : null;
        var expectedSignerKeyId = config.Has("expected_signer_key_id")
            ? Guid.Parse(config.GetString("expected_signer_key_id"))
            : (Guid?)null;

        var result = await CryptoProvider.VerifyAsync(
            new VerifyRequest(inputPath, detachedSignaturePath, expectedSignerKeyId), null, ct);

        var outputs = new Dictionary<string, object>
        {
            ["verify_status"] = result.Status.ToString(),
            ["is_valid"] = result.IsValid
        };

        if (result.SignerFingerprint is not null)
            outputs["signer_fingerprint"] = result.SignerFingerprint;

        if (result.SignatureTimestamp.HasValue)
            outputs["signature_timestamp"] = result.SignatureTimestamp.Value.ToString("O");

        return StepResult.Ok(0, outputs);
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("input_path")) return Task.FromResult(StepResult.Fail("Missing required config: input_path"));
        return Task.FromResult(StepResult.Ok());
    }
}

using Courier.Domain.Engine;
using Courier.Features.Engine.Crypto;

namespace Courier.Features.Engine.Steps.Crypto;

public abstract class CryptoStepBase : IJobStep
{
    protected readonly ICryptoProvider CryptoProvider;

    protected CryptoStepBase(ICryptoProvider cryptoProvider)
    {
        CryptoProvider = cryptoProvider;
    }

    public abstract string TypeKey { get; }
    public abstract Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct);
    public abstract Task<StepResult> ValidateAsync(StepConfiguration config);

    protected static string ResolveContextRef(string value, JobContext context)
        => ContextResolver.Resolve(value, context);
}

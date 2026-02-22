using Courier.Domain.Engine;

namespace Courier.Features.Engine;

public class StepTypeRegistry
{
    private readonly Dictionary<string, IJobStep> _steps;

    public StepTypeRegistry(IEnumerable<IJobStep> steps)
    {
        _steps = steps.ToDictionary(s => s.TypeKey, StringComparer.OrdinalIgnoreCase);
    }

    public IJobStep Resolve(string typeKey)
        => _steps.TryGetValue(typeKey, out var step)
            ? step
            : throw new KeyNotFoundException($"No step handler registered for type key '{typeKey}'. Registered: [{string.Join(", ", _steps.Keys)}]");

    public IEnumerable<string> GetRegisteredTypes() => _steps.Keys;
}

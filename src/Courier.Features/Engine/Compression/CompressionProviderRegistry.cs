namespace Courier.Features.Engine.Compression;

public class CompressionProviderRegistry
{
    private readonly Dictionary<string, ICompressionProvider> _providers;

    public CompressionProviderRegistry(IEnumerable<ICompressionProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.FormatKey, StringComparer.OrdinalIgnoreCase);
    }

    public ICompressionProvider GetProvider(string formatKey)
    {
        if (_providers.TryGetValue(formatKey, out var provider))
            return provider;

        throw new InvalidOperationException(
            $"No compression provider registered for format '{formatKey}'. Available: {string.Join(", ", _providers.Keys)}");
    }
}

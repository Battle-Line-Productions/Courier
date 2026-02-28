using System.Text.Json;

namespace Courier.Domain.Engine;

public class StepConfiguration
{
    private readonly JsonElement _root;

    public StepConfiguration(string json)
    {
        _root = JsonDocument.Parse(json).RootElement;
    }

    public string GetString(string key)
        => _root.GetProperty(key).GetString()
           ?? throw new InvalidOperationException($"Step config key '{key}' is null.");

    public string? GetStringOrDefault(string key, string? defaultValue = null)
        => _root.TryGetProperty(key, out var prop) ? prop.GetString() : defaultValue;

    public int GetInt(string key)
        => _root.GetProperty(key).GetInt32();

    public bool GetBool(string key)
        => _root.GetProperty(key).GetBoolean();

    public bool GetBoolOrDefault(string key, bool defaultValue = false)
        => _root.TryGetProperty(key, out var prop) ? prop.GetBoolean() : defaultValue;

    public int GetIntOrDefault(string key, int defaultValue = 0)
        => _root.TryGetProperty(key, out var prop) ? prop.GetInt32() : defaultValue;

    public string[] GetStringArray(string key)
    {
        if (!_root.TryGetProperty(key, out var prop))
            return [];
        return prop.EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    public bool Has(string key)
        => _root.TryGetProperty(key, out _);

    public string Raw => _root.GetRawText();
}

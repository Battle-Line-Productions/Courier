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
        => FindProperty(key).GetString()
           ?? throw new InvalidOperationException($"Step config key '{key}' is null.");

    public string? GetStringOrDefault(string key, string? defaultValue = null)
        => TryFindProperty(key, out var prop) ? prop.GetString() : defaultValue;

    public int GetInt(string key)
        => FindProperty(key).GetInt32();

    public bool GetBool(string key)
        => FindProperty(key).GetBoolean();

    public bool GetBoolOrDefault(string key, bool defaultValue = false)
        => TryFindProperty(key, out var prop) ? prop.GetBoolean() : defaultValue;

    public int GetIntOrDefault(string key, int defaultValue = 0)
        => TryFindProperty(key, out var prop) ? prop.GetInt32() : defaultValue;

    public long GetLong(string key)
        => FindProperty(key).GetInt64();

    public long GetLongOrDefault(string key, long defaultValue = 0)
        => TryFindProperty(key, out var prop) ? prop.GetInt64() : defaultValue;

    public string[] GetStringArray(string key)
    {
        if (!TryFindProperty(key, out var prop))
            return [];
        return prop.EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    public bool Has(string key)
        => TryFindProperty(key, out _);

    public string Raw => _root.GetRawText();

    /// <summary>
    /// Finds a property by snake_case key, falling back to camelCase equivalent.
    /// This handles configs saved with either convention.
    /// </summary>
    private JsonElement FindProperty(string key)
    {
        if (_root.TryGetProperty(key, out var prop))
            return prop;

        var camelKey = SnakeToCamel(key);
        if (camelKey != key && _root.TryGetProperty(camelKey, out prop))
            return prop;

        throw new KeyNotFoundException($"Step config key '{key}' not found. Available keys: {string.Join(", ", EnumerateKeys())}");
    }

    private bool TryFindProperty(string key, out JsonElement prop)
    {
        if (_root.TryGetProperty(key, out prop))
            return true;

        var camelKey = SnakeToCamel(key);
        if (camelKey != key && _root.TryGetProperty(camelKey, out prop))
            return true;

        prop = default;
        return false;
    }

    private IEnumerable<string> EnumerateKeys()
        => _root.EnumerateObject().Select(p => p.Name);

    private static string SnakeToCamel(string snake)
    {
        var parts = snake.Split('_');
        if (parts.Length <= 1)
            return snake;

        return parts[0] + string.Concat(parts.Skip(1).Select(p =>
            p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
    }
}

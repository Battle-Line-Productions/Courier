namespace Courier.Domain.Engine;

public class JobContext
{
    private readonly Dictionary<string, object> _data = new();

    public void Set<T>(string key, T value) where T : notnull
        => _data[key] = value;

    public T Get<T>(string key)
        => (T)_data[key];

    public bool TryGet<T>(string key, out T? value)
    {
        if (_data.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public IReadOnlyDictionary<string, object> Snapshot()
        => new Dictionary<string, object>(_data);

    public static JobContext Restore(IDictionary<string, object> data)
    {
        var ctx = new JobContext();
        foreach (var kvp in data)
            ctx._data[kvp.Key] = kvp.Value;
        return ctx;
    }
}

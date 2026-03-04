namespace Courier.Domain.Engine;

public class JobContext
{
    private readonly Dictionary<string, object> _data = new();
    private readonly Stack<LoopScope> _loopStack = new();

    public int LoopDepth => _loopStack.Count;
    public int? CurrentIterationIndex => _loopStack.Count > 0 ? _loopStack.Peek().CurrentIndex : null;

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

    public void PushLoopScope(object currentItem, int index, int totalItems)
    {
        var depth = _loopStack.Count;

        // If nested, save outer loop's magic keys with depth prefix
        if (depth > 0)
        {
            if (_data.TryGetValue("loop.current_item", out var outerItem))
                _data[$"loop.{depth - 1}.current_item"] = outerItem;
            if (_data.TryGetValue("loop.index", out var outerIndex))
                _data[$"loop.{depth - 1}.index"] = outerIndex;
        }

        _data["loop.current_item"] = currentItem;
        _data["loop.index"] = index;
        _loopStack.Push(new LoopScope(depth, index, totalItems));
    }

    public void PopLoopScope()
    {
        if (_loopStack.Count == 0)
            throw new InvalidOperationException("No active loop scope to pop.");

        _loopStack.Pop();
        var depth = _loopStack.Count;

        // Remove current magic keys
        _data.Remove("loop.current_item");
        _data.Remove("loop.index");

        // Restore outer loop's magic keys if we're still inside a loop
        if (depth > 0)
        {
            var outerDepth = depth - 1;
            if (_data.TryGetValue($"loop.{outerDepth}.current_item", out var outerItem))
            {
                _data["loop.current_item"] = outerItem;
                _data.Remove($"loop.{outerDepth}.current_item");
            }
            if (_data.TryGetValue($"loop.{outerDepth}.index", out var outerIndex))
            {
                _data["loop.index"] = outerIndex;
                _data.Remove($"loop.{outerDepth}.index");
            }
        }
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

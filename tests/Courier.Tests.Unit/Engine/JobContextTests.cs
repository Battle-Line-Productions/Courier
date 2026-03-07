using Courier.Domain.Engine;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class JobContextTests
{
    [Fact]
    public void Set_And_Get_ReturnsValue()
    {
        var ctx = new JobContext();
        ctx.Set("0.downloaded_file", "/tmp/file.txt");
        ctx.Get<string>("0.downloaded_file").ShouldBe("/tmp/file.txt");
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var ctx = new JobContext();
        ctx.TryGet<string>("missing", out var value).ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void Snapshot_ReturnsAllEntries()
    {
        var ctx = new JobContext();
        ctx.Set("a", 1);
        ctx.Set("b", "two");
        var snap = ctx.Snapshot();
        snap.Count.ShouldBe(2);
        snap["a"].ShouldBe(1);
        snap["b"].ShouldBe("two");
    }

    [Fact]
    public void Snapshot_IsReadOnly_OriginalUnaffected()
    {
        var ctx = new JobContext();
        ctx.Set("a", 1);
        var snap = ctx.Snapshot();
        ctx.Set("b", 2);
        snap.Count.ShouldBe(1);
    }

    [Fact]
    public void Restore_PopulatesFromDictionary()
    {
        var data = new Dictionary<string, object> { ["x"] = "hello" };
        var ctx = JobContext.Restore(data);
        ctx.Get<string>("x").ShouldBe("hello");
    }

    [Fact]
    public void SnapshotRestore_Roundtrip_PreservesAllKeys()
    {
        var ctx = new JobContext();
        ctx.Set("a", "alpha");
        ctx.Set("b", 42);
        ctx.Set("c", true);

        var snapshot = ctx.Snapshot().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var restored = JobContext.Restore(snapshot);

        restored.Get<string>("a").ShouldBe("alpha");
        restored.Get<int>("b").ShouldBe(42);
        restored.Get<bool>("c").ShouldBeTrue();
    }

    [Fact]
    public void SnapshotRestore_WithJsonElementValues_PreservesTypes()
    {
        var ctx = new JobContext();
        var json = System.Text.Json.JsonDocument.Parse("""{"name": "test"}""").RootElement;
        ctx.Set("data", json);

        var snapshot = ctx.Snapshot().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var restored = JobContext.Restore(snapshot);

        // Object reference is preserved through restore
        var element = restored.Get<System.Text.Json.JsonElement>("data");
        element.GetProperty("name").GetString().ShouldBe("test");
    }

    [Fact]
    public void Restore_EmptyDictionary_CreatesEmptyContext()
    {
        var ctx = JobContext.Restore(new Dictionary<string, object>());
        ctx.Snapshot().Count.ShouldBe(0);
    }

    [Fact]
    public void Restore_NullValues_HandledGracefully()
    {
        var data = new Dictionary<string, object> { ["key"] = null! };
        var ctx = JobContext.Restore(data);

        // Restore bypasses Set<T>'s notnull constraint, so null is stored.
        // TryGet uses pattern matching (raw is T typed) which returns false for null.
        ctx.TryGet<object>("key", out _).ShouldBeFalse();

        // But the key does exist in the snapshot
        ctx.Snapshot().ContainsKey("key").ShouldBeTrue();
    }
}

using System.Text.Json;
using Courier.Domain.Engine;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class JobContextSnapshotTests
{
    // ── Snapshot with loop scope ───────────────────────────────────────

    [Fact]
    public void Snapshot_WithLoopScope_IncludesLoopKeys()
    {
        var context = new JobContext();
        context.Set("step1.output", "file.txt");
        context.PushLoopScope("item_a", 0, 3);

        var snapshot = context.Snapshot();

        snapshot.ShouldContainKey("loop.current_item");
        snapshot["loop.current_item"].ShouldBe("item_a");
        snapshot.ShouldContainKey("loop.index");
        snapshot["loop.index"].ShouldBe(0);
        snapshot.ShouldContainKey("step1.output");
    }

    [Fact]
    public void Snapshot_AfterPopLoopScope_ExcludesLoopKeys()
    {
        var context = new JobContext();
        context.Set("step1.output", "file.txt");
        context.PushLoopScope("item_a", 0, 3);
        context.PopLoopScope();

        var snapshot = context.Snapshot();

        snapshot.ShouldNotContainKey("loop.current_item");
        snapshot.ShouldNotContainKey("loop.index");
        snapshot.ShouldContainKey("step1.output");
    }

    // ── Large context ─────────────────────────────────────────────────

    [Fact]
    public void Snapshot_LargeContext_100Keys_AllPreserved()
    {
        var context = new JobContext();
        for (var i = 0; i < 100; i++)
            context.Set($"key_{i}", $"value_{i}");

        var snapshot = context.Snapshot().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var restored = JobContext.Restore(snapshot);

        for (var i = 0; i < 100; i++)
            restored.Get<string>($"key_{i}").ShouldBe($"value_{i}");
    }

    // ── Restore then Set ──────────────────────────────────────────────

    [Fact]
    public void Restore_ThenSet_DoesNotCorruptExistingKeys()
    {
        var original = new Dictionary<string, object>
        {
            ["existing"] = "old_value",
            ["keep"] = "preserved",
        };
        var context = JobContext.Restore(original);

        context.Set("new_key", "new_value");
        context.Set("existing", "updated_value");

        context.Get<string>("keep").ShouldBe("preserved");
        context.Get<string>("existing").ShouldBe("updated_value");
        context.Get<string>("new_key").ShouldBe("new_value");
    }

    // ── Snapshot/Restore with JSON serialization roundtrip ─────────────

    [Fact]
    public void SnapshotRestore_WithJsonSerialization_PreservesStringValues()
    {
        var context = new JobContext();
        context.Set("path", "/tmp/file.txt");
        context.Set("status", "completed");

        var snapshot = context.Snapshot();
        var json = JsonSerializer.Serialize(snapshot);
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;

        // After JSON roundtrip, values become JsonElement
        var restored = JobContext.Restore(deserialized);
        var restoredSnapshot = restored.Snapshot();

        restoredSnapshot.ShouldContainKey("path");
        restoredSnapshot.ShouldContainKey("status");

        // Values survive as JsonElement after deserialization
        restored.TryGet<JsonElement>("path", out var pathElement).ShouldBeTrue();
        pathElement.GetString().ShouldBe("/tmp/file.txt");
    }

    // ── Restore with mixed types ──────────────────────────────────────

    [Fact]
    public void Restore_WithMixedTypes_PreservesAllValues()
    {
        var jsonElement = JsonSerializer.SerializeToElement(new { name = "test" });
        var original = new Dictionary<string, object>
        {
            ["str"] = "hello",
            ["num"] = 42,
            ["flag"] = true,
            ["json"] = jsonElement,
        };

        var context = JobContext.Restore(original);

        context.Get<string>("str").ShouldBe("hello");
        context.Get<int>("num").ShouldBe(42);
        context.Get<bool>("flag").ShouldBeTrue();
        context.Get<JsonElement>("json").GetProperty("name").GetString().ShouldBe("test");
    }

    // ── Snapshot is a copy, not a reference ───────────────────────────

    [Fact]
    public void Snapshot_ReturnsIndependentCopy()
    {
        var context = new JobContext();
        context.Set("key", "original");

        var snapshot = context.Snapshot();

        // Mutating context after snapshot should not affect snapshot
        context.Set("key", "modified");
        context.Set("new_key", "added");

        snapshot["key"].ShouldBe("original");
        snapshot.ShouldNotContainKey("new_key");
    }

    // ── Restore creates independent context ───────────────────────────

    [Fact]
    public void Restore_CreatesIndependentContext()
    {
        var data = new Dictionary<string, object> { ["key"] = "value" };
        var context = JobContext.Restore(data);

        // Mutating original dict should not affect restored context
        data["key"] = "mutated";
        data["extra"] = "added";

        context.Get<string>("key").ShouldBe("value");
        context.TryGet<string>("extra", out _).ShouldBeFalse();
    }
}

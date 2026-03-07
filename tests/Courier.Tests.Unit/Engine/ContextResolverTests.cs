using System.Text.Json;
using Courier.Domain.Engine;
using Courier.Features.Engine;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class ContextResolverTests
{
    // ── Literal passthrough ────────────────────────────────────────────

    [Fact]
    public void Resolve_LiteralValue_ReturnsAsIs()
    {
        var context = new JobContext();
        ContextResolver.Resolve("/path/to/file.txt", context).ShouldBe("/path/to/file.txt");
    }

    [Fact]
    public void Resolve_EmptyString_ReturnsEmpty()
    {
        var context = new JobContext();
        ContextResolver.Resolve("", context).ShouldBe("");
    }

    // ── Context references ─────────────────────────────────────────────

    [Fact]
    public void Resolve_ContextRef_String_ResolvesValue()
    {
        var context = new JobContext();
        context.Set("1.downloaded_file", "/tmp/file.txt");

        ContextResolver.Resolve("context:1.downloaded_file", context).ShouldBe("/tmp/file.txt");
    }

    [Fact]
    public void Resolve_ContextRef_Integer_ResolvesToString()
    {
        var context = new JobContext();
        context.Set("0.file_count", 42);

        ContextResolver.Resolve("context:0.file_count", context).ShouldBe("42");
    }

    [Fact]
    public void Resolve_ContextRef_MissingKey_ThrowsInvalidOperationException()
    {
        var context = new JobContext();

        Should.Throw<InvalidOperationException>(
            () => ContextResolver.Resolve("context:missing_key", context));
    }

    // ── JsonElement values ─────────────────────────────────────────────

    [Fact]
    public void Resolve_ContextRef_JsonElementString_ResolvesValue()
    {
        var context = new JobContext();
        var json = JsonDocument.Parse("\"hello\"").RootElement;
        context.Set("val", json);

        ContextResolver.Resolve("context:val", context).ShouldBe("hello");
    }

    [Fact]
    public void Resolve_ContextRef_JsonElementNumber_ResolvesRawText()
    {
        var context = new JobContext();
        var json = JsonDocument.Parse("1048576").RootElement;
        context.Set("size", json);

        ContextResolver.Resolve("context:size", context).ShouldBe("1048576");
    }

    // ── Nested property access ─────────────────────────────────────────

    [Fact]
    public void Resolve_ContextRef_JsonObjectProperty_ResolvesNestedValue()
    {
        var context = new JobContext();
        var json = JsonDocument.Parse("""{"name": "report.csv", "size": 1024}""").RootElement;
        context.Set("loop.current_item", json);

        ContextResolver.Resolve("context:loop.current_item.name", context).ShouldBe("report.csv");
        ContextResolver.Resolve("context:loop.current_item.size", context).ShouldBe("1024");
    }

    [Fact]
    public void Resolve_ContextRef_DictionaryProperty_ResolvesNestedValue()
    {
        var context = new JobContext();
        var dict = new Dictionary<string, object> { ["name"] = "report.csv" };
        context.Set("loop.current_item", dict);

        ContextResolver.Resolve("context:loop.current_item.name", context).ShouldBe("report.csv");
    }

    // ── Loop magic keys ────────────────────────────────────────────────

    [Fact]
    public void Resolve_LoopCurrentItem_ResolvedAfterPush()
    {
        var context = new JobContext();
        var item = JsonDocument.Parse("""{"file": "test.csv"}""").RootElement;
        context.PushLoopScope(item, 0, 3);

        ContextResolver.Resolve("context:loop.current_item.file", context).ShouldBe("test.csv");

        context.PopLoopScope();
    }

    [Fact]
    public void Resolve_LoopIndex_ResolvedAfterPush()
    {
        var context = new JobContext();
        context.PushLoopScope("item", 2, 5);

        ContextResolver.Resolve("context:loop.index", context).ShouldBe("2");

        context.PopLoopScope();
    }

    // ── TryResolve ─────────────────────────────────────────────────────

    [Fact]
    public void TryResolve_LiteralValue_ReturnsTrue()
    {
        var context = new JobContext();
        ContextResolver.TryResolve("/path", context, out var result).ShouldBeTrue();
        result.ShouldBe("/path");
    }

    [Fact]
    public void TryResolve_MissingContextRef_ReturnsFalse()
    {
        var context = new JobContext();
        ContextResolver.TryResolve("context:missing", context, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryResolve_ValidContextRef_ReturnsTrue()
    {
        var context = new JobContext();
        context.Set("key", "value");
        ContextResolver.TryResolve("context:key", context, out var result).ShouldBeTrue();
        result.ShouldBe("value");
    }

    [Fact]
    public void Resolve_ContextRef_EmptyKeyAfterPrefix_ThrowsInvalidOperationException()
    {
        var context = new JobContext();
        Should.Throw<InvalidOperationException>(
            () => ContextResolver.Resolve("context:", context));
    }

    [Fact]
    public void Resolve_ContextRef_WhitespaceKey_ThrowsInvalidOperationException()
    {
        var context = new JobContext();
        Should.Throw<InvalidOperationException>(
            () => ContextResolver.Resolve("context: ", context));
    }
}

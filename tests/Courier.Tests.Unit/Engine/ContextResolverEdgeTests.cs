using System.Text.Json;
using Courier.Domain.Engine;
using Courier.Features.Engine;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class ContextResolverEdgeTests
{
    // ── Object value resolution ───────────────────────────────────────

    [Fact]
    public void Resolve_ContextRef_ObjectValue_ReturnsToString()
    {
        var context = new JobContext();
        context.Set("count", 42);

        var result = ContextResolver.Resolve("context:count", context);

        result.ShouldBe("42");
    }

    [Fact]
    public void Resolve_ContextRef_BoolValue_ReturnsString()
    {
        var context = new JobContext();
        context.Set("flag", true);

        var result = ContextResolver.Resolve("context:flag", context);

        result.ShouldBe("True");
    }

    // ── Nested JSON property edge cases ───────────────────────────────

    [Fact]
    public void Resolve_ContextRef_NestedJsonProperty_MissingProperty_Throws()
    {
        var context = new JobContext();
        var json = JsonSerializer.SerializeToElement(new { name = "test" });
        context.Set("item", json);

        // "item.nonexistent" — JsonElement has no "nonexistent" property,
        // falls through all branches, TryResolveKey returns false, Resolve throws
        Should.Throw<InvalidOperationException>(
            () => ContextResolver.Resolve("context:item.nonexistent", context));
    }

    [Fact]
    public void Resolve_ContextRef_JsonElementBool_ReturnsRawText()
    {
        var context = new JobContext();
        var json = JsonSerializer.SerializeToElement(new { enabled = true });
        context.Set("config", json);

        var result = ContextResolver.Resolve("context:config.enabled", context);

        result.ShouldBe("true");
    }

    [Fact]
    public void Resolve_ContextRef_JsonElementNumber_ReturnsRawText()
    {
        var context = new JobContext();
        var json = JsonSerializer.SerializeToElement(new { port = 8080 });
        context.Set("settings", json);

        var result = ContextResolver.Resolve("context:settings.port", context);

        result.ShouldBe("8080");
    }

    // ── TryResolve edge cases ─────────────────────────────────────────

    [Fact]
    public void TryResolve_ContextRef_ObjectValue_ReturnsTrue()
    {
        var context = new JobContext();
        context.Set("size", 1024L);

        var success = ContextResolver.TryResolve("context:size", context, out var resolved);

        success.ShouldBeTrue();
        resolved.ShouldBe("1024");
    }

    [Fact]
    public void TryResolve_NonContextPrefix_ReturnsOriginalValue()
    {
        var context = new JobContext();

        var success = ContextResolver.TryResolve("plain-value", context, out var resolved);

        success.ShouldBeTrue();
        resolved.ShouldBe("plain-value");
    }

    [Fact]
    public void TryResolve_MissingKey_ReturnsFalse()
    {
        var context = new JobContext();

        var success = ContextResolver.TryResolve("context:missing.key", context, out var resolved);

        success.ShouldBeFalse();
        resolved.ShouldBe(string.Empty);
    }

    // ── Dictionary nested property access ─────────────────────────────

    [Fact]
    public void Resolve_ContextRef_DictionaryProperty_ReturnsValue()
    {
        var context = new JobContext();
        var dict = new Dictionary<string, object> { ["host"] = "localhost", ["port"] = 5432 };
        context.Set("db", dict);

        var result = ContextResolver.Resolve("context:db.host", context);

        result.ShouldBe("localhost");
    }

    [Fact]
    public void Resolve_ContextRef_DictionaryProperty_NullValue_ReturnsEmpty()
    {
        var context = new JobContext();
        var dict = new Dictionary<string, object> { ["key"] = null! };
        context.Set("data", dict);

        var result = ContextResolver.Resolve("context:data.key", context);

        result.ShouldBe(string.Empty);
    }

    // ── JsonElement stored directly as value ──────────────────────────

    [Fact]
    public void Resolve_ContextRef_DirectJsonElementString_ReturnsStringValue()
    {
        var context = new JobContext();
        var json = JsonSerializer.SerializeToElement("hello");
        context.Set("greeting", json);

        var result = ContextResolver.Resolve("context:greeting", context);

        result.ShouldBe("hello");
    }

    [Fact]
    public void Resolve_ContextRef_DirectJsonElementObject_ReturnsRawText()
    {
        var context = new JobContext();
        var json = JsonSerializer.SerializeToElement(new { a = 1 });
        context.Set("obj", json);

        var result = ContextResolver.Resolve("context:obj", context);

        result.ShouldBe("{\"a\":1}");
    }
}

using Courier.Functions.Sdk;
using Shouldly;

namespace Courier.Tests.Unit.Sdk;

public class CourierCallbackTests
{
    [Fact]
    public void FromBody_WithCallbackInfo_HasCallbackIsTrue()
    {
        var body = """
        {
            "payload": {"key": "value"},
            "callback": {
                "url": "https://courier.test/api/v1/callbacks/abc123",
                "key": "secret-key"
            }
        }
        """;

        var cb = CourierCallback.FromBody(body);

        cb.HasCallback.ShouldBeTrue();
        cb.Payload.ShouldNotBeNull();
        cb.Payload!.Value.GetProperty("key").GetString().ShouldBe("value");
    }

    [Fact]
    public void FromBody_WithoutCallbackInfo_HasCallbackIsFalse()
    {
        var body = """{"key": "value"}""";

        var cb = CourierCallback.FromBody(body);

        cb.HasCallback.ShouldBeFalse();
        cb.Payload.ShouldNotBeNull();
    }

    [Fact]
    public void FromBody_EmptyBody_HasCallbackIsFalse()
    {
        var cb = CourierCallback.FromBody("");

        cb.HasCallback.ShouldBeFalse();
        cb.Payload.ShouldBeNull();
    }

    [Fact]
    public void FromBody_MalformedJson_HasCallbackIsFalse()
    {
        var cb = CourierCallback.FromBody("not json");

        cb.HasCallback.ShouldBeFalse();
        cb.Payload.ShouldBeNull();
    }

    [Fact]
    public void FromBody_NullPayload_PayloadIsNull()
    {
        var body = """
        {
            "callback": {
                "url": "https://courier.test/api/v1/callbacks/abc123",
                "key": "secret-key"
            }
        }
        """;

        var cb = CourierCallback.FromBody(body);

        cb.HasCallback.ShouldBeTrue();
        cb.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task SuccessAsync_NoCallback_IsNoOp()
    {
        var cb = CourierCallback.FromBody("{}");

        // Should not throw
        await cb.SuccessAsync(new { count = 42 });
    }

    [Fact]
    public async Task FailAsync_NoCallback_IsNoOp()
    {
        var cb = CourierCallback.FromBody("{}");

        // Should not throw
        await cb.FailAsync("some error");
    }
}

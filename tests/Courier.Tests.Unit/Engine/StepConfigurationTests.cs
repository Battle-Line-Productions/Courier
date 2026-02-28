using Courier.Domain.Engine;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class StepConfigurationTests
{
    [Fact]
    public void GetBoolOrDefault_MissingKey_ReturnsDefault()
    {
        var config = new StepConfiguration("{}");
        config.GetBoolOrDefault("missing", true).ShouldBeTrue();
    }

    [Fact]
    public void GetBoolOrDefault_PresentKey_ReturnsValue()
    {
        var config = new StepConfiguration("""{"flag": false}""");
        config.GetBoolOrDefault("flag", true).ShouldBeFalse();
    }

    [Fact]
    public void GetIntOrDefault_MissingKey_ReturnsDefault()
    {
        var config = new StepConfiguration("{}");
        config.GetIntOrDefault("missing", 42).ShouldBe(42);
    }

    [Fact]
    public void GetIntOrDefault_PresentKey_ReturnsValue()
    {
        var config = new StepConfiguration("""{"count": 7}""");
        config.GetIntOrDefault("count", 42).ShouldBe(7);
    }

    [Fact]
    public void GetStringArray_ReturnsValues()
    {
        var config = new StepConfiguration("""{"ids": ["aaa", "bbb"]}""");
        var result = config.GetStringArray("ids");
        result.ShouldBe(new[] { "aaa", "bbb" });
    }

    [Fact]
    public void GetStringArray_MissingKey_ReturnsEmpty()
    {
        var config = new StepConfiguration("{}");
        config.GetStringArray("ids").ShouldBeEmpty();
    }
}

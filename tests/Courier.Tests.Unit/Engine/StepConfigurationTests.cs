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

    [Fact]
    public void GetString_PresentKey_ReturnsValue()
    {
        var config = new StepConfiguration("""{"name": "report.csv"}""");
        config.GetString("name").ShouldBe("report.csv");
    }

    [Fact]
    public void GetString_MissingKey_ThrowsKeyNotFoundException()
    {
        var config = new StepConfiguration("{}");
        Should.Throw<KeyNotFoundException>(() => config.GetString("missing"));
    }

    [Fact]
    public void GetBool_PresentKey_ReturnsValue()
    {
        var config = new StepConfiguration("""{"enabled": true}""");
        config.GetBool("enabled").ShouldBeTrue();
    }

    [Fact]
    public void GetInt_PresentKey_ReturnsValue()
    {
        var config = new StepConfiguration("""{"port": 8080}""");
        config.GetInt("port").ShouldBe(8080);
    }

    [Fact]
    public void GetLong_PresentKey_ReturnsValue()
    {
        var config = new StepConfiguration("""{"size": 9876543210}""");
        config.GetLong("size").ShouldBe(9876543210L);
    }

    [Fact]
    public void GetLongOrDefault_MissingKey_ReturnsDefault()
    {
        var config = new StepConfiguration("{}");
        config.GetLongOrDefault("missing", 999L).ShouldBe(999L);
    }

    [Fact]
    public void Has_PresentKey_ReturnsTrue()
    {
        var config = new StepConfiguration("""{"name": "test"}""");
        config.Has("name").ShouldBeTrue();
    }

    [Fact]
    public void Has_MissingKey_ReturnsFalse()
    {
        var config = new StepConfiguration("{}");
        config.Has("missing").ShouldBeFalse();
    }

    [Fact]
    public void Raw_ReturnsOriginalJsonText()
    {
        var config = new StepConfiguration("""{"key":"value"}""");
        config.Raw.ShouldBe("""{"key":"value"}""");
    }

    // --- camelCase fallback regression tests (Bug: frontend sends camelCase, backend reads snake_case) ---

    [Fact]
    public void GetString_WithCamelCaseKey_FallsBackSuccessfully()
    {
        var config = new StepConfiguration("""{"sourcePath": "f.txt"}""");
        config.GetString("source_path").ShouldBe("f.txt");
    }

    [Fact]
    public void GetBool_WithCamelCaseKey_FallsBackSuccessfully()
    {
        var config = new StepConfiguration("""{"overwriteExisting": true}""");
        config.GetBool("overwrite_existing").ShouldBeTrue();
    }

    [Fact]
    public void GetInt_WithCamelCaseKey_FallsBackSuccessfully()
    {
        var config = new StepConfiguration("""{"retryCount": 3}""");
        config.GetInt("retry_count").ShouldBe(3);
    }

    [Fact]
    public void GetStringOrDefault_WithCamelCaseKey_ReturnsValueNotDefault()
    {
        var config = new StepConfiguration("""{"destinationPath": "/out"}""");
        config.GetStringOrDefault("destination_path", "/def").ShouldBe("/out");
    }

    [Fact]
    public void Has_WithCamelCaseKey_ReturnsTrue()
    {
        var config = new StepConfiguration("""{"sourcePath": "f.txt"}""");
        config.Has("source_path").ShouldBeTrue();
    }

    [Fact]
    public void GetStringArray_WithCamelCaseKey_FallsBackSuccessfully()
    {
        var config = new StepConfiguration("""{"filePatterns": ["*.csv"]}""");
        config.GetStringArray("file_patterns").ShouldBe(new[] { "*.csv" });
    }

    [Fact]
    public void GetString_PreferExactSnakeCaseKey_WhenBothFormatsExist()
    {
        var config = new StepConfiguration("""{"source_path": "snake.txt", "sourcePath": "camel.txt"}""");
        config.GetString("source_path").ShouldBe("snake.txt");
    }
}

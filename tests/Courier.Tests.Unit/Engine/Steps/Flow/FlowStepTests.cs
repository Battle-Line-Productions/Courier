using Courier.Domain.Engine;
using Courier.Features.Engine.Steps.Flow;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Steps.Flow;

public class FlowStepTests
{
    // ── FlowForEachStep ────────────────────────────────────────────────

    [Fact]
    public async Task ForEach_ExecuteAsync_ReturnsOk()
    {
        var step = new FlowForEachStep();
        var config = new StepConfiguration("""{ "source": "context:0.file_list" }""");
        var context = new JobContext();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ForEach_ValidateAsync_MissingSource_ReturnsFail()
    {
        var step = new FlowForEachStep();
        var config = new StepConfiguration("{}");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("source");
    }

    [Fact]
    public async Task ForEach_ValidateAsync_WithSource_ReturnsOk()
    {
        var step = new FlowForEachStep();
        var config = new StepConfiguration("""{ "source": "context:0.files" }""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public void ForEach_TypeKey_IsCorrect()
    {
        new FlowForEachStep().TypeKey.ShouldBe("flow.foreach");
    }

    // ── FlowIfStep ─────────────────────────────────────────────────────

    [Fact]
    public async Task If_ExecuteAsync_ReturnsOk()
    {
        var step = new FlowIfStep();
        var config = new StepConfiguration("""{ "left": "a", "operator": "equals", "right": "b" }""");
        var context = new JobContext();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task If_ValidateAsync_MissingLeft_ReturnsFail()
    {
        var step = new FlowIfStep();
        var config = new StepConfiguration("""{ "operator": "equals", "right": "b" }""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("left");
    }

    [Fact]
    public async Task If_ValidateAsync_MissingOperator_ReturnsFail()
    {
        var step = new FlowIfStep();
        var config = new StepConfiguration("""{ "left": "a", "right": "b" }""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("operator");
    }

    [Fact]
    public async Task If_ValidateAsync_MissingRightForNonExists_ReturnsFail()
    {
        var step = new FlowIfStep();
        var config = new StepConfiguration("""{ "left": "a", "operator": "equals" }""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("right");
    }

    [Fact]
    public async Task If_ValidateAsync_ExistsOperator_RightNotRequired()
    {
        var step = new FlowIfStep();
        var config = new StepConfiguration("""{ "left": "a", "operator": "exists" }""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public void If_TypeKey_IsCorrect()
    {
        new FlowIfStep().TypeKey.ShouldBe("flow.if");
    }

    // ── FlowElseStep ───────────────────────────────────────────────────

    [Fact]
    public async Task Else_ExecuteAsync_ReturnsOk()
    {
        var step = new FlowElseStep();
        var config = new StepConfiguration("{}");
        var context = new JobContext();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Else_ValidateAsync_AlwaysReturnsOk()
    {
        var step = new FlowElseStep();
        var result = await step.ValidateAsync(new StepConfiguration("{}"));
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public void Else_TypeKey_IsCorrect()
    {
        new FlowElseStep().TypeKey.ShouldBe("flow.else");
    }

    // ── FlowEndStep ────────────────────────────────────────────────────

    [Fact]
    public async Task End_ExecuteAsync_ReturnsOk()
    {
        var step = new FlowEndStep();
        var config = new StepConfiguration("{}");
        var context = new JobContext();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task End_ValidateAsync_AlwaysReturnsOk()
    {
        var step = new FlowEndStep();
        var result = await step.ValidateAsync(new StepConfiguration("{}"));
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public void End_TypeKey_IsCorrect()
    {
        new FlowEndStep().TypeKey.ShouldBe("flow.end");
    }
}

using Courier.Domain.Engine;
using Courier.Domain.Entities;
using Courier.Features.Engine;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class ExecutionPlanParserEdgeTests
{
    private static JobStep Step(int order, string typeKey) => new()
    {
        Id = Guid.NewGuid(),
        JobId = Guid.NewGuid(),
        StepOrder = order,
        Name = $"step_{order}",
        TypeKey = typeKey,
    };

    // ── Single step ───────────────────────────────────────────────────

    [Fact]
    public void Parse_SingleStepList_ReturnsSingleNode()
    {
        var steps = new List<JobStep> { Step(1, "file.copy") };

        var nodes = ExecutionPlanParser.Parse(steps);

        nodes.Count.ShouldBe(1);
        nodes[0].ShouldBeOfType<StepNode>().StepIndex.ShouldBe(0);
    }

    // ── Orphaned flow.end ─────────────────────────────────────────────

    [Fact]
    public void Parse_FlowEndWithoutBlock_ThrowsInvalidOperationException()
    {
        var steps = new List<JobStep> { Step(1, "flow.end") };

        Should.Throw<InvalidOperationException>(
            () => ExecutionPlanParser.Parse(steps));
    }

    // ── Multiple else blocks ──────────────────────────────────────────

    [Fact]
    public void Parse_MultipleElseBlocks_ThrowsInvalidOperationException()
    {
        // flow.if → step → flow.else → step → flow.else → step → flow.end
        // The parser consumes the first else normally, but encounters the second
        // flow.else inside the else-branch's ParseBlock(If), which returns the
        // else branch. Then the If case sees index is past the second else.
        // Actually: second flow.else is encountered inside ParseBlock(If) for
        // the else branch, which returns nodes (since blockType=If allows else).
        // Then the If case does NOT check for another else. Back at top level,
        // the second flow.else is already consumed. Let's trace more carefully:
        //
        // Index 0: flow.if → enters If case, index=1
        //   ParseBlock(If) for then: index=1 step → index=2 flow.else (blockType=If) → returns [step]
        // Back in If: index=2 is flow.else → index=3, ParseBlock(If) for else:
        //   index=3 step → index=4 flow.else (blockType=If) → returns [step]
        // Back in If: creates IfElseNode. index=4 is flow.else.
        // Back in Parse top-level ParseBlock(null): index=4 flow.else, blockType=null → throws
        var steps = new List<JobStep>
        {
            Step(0, "flow.if"),
            Step(1, "file.copy"),
            Step(2, "flow.else"),
            Step(3, "file.move"),
            Step(4, "flow.else"),
            Step(5, "file.delete"),
            Step(6, "flow.end"),
        };

        var ex = Should.Throw<InvalidOperationException>(
            () => ExecutionPlanParser.Parse(steps));

        ex.Message.ShouldContain("flow.else");
    }

    // ── Nested: ForEach inside If then-branch ─────────────────────────

    [Fact]
    public void Parse_ForEachInsideIfThenBranch_CreatesCorrectTree()
    {
        // flow.if → flow.foreach → step → flow.end → flow.end
        var steps = new List<JobStep>
        {
            Step(0, "flow.if"),
            Step(1, "flow.foreach"),
            Step(2, "file.copy"),
            Step(3, "flow.end"),   // ends foreach
            Step(4, "flow.end"),   // ends if
        };

        var nodes = ExecutionPlanParser.Parse(steps);

        nodes.Count.ShouldBe(1);
        var ifNode = nodes[0].ShouldBeOfType<IfElseNode>();
        ifNode.StepIndex.ShouldBe(0);
        ifNode.ElseBranch.ShouldBeNull();
        ifNode.ThenBranch.Count.ShouldBe(1);

        var foreachNode = ifNode.ThenBranch[0].ShouldBeOfType<ForEachNode>();
        foreachNode.StepIndex.ShouldBe(1);
        foreachNode.Body.Count.ShouldBe(1);
        foreachNode.Body[0].ShouldBeOfType<StepNode>().StepIndex.ShouldBe(2);
    }

    // ── Three-level nesting ───────────────────────────────────────────

    [Fact]
    public void Parse_IfInsideForEachInsideIf_ThreeLevelNesting()
    {
        // flow.if → flow.foreach → flow.if → step → flow.end → flow.end → flow.end
        var steps = new List<JobStep>
        {
            Step(0, "flow.if"),        // outer if
            Step(1, "flow.foreach"),   // foreach inside if-then
            Step(2, "flow.if"),        // inner if inside foreach
            Step(3, "file.copy"),      // step inside inner if
            Step(4, "flow.end"),       // ends inner if
            Step(5, "flow.end"),       // ends foreach
            Step(6, "flow.end"),       // ends outer if
        };

        var nodes = ExecutionPlanParser.Parse(steps);

        nodes.Count.ShouldBe(1);
        var outerIf = nodes[0].ShouldBeOfType<IfElseNode>();
        outerIf.ThenBranch.Count.ShouldBe(1);

        var forEach = outerIf.ThenBranch[0].ShouldBeOfType<ForEachNode>();
        forEach.Body.Count.ShouldBe(1);

        var innerIf = forEach.Body[0].ShouldBeOfType<IfElseNode>();
        innerIf.ThenBranch.Count.ShouldBe(1);
        innerIf.ThenBranch[0].ShouldBeOfType<StepNode>().StepIndex.ShouldBe(3);
        innerIf.ElseBranch.ShouldBeNull();
    }

    // ── Orphaned flow.else at top level ───────────────────────────────

    [Fact]
    public void Parse_OrphanedElseAtTopLevel_ThrowsInvalidOperationException()
    {
        var steps = new List<JobStep>
        {
            Step(0, "file.copy"),
            Step(1, "flow.else"),
        };

        var ex = Should.Throw<InvalidOperationException>(
            () => ExecutionPlanParser.Parse(steps));

        ex.Message.ShouldContain("flow.else");
    }

    // ── Else inside foreach (not if) ──────────────────────────────────

    [Fact]
    public void Parse_ElseInsideForEach_ThrowsInvalidOperationException()
    {
        // flow.foreach → step → flow.else → step → flow.end
        var steps = new List<JobStep>
        {
            Step(0, "flow.foreach"),
            Step(1, "file.copy"),
            Step(2, "flow.else"),
            Step(3, "file.move"),
            Step(4, "flow.end"),
        };

        var ex = Should.Throw<InvalidOperationException>(
            () => ExecutionPlanParser.Parse(steps));

        ex.Message.ShouldContain("flow.else must appear inside a flow.if block");
    }

    // ── Unterminated block ────────────────────────────────────────────

    [Fact]
    public void Parse_UnterminatedForEach_ThrowsInvalidOperationException()
    {
        var steps = new List<JobStep>
        {
            Step(0, "flow.foreach"),
            Step(1, "file.copy"),
        };

        var ex = Should.Throw<InvalidOperationException>(
            () => ExecutionPlanParser.Parse(steps));

        ex.Message.ShouldContain("Unterminated");
    }
}

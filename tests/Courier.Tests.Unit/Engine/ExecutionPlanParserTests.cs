using Courier.Domain.Engine;
using Courier.Domain.Entities;
using Courier.Features.Engine;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class ExecutionPlanParserTests
{
    private static JobStep Step(int order, string typeKey) => new()
    {
        Id = Guid.NewGuid(),
        JobId = Guid.NewGuid(),
        StepOrder = order,
        Name = $"step_{order}",
        TypeKey = typeKey,
    };

    // ── Flat sequence (no flow steps) ──────────────────────────────────

    [Fact]
    public void Parse_FlatSequence_ReturnsAllStepNodes()
    {
        // Arrange
        var steps = new List<JobStep>
        {
            Step(0, "sftp.list"),
            Step(1, "file.copy"),
            Step(2, "file.delete"),
        };

        // Act
        var result = ExecutionPlanParser.Parse(steps);

        // Assert
        result.Count.ShouldBe(3);
        result.ShouldAllBe(n => n is StepNode);
        ((StepNode)result[0]).StepIndex.ShouldBe(0);
        ((StepNode)result[1]).StepIndex.ShouldBe(1);
        ((StepNode)result[2]).StepIndex.ShouldBe(2);
    }

    [Fact]
    public void Parse_EmptyStepList_ReturnsEmptyNodes()
    {
        var result = ExecutionPlanParser.Parse([]);
        result.ShouldBeEmpty();
    }

    // ── ForEach block ──────────────────────────────────────────────────

    [Fact]
    public void Parse_ForEachBlock_CreatesForEachNode()
    {
        // Arrange
        var steps = new List<JobStep>
        {
            Step(0, "sftp.list"),
            Step(1, "flow.foreach"),
            Step(2, "sftp.download"),
            Step(3, "flow.end"),
            Step(4, "file.delete"),
        };

        // Act
        var result = ExecutionPlanParser.Parse(steps);

        // Assert
        result.Count.ShouldBe(3); // StepNode, ForEachNode, StepNode
        result[0].ShouldBeOfType<StepNode>().StepIndex.ShouldBe(0);

        var foreachNode = result[1].ShouldBeOfType<ForEachNode>();
        foreachNode.StepIndex.ShouldBe(1);
        foreachNode.Body.Count.ShouldBe(1);
        foreachNode.Body[0].ShouldBeOfType<StepNode>().StepIndex.ShouldBe(2);

        result[2].ShouldBeOfType<StepNode>().StepIndex.ShouldBe(4);
    }

    [Fact]
    public void Parse_ForEachWithMultipleBodySteps_AllInBody()
    {
        var steps = new List<JobStep>
        {
            Step(0, "flow.foreach"),
            Step(1, "sftp.download"),
            Step(2, "pgp.encrypt"),
            Step(3, "sftp.upload"),
            Step(4, "flow.end"),
        };

        var result = ExecutionPlanParser.Parse(steps);

        result.Count.ShouldBe(1);
        var foreachNode = result[0].ShouldBeOfType<ForEachNode>();
        foreachNode.Body.Count.ShouldBe(3);
    }

    // ── If/Else block ──────────────────────────────────────────────────

    [Fact]
    public void Parse_IfWithoutElse_CreatesThenBranchOnly()
    {
        var steps = new List<JobStep>
        {
            Step(0, "flow.if"),
            Step(1, "pgp.encrypt"),
            Step(2, "flow.end"),
        };

        var result = ExecutionPlanParser.Parse(steps);

        result.Count.ShouldBe(1);
        var ifNode = result[0].ShouldBeOfType<IfElseNode>();
        ifNode.StepIndex.ShouldBe(0);
        ifNode.ThenBranch.Count.ShouldBe(1);
        ifNode.ThenBranch[0].ShouldBeOfType<StepNode>().StepIndex.ShouldBe(1);
        ifNode.ElseBranch.ShouldBeNull();
    }

    [Fact]
    public void Parse_IfWithElse_CreatesBothBranches()
    {
        var steps = new List<JobStep>
        {
            Step(0, "flow.if"),
            Step(1, "pgp.encrypt"),
            Step(2, "flow.else"),
            Step(3, "file.copy"),
            Step(4, "flow.end"),
        };

        var result = ExecutionPlanParser.Parse(steps);

        result.Count.ShouldBe(1);
        var ifNode = result[0].ShouldBeOfType<IfElseNode>();
        ifNode.ThenBranch.Count.ShouldBe(1);
        ifNode.ThenBranch[0].ShouldBeOfType<StepNode>().StepIndex.ShouldBe(1);
        ifNode.ElseBranch.ShouldNotBeNull();
        ifNode.ElseBranch!.Count.ShouldBe(1);
        ifNode.ElseBranch[0].ShouldBeOfType<StepNode>().StepIndex.ShouldBe(3);
    }

    // ── Nested blocks ──────────────────────────────────────────────────

    [Fact]
    public void Parse_IfInsideForeach_CreatesNestedTree()
    {
        var steps = new List<JobStep>
        {
            Step(0, "sftp.list"),
            Step(1, "flow.foreach"),
            Step(2, "sftp.download"),
            Step(3, "flow.if"),
            Step(4, "pgp.encrypt"),
            Step(5, "flow.else"),
            Step(6, "file.copy"),
            Step(7, "flow.end"),   // closes if
            Step(8, "flow.end"),   // closes foreach
            Step(9, "file.delete"),
        };

        var result = ExecutionPlanParser.Parse(steps);

        result.Count.ShouldBe(3);
        result[0].ShouldBeOfType<StepNode>();

        var foreachNode = result[1].ShouldBeOfType<ForEachNode>();
        foreachNode.Body.Count.ShouldBe(2); // sftp.download + if/else

        var ifNode = foreachNode.Body[1].ShouldBeOfType<IfElseNode>();
        ifNode.ThenBranch.Count.ShouldBe(1);
        ifNode.ElseBranch.ShouldNotBeNull();
        ifNode.ElseBranch!.Count.ShouldBe(1);

        result[2].ShouldBeOfType<StepNode>().StepIndex.ShouldBe(9);
    }

    [Fact]
    public void Parse_NestedForeach_CreatesNestedForEachNodes()
    {
        var steps = new List<JobStep>
        {
            Step(0, "flow.foreach"),
            Step(1, "flow.foreach"),
            Step(2, "file.copy"),
            Step(3, "flow.end"),   // closes inner foreach
            Step(4, "flow.end"),   // closes outer foreach
        };

        var result = ExecutionPlanParser.Parse(steps);

        result.Count.ShouldBe(1);
        var outer = result[0].ShouldBeOfType<ForEachNode>();
        outer.Body.Count.ShouldBe(1);
        var inner = outer.Body[0].ShouldBeOfType<ForEachNode>();
        inner.Body.Count.ShouldBe(1);
        inner.Body[0].ShouldBeOfType<StepNode>().StepIndex.ShouldBe(2);
    }

    // ── Error cases ────────────────────────────────────────────────────

    [Fact]
    public void Parse_UnmatchedForeach_ThrowsInvalidOperationException()
    {
        var steps = new List<JobStep>
        {
            Step(0, "flow.foreach"),
            Step(1, "file.copy"),
            // missing flow.end
        };

        var ex = Should.Throw<InvalidOperationException>(() => ExecutionPlanParser.Parse(steps));
        ex.Message.ShouldContain("Unterminated");
    }

    [Fact]
    public void Parse_UnmatchedIf_ThrowsInvalidOperationException()
    {
        var steps = new List<JobStep>
        {
            Step(0, "flow.if"),
            Step(1, "pgp.encrypt"),
            // missing flow.end
        };

        var ex = Should.Throw<InvalidOperationException>(() => ExecutionPlanParser.Parse(steps));
        ex.Message.ShouldContain("Unterminated");
    }

    [Fact]
    public void Parse_OrphanedElse_ThrowsInvalidOperationException()
    {
        var steps = new List<JobStep>
        {
            Step(0, "file.copy"),
            Step(1, "flow.else"),
            Step(2, "file.delete"),
        };

        var ex = Should.Throw<InvalidOperationException>(() => ExecutionPlanParser.Parse(steps));
        ex.Message.ShouldContain("Unexpected");
    }

    [Fact]
    public void Parse_OrphanedEnd_ThrowsInvalidOperationException()
    {
        var steps = new List<JobStep>
        {
            Step(0, "file.copy"),
            Step(1, "flow.end"),
        };

        var ex = Should.Throw<InvalidOperationException>(() => ExecutionPlanParser.Parse(steps));
        ex.Message.ShouldContain("Unexpected");
    }

    [Fact]
    public void Parse_ElseInsideForeach_ThrowsInvalidOperationException()
    {
        var steps = new List<JobStep>
        {
            Step(0, "flow.foreach"),
            Step(1, "flow.else"),
            Step(2, "flow.end"),
        };

        var ex = Should.Throw<InvalidOperationException>(() => ExecutionPlanParser.Parse(steps));
        ex.Message.ShouldContain("flow.else");
        ex.Message.ShouldContain("flow.if");
    }

    // ── Case insensitivity ─────────────────────────────────────────────

    [Fact]
    public void Parse_CaseInsensitiveTypeKeys_ParsesCorrectly()
    {
        var steps = new List<JobStep>
        {
            Step(0, "Flow.ForEach"),
            Step(1, "file.copy"),
            Step(2, "Flow.End"),
        };

        var result = ExecutionPlanParser.Parse(steps);

        result.Count.ShouldBe(1);
        result[0].ShouldBeOfType<ForEachNode>();
    }
}

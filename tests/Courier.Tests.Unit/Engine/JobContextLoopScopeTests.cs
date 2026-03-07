using Courier.Domain.Engine;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class JobContextLoopScopeTests
{
    [Fact]
    public void PushLoopScope_SetsCurrentItemInContext()
    {
        var ctx = new JobContext();
        ctx.PushLoopScope("file.txt", 0, 3);

        ctx.Get<object>("loop.current_item").ShouldBe("file.txt");
        ctx.PopLoopScope();
    }

    [Fact]
    public void PushLoopScope_SetsLoopIndexInContext()
    {
        var ctx = new JobContext();
        ctx.PushLoopScope("file.txt", 2, 5);

        ctx.Get<object>("loop.index").ShouldBe(2);
        ctx.PopLoopScope();
    }

    [Fact]
    public void PushLoopScope_LoopDepth_IncrementsOnEachPush()
    {
        var ctx = new JobContext();
        ctx.LoopDepth.ShouldBe(0);

        ctx.PushLoopScope("a", 0, 2);
        ctx.LoopDepth.ShouldBe(1);

        ctx.PushLoopScope("b", 0, 3);
        ctx.LoopDepth.ShouldBe(2);

        ctx.PopLoopScope();
        ctx.PopLoopScope();
    }

    [Fact]
    public void PopLoopScope_EmptyStack_ThrowsInvalidOperationException()
    {
        var ctx = new JobContext();
        Should.Throw<InvalidOperationException>(() => ctx.PopLoopScope());
    }

    [Fact]
    public void PopLoopScope_RemovesMagicKeys_WhenLastScopePopped()
    {
        var ctx = new JobContext();
        ctx.PushLoopScope("item", 0, 1);
        ctx.PopLoopScope();

        ctx.TryGet<object>("loop.current_item", out _).ShouldBeFalse();
        ctx.TryGet<object>("loop.index", out _).ShouldBeFalse();
    }

    [Fact]
    public void PopLoopScope_LoopDepth_DecrementsOnPop()
    {
        var ctx = new JobContext();
        ctx.PushLoopScope("a", 0, 2);
        ctx.PushLoopScope("b", 0, 3);
        ctx.LoopDepth.ShouldBe(2);

        ctx.PopLoopScope();
        ctx.LoopDepth.ShouldBe(1);

        ctx.PopLoopScope();
        ctx.LoopDepth.ShouldBe(0);
    }

    [Fact]
    public void NestedPush_SavesOuterKeysWithDepthPrefix()
    {
        var ctx = new JobContext();
        ctx.PushLoopScope("outer", 1, 5);
        ctx.PushLoopScope("inner", 0, 3);

        // Outer keys saved with depth prefix
        ctx.TryGet<object>("loop.0.current_item", out var outerItem).ShouldBeTrue();
        outerItem.ShouldBe("outer");

        ctx.TryGet<object>("loop.0.index", out var outerIndex).ShouldBeTrue();
        outerIndex.ShouldBe(1);

        // Current keys reflect inner scope
        ctx.Get<object>("loop.current_item").ShouldBe("inner");
        ctx.Get<object>("loop.index").ShouldBe(0);

        ctx.PopLoopScope();
        ctx.PopLoopScope();
    }

    [Fact]
    public void NestedPop_RestoresOuterMagicKeys()
    {
        var ctx = new JobContext();
        ctx.PushLoopScope("outer", 1, 5);
        ctx.PushLoopScope("inner", 0, 3);

        ctx.PopLoopScope();

        ctx.Get<object>("loop.current_item").ShouldBe("outer");
        ctx.Get<object>("loop.index").ShouldBe(1);

        ctx.PopLoopScope();
    }

    [Fact]
    public void NestedPop_RemovesDepthPrefixedKeys()
    {
        var ctx = new JobContext();
        ctx.PushLoopScope("outer", 1, 5);
        ctx.PushLoopScope("inner", 0, 3);

        ctx.PopLoopScope();

        ctx.TryGet<object>("loop.0.current_item", out _).ShouldBeFalse();
        ctx.TryGet<object>("loop.0.index", out _).ShouldBeFalse();

        ctx.PopLoopScope();
    }

    [Fact]
    public void CurrentIterationIndex_ReturnsIndexFromTopOfStack()
    {
        var ctx = new JobContext();
        ctx.CurrentIterationIndex.ShouldBeNull();

        ctx.PushLoopScope("a", 3, 10);
        ctx.CurrentIterationIndex.ShouldBe(3);

        ctx.PushLoopScope("b", 7, 20);
        ctx.CurrentIterationIndex.ShouldBe(7);

        ctx.PopLoopScope();
        ctx.CurrentIterationIndex.ShouldBe(3);

        ctx.PopLoopScope();
        ctx.CurrentIterationIndex.ShouldBeNull();
    }
}

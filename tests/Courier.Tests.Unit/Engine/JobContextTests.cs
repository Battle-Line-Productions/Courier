using Courier.Domain.Engine;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class JobContextTests
{
    [Fact]
    public void Set_And_Get_ReturnsValue()
    {
        var ctx = new JobContext();
        ctx.Set("0.downloaded_file", "/tmp/file.txt");
        ctx.Get<string>("0.downloaded_file").ShouldBe("/tmp/file.txt");
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var ctx = new JobContext();
        ctx.TryGet<string>("missing", out var value).ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void Snapshot_ReturnsAllEntries()
    {
        var ctx = new JobContext();
        ctx.Set("a", 1);
        ctx.Set("b", "two");
        var snap = ctx.Snapshot();
        snap.Count.ShouldBe(2);
        snap["a"].ShouldBe(1);
        snap["b"].ShouldBe("two");
    }

    [Fact]
    public void Snapshot_IsReadOnly_OriginalUnaffected()
    {
        var ctx = new JobContext();
        ctx.Set("a", 1);
        var snap = ctx.Snapshot();
        ctx.Set("b", 2);
        snap.Count.ShouldBe(1);
    }

    [Fact]
    public void Restore_PopulatesFromDictionary()
    {
        var data = new Dictionary<string, object> { ["x"] = "hello" };
        var ctx = JobContext.Restore(data);
        ctx.Get<string>("x").ShouldBe("hello");
    }
}

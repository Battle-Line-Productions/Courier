using Courier.Domain.Engine;
using Courier.Features.Engine;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class StepTypeRegistryTests
{
    [Fact]
    public void Resolve_RegisteredType_ReturnsStep()
    {
        var step = Substitute.For<IJobStep>();
        step.TypeKey.Returns("test.step");

        var registry = new StepTypeRegistry([step]);
        var resolved = registry.Resolve("test.step");

        resolved.ShouldBe(step);
    }

    [Fact]
    public void Resolve_UnknownType_ThrowsKeyNotFoundException()
    {
        var registry = new StepTypeRegistry([]);
        Should.Throw<KeyNotFoundException>(() => registry.Resolve("unknown.type"));
    }

    [Fact]
    public void GetRegisteredTypes_ReturnsAllKeys()
    {
        var step1 = Substitute.For<IJobStep>();
        step1.TypeKey.Returns("a");
        var step2 = Substitute.For<IJobStep>();
        step2.TypeKey.Returns("b");

        var registry = new StepTypeRegistry([step1, step2]);
        var keys = registry.GetRegisteredTypes();

        keys.ShouldContain("a");
        keys.ShouldContain("b");
        keys.Count().ShouldBe(2);
    }

    [Fact]
    public void Resolve_CaseInsensitiveKey_ReturnsHandler()
    {
        var step = Substitute.For<IJobStep>();
        step.TypeKey.Returns("file.copy");

        var registry = new StepTypeRegistry([step]);
        var resolved = registry.Resolve("FILE.COPY");

        resolved.ShouldBe(step);
    }

    [Fact]
    public void Constructor_DuplicateTypeKeys_ThrowsArgumentException()
    {
        var step1 = Substitute.For<IJobStep>();
        step1.TypeKey.Returns("file.copy");
        var step2 = Substitute.For<IJobStep>();
        step2.TypeKey.Returns("file.copy");

        Should.Throw<ArgumentException>(() => new StepTypeRegistry([step1, step2]));
    }
}

using Courier.Domain.Engine;
using Courier.Features.Engine;
using Courier.Features.Engine.Steps;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class StepTypeRegistryApiTests
{
    private static StepTypeRegistry CreateRegistry(params IJobStep[] steps)
    {
        return new StepTypeRegistry(steps);
    }

    private static IJobStep CreateMockStep(string typeKey)
    {
        var step = Substitute.For<IJobStep>();
        step.TypeKey.Returns(typeKey);
        return step;
    }

    [Fact]
    public void GetAllMetadata_ReturnsAllRegisteredMetadataEntries()
    {
        // Arrange
        var registry = CreateRegistry(CreateMockStep("file.copy"));

        // Act
        var metadata = registry.GetAllMetadata().ToList();

        // Assert
        metadata.Count.ShouldBeGreaterThan(0);
        metadata.ShouldContain(m => m.TypeKey == "file.copy");
        metadata.ShouldContain(m => m.TypeKey == "sftp.upload");
        metadata.ShouldContain(m => m.TypeKey == "pgp.encrypt");
    }

    [Fact]
    public void GetAllMetadata_EachEntryHasRequiredFields()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var metadata = registry.GetAllMetadata().ToList();

        // Assert
        foreach (var entry in metadata)
        {
            entry.TypeKey.ShouldNotBeNullOrWhiteSpace();
            entry.DisplayName.ShouldNotBeNullOrWhiteSpace();
            entry.Category.ShouldNotBeNullOrWhiteSpace();
            entry.Description.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData("file.copy", "Copy File", "file")]
    [InlineData("sftp.upload", "SFTP Upload", "transfer.sftp")]
    [InlineData("pgp.encrypt", "PGP Encrypt", "crypto")]
    [InlineData("flow.if", "If Condition", "flow")]
    [InlineData("azure_function.execute", "Azure Function", "cloud")]
    public void GetMetadata_ByKey_ReturnsCorrectMetadata(string typeKey, string expectedDisplayName, string expectedCategory)
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var metadata = registry.GetMetadata(typeKey);

        // Assert
        metadata.ShouldNotBeNull();
        metadata.TypeKey.ShouldBe(typeKey);
        metadata.DisplayName.ShouldBe(expectedDisplayName);
        metadata.Category.ShouldBe(expectedCategory);
    }

    [Fact]
    public void GetMetadata_NonExistentKey_ReturnsNull()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var metadata = registry.GetMetadata("nonexistent.step");

        // Assert
        metadata.ShouldBeNull();
    }

    [Fact]
    public void GetMetadata_CaseInsensitive_ReturnsMatch()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var metadata = registry.GetMetadata("FILE.COPY");

        // Assert
        metadata.ShouldNotBeNull();
        metadata.TypeKey.ShouldBe("file.copy");
    }

    [Fact]
    public void GetAllMetadata_ContainsExpectedCategories()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var categories = registry.GetAllMetadata()
            .Select(m => m.Category)
            .Distinct()
            .ToList();

        // Assert
        categories.ShouldContain("file");
        categories.ShouldContain("transfer.sftp");
        categories.ShouldContain("transfer.ftp");
        categories.ShouldContain("transfer.ftps");
        categories.ShouldContain("crypto");
        categories.ShouldContain("flow");
        categories.ShouldContain("cloud");
    }

    [Fact]
    public void GetAllMetadata_NoDuplicateTypeKeys()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var metadata = registry.GetAllMetadata().ToList();
        var uniqueKeys = metadata.Select(m => m.TypeKey).Distinct().ToList();

        // Assert
        uniqueKeys.Count.ShouldBe(metadata.Count);
    }

    [Fact]
    public void Resolve_RegisteredStep_ReturnsStep()
    {
        // Arrange
        var mockStep = CreateMockStep("file.copy");
        var registry = CreateRegistry(mockStep);

        // Act
        var resolved = registry.Resolve("file.copy");

        // Assert
        resolved.ShouldBe(mockStep);
    }

    [Fact]
    public void Resolve_UnregisteredStep_ThrowsKeyNotFoundException()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        Should.Throw<KeyNotFoundException>(() => registry.Resolve("nonexistent.step"));
    }
}

using Courier.Domain.Engine;
using Courier.Features.Engine.Steps;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Steps;

public class IdempotencyTests : IDisposable
{
    private readonly string _tempDir;

    public IdempotencyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"courier_idem_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateTempFile(string name, string content = "test content")
    {
        var path = Path.Combine(_tempDir, name);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
        return path;
    }

    private static StepConfiguration MakeConfig(string sourcePath, string destPath, string? idempotency = null)
    {
        var escapedSource = sourcePath.Replace("\\", "\\\\");
        var escapedDest = destPath.Replace("\\", "\\\\");
        var idem = idempotency is not null ? $""", "idempotency": "{idempotency}" """ : "";
        var json = $$"""{"source_path": "{{escapedSource}}", "destination_path": "{{escapedDest}}"{{idem}}}""";
        return new StepConfiguration(json);
    }

    [Fact]
    public async Task FileCopy_SkipIfExists_SkipsWhenDestinationExists()
    {
        // Arrange
        var source = CreateTempFile("source.txt", "original");
        var dest = CreateTempFile("dest.txt", "existing content");
        var step = new FileCopyStep();
        var config = MakeConfig(source, dest, "skip_if_exists");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Outputs!["skipped"].ShouldBe("true");
        result.Outputs["reason"].ShouldBe("file_exists");
        result.BytesProcessed.ShouldBe(0);
        File.ReadAllText(dest).ShouldBe("existing content"); // unchanged
    }

    [Fact]
    public async Task FileCopy_SkipIfExists_ProceedsWhenDestinationMissing()
    {
        // Arrange
        var source = CreateTempFile("source.txt", "data");
        var dest = Path.Combine(_tempDir, "output", "dest.txt");
        var step = new FileCopyStep();
        var config = MakeConfig(source, dest, "skip_if_exists");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Outputs!.ContainsKey("skipped").ShouldBeFalse();
        File.Exists(dest).ShouldBeTrue();
        File.ReadAllText(dest).ShouldBe("data");
    }

    [Fact]
    public async Task FileCopy_Overwrite_OverwritesExistingFile()
    {
        // Arrange
        var source = CreateTempFile("source.txt", "new content");
        var dest = CreateTempFile("dest.txt", "old content");
        var step = new FileCopyStep();
        var config = MakeConfig(source, dest, "overwrite");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        File.ReadAllText(dest).ShouldBe("new content");
        result.BytesProcessed.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task FileCopy_DefaultIdempotency_OverwritesExistingFile()
    {
        // Arrange
        var source = CreateTempFile("source.txt", "new data");
        var dest = CreateTempFile("dest.txt", "old data");
        var step = new FileCopyStep();
        var config = MakeConfig(source, dest); // no idempotency config
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        File.ReadAllText(dest).ShouldBe("new data");
    }

    [Fact]
    public async Task FileMove_SkipIfExists_SkipsWhenDestinationExists()
    {
        // Arrange
        var source = CreateTempFile("source.txt", "original");
        var dest = CreateTempFile("dest.txt", "existing");
        var step = new FileMoveStep();
        var config = MakeConfig(source, dest, "skip_if_exists");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Outputs!["skipped"].ShouldBe("true");
        result.Outputs["reason"].ShouldBe("file_exists");
        File.Exists(source).ShouldBeTrue(); // source not deleted
        File.ReadAllText(dest).ShouldBe("existing"); // unchanged
    }

    [Fact]
    public async Task FileMove_Overwrite_OverwritesExistingFile()
    {
        // Arrange
        var source = CreateTempFile("source.txt", "new content");
        var dest = CreateTempFile("dest.txt", "old content");
        var step = new FileMoveStep();
        var config = MakeConfig(source, dest, "overwrite");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        File.ReadAllText(dest).ShouldBe("new content");
        File.Exists(source).ShouldBeFalse(); // source was moved
    }

    [Fact]
    public async Task FileMove_SkipIfExists_SourceGoneButDestExists_ReportsAlreadyComplete()
    {
        // Arrange
        var sourcePath = Path.Combine(_tempDir, "gone.txt"); // does not exist
        var dest = CreateTempFile("dest.txt", "completed content");
        var step = new FileMoveStep();
        var config = MakeConfig(sourcePath, dest, "skip_if_exists");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Outputs!["skipped"].ShouldBe("true");
        result.Outputs["reason"].ShouldBe("already_complete");
    }

    [Fact]
    public async Task FileCopy_Resume_SkipsWhenFileSizesMatch()
    {
        // Arrange
        var content = "same content for resume test";
        var source = CreateTempFile("source.txt", content);
        var dest = CreateTempFile("dest.txt", content); // same size
        var step = new FileCopyStep();
        var config = MakeConfig(source, dest, "resume");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Outputs!["skipped"].ShouldBe("true");
        result.Outputs["reason"].ShouldBe("already_complete");
    }

    [Fact]
    public async Task FileCopy_SkipIfExists_OutputContainsCopiedFilePath()
    {
        // Arrange
        var source = CreateTempFile("source.txt", "data");
        var dest = CreateTempFile("dest.txt", "existing");
        var step = new FileCopyStep();
        var config = MakeConfig(source, dest, "skip_if_exists");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Outputs!["copied_file"].ShouldBe(dest);
    }
}

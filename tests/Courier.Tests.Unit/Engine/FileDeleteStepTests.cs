using Courier.Domain.Engine;
using Courier.Features.Engine.Steps.FileOps;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class FileDeleteStepTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileDeleteStep _step;

    public FileDeleteStepTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"courier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _step = new FileDeleteStep();
    }

    [Fact]
    public void TypeKey_IsFileDelete()
    {
        _step.TypeKey.ShouldBe("file.delete");
    }

    [Fact]
    public async Task ValidateAsync_MissingPath_ReturnsFailure()
    {
        // Arrange
        var config = new StepConfiguration("{}");

        // Act
        var result = await _step.ValidateAsync(config);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("path");
    }

    [Fact]
    public async Task ValidateAsync_ValidConfig_ReturnsSuccess()
    {
        // Arrange
        var config = new StepConfiguration("""{"path": "/some/file.txt"}""");

        // Act
        var result = await _step.ValidateAsync(config);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ExistingFile_DeletesSuccessfully()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "delete-me.txt");
        await File.WriteAllTextAsync(filePath, "to be deleted");

        var config = new StepConfiguration($$"""{"path": "{{filePath.Replace("\\", "\\\\")}}"}""");
        var context = new JobContext();

        // Act
        var result = await _step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        File.Exists(filePath).ShouldBeFalse();
        result.Outputs!["existed"].ShouldBe(true);
        result.Outputs!["deleted_file"].ShouldBe(filePath);
        result.BytesProcessed.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentFile_LenientByDefault()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "nonexistent.txt");
        var config = new StepConfiguration($$"""{"path": "{{filePath.Replace("\\", "\\\\")}}"}""");
        var context = new JobContext();

        // Act
        var result = await _step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Outputs!["existed"].ShouldBe(false);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentFile_FailIfNotFound()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "nonexistent.txt");
        var config = new StepConfiguration($$"""{"path": "{{filePath.Replace("\\", "\\\\")}}", "fail_if_not_found": true}""");
        var context = new JobContext();

        // Act
        var result = await _step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("File not found");
    }

    [Fact]
    public async Task ExecuteAsync_ResolvesContextRef()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "ctx-delete.txt");
        await File.WriteAllTextAsync(filePath, "context data");

        var config = new StepConfiguration("""{"path": "context:1.archive_path"}""");
        var context = new JobContext();
        context.Set("1.archive_path", filePath);

        // Act
        var result = await _step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        File.Exists(filePath).ShouldBeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

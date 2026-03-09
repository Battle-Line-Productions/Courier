using Courier.Domain.Engine;
using Courier.Features.Engine.Steps;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class FileMoveStepTests : IDisposable
{
    private readonly string _tempDir;

    public FileMoveStepTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"courier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task Execute_MovesFile_Successfully()
    {
        var sourceFile = Path.Combine(_tempDir, "source.txt");
        var destFile = Path.Combine(_tempDir, "dest.txt");
        await File.WriteAllTextAsync(sourceFile, "move me");

        var config = new StepConfiguration($$"""{"source_path": "{{sourceFile.Replace("\\", "\\\\")}}", "destination_path": "{{destFile.Replace("\\", "\\\\")}}"}""");
        var context = new JobContext();
        var step = new FileMoveStep();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeTrue();
        File.Exists(destFile).ShouldBeTrue();
        File.Exists(sourceFile).ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_SourceNotFound_ReturnsFailure()
    {
        var config = new StepConfiguration("""{"source_path": "/nonexistent/file.txt", "destination_path": "/tmp/out.txt"}""");
        var context = new JobContext();
        var step = new FileMoveStep();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Source file not found");
    }

    [Fact]
    public void TypeKey_IsFileMove()
    {
        new FileMoveStep().TypeKey.ShouldBe("file.move");
    }

    [Fact]
    public async Task Execute_DestinationIsDirectory_MovesWithSourceFilename()
    {
        var sourceFile = Path.Combine(_tempDir, "source.txt");
        var destDir = Path.Combine(_tempDir, "outdir");
        Directory.CreateDirectory(destDir);
        await File.WriteAllTextAsync(sourceFile, "move me");

        var config = new StepConfiguration($$"""{"source_path": "{{sourceFile.Replace("\\", "\\\\")}}", "destination_path": "{{destDir.Replace("\\", "\\\\")}}"}""");
        var context = new JobContext();
        var step = new FileMoveStep();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeTrue();
        var expectedDest = Path.Combine(destDir, "source.txt");
        File.Exists(expectedDest).ShouldBeTrue();
        (await File.ReadAllTextAsync(expectedDest)).ShouldBe("move me");
        File.Exists(sourceFile).ShouldBeFalse(); // source removed
    }

    [Fact]
    public async Task Execute_SourceGone_DestExistsInDirectory_WithIdempotency_Succeeds()
    {
        var sourceFile = Path.Combine(_tempDir, "source.txt");
        var destDir = Path.Combine(_tempDir, "outdir");
        Directory.CreateDirectory(destDir);
        // Source is gone, but dest already has the file (move already completed)
        await File.WriteAllTextAsync(Path.Combine(destDir, "source.txt"), "already moved");

        var config = new StepConfiguration($$"""{"source_path": "{{sourceFile.Replace("\\", "\\\\")}}", "destination_path": "{{destDir.Replace("\\", "\\\\")}}", "idempotency": "skip_if_exists"}""");
        var context = new JobContext();
        var step = new FileMoveStep();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Outputs!["reason"].ShouldBe("already_complete");
    }

    [Fact]
    public async Task Execute_WithCamelCaseConfigKeys_MovesSuccessfully()
    {
        var sourceFile = Path.Combine(_tempDir, "source.txt");
        var destFile = Path.Combine(_tempDir, "dest.txt");
        await File.WriteAllTextAsync(sourceFile, "camel case config");

        // Uses camelCase keys — the bug that caused KeyNotFoundException
        var config = new StepConfiguration($$"""{"sourcePath": "{{sourceFile.Replace("\\", "\\\\")}}", "destinationPath": "{{destFile.Replace("\\", "\\\\")}}"}""");
        var context = new JobContext();
        var step = new FileMoveStep();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeTrue();
        File.Exists(destFile).ShouldBeTrue();
        File.Exists(sourceFile).ShouldBeFalse();
        (await File.ReadAllTextAsync(destFile)).ShouldBe("camel case config");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

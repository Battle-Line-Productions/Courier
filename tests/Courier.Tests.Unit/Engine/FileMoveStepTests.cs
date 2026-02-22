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

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

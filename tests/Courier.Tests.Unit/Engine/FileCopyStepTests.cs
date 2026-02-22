using Courier.Domain.Engine;
using Courier.Features.Engine.Steps;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class FileCopyStepTests : IDisposable
{
    private readonly string _tempDir;

    public FileCopyStepTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"courier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task Execute_CopiesFile_Successfully()
    {
        var sourceFile = Path.Combine(_tempDir, "source.txt");
        var destFile = Path.Combine(_tempDir, "dest.txt");
        await File.WriteAllTextAsync(sourceFile, "hello world");

        var config = new StepConfiguration($$"""{"source_path": "{{sourceFile.Replace("\\", "\\\\")}}", "destination_path": "{{destFile.Replace("\\", "\\\\")}}"}""");
        var context = new JobContext();
        var step = new FileCopyStep();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeTrue();
        File.Exists(destFile).ShouldBeTrue();
        (await File.ReadAllTextAsync(destFile)).ShouldBe("hello world");
        File.Exists(sourceFile).ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_SourceNotFound_ReturnsFailure()
    {
        var config = new StepConfiguration("""{"source_path": "/nonexistent/file.txt", "destination_path": "/tmp/out.txt"}""");
        var context = new JobContext();
        var step = new FileCopyStep();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Source file not found");
    }

    [Fact]
    public void TypeKey_IsFileCopy()
    {
        new FileCopyStep().TypeKey.ShouldBe("file.copy");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

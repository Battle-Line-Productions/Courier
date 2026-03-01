using Courier.Domain.Engine;
using Courier.Features.Engine.Compression;
using Courier.Features.Engine.Steps.FileOps;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class FileZipStepTests
{
    private readonly ICompressionProvider _mockProvider;
    private readonly FileZipStep _step;

    public FileZipStepTests()
    {
        _mockProvider = Substitute.For<ICompressionProvider>();
        _mockProvider.FormatKey.Returns("zip");

        var registry = new CompressionProviderRegistry([_mockProvider]);
        _step = new FileZipStep(registry);
    }

    [Fact]
    public void TypeKey_IsFileZip()
    {
        _step.TypeKey.ShouldBe("file.zip");
    }

    [Fact]
    public async Task ValidateAsync_MissingSourcePath_ReturnsFailure()
    {
        // Arrange
        var config = new StepConfiguration("""{"output_path": "/out.zip"}""");

        // Act
        var result = await _step.ValidateAsync(config);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("source_path");
    }

    [Fact]
    public async Task ValidateAsync_MissingOutputPath_ReturnsFailure()
    {
        // Arrange
        var config = new StepConfiguration("""{"source_path": "/file.txt"}""");

        // Act
        var result = await _step.ValidateAsync(config);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("output_path");
    }

    [Fact]
    public async Task ValidateAsync_ValidConfig_ReturnsSuccess()
    {
        // Arrange
        var config = new StepConfiguration("""{"source_path": "/file.txt", "output_path": "/out.zip"}""");

        // Act
        var result = await _step.ValidateAsync(config);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToProvider()
    {
        // Arrange
        _mockProvider.CompressAsync(Arg.Any<CompressRequest>(), Arg.Any<IProgress<CompressionProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new CompressionResult(true, 1024, "/out.zip", null, null));

        var config = new StepConfiguration("""{"source_path": "/file.txt", "output_path": "/out.zip"}""");
        var context = new JobContext();

        // Act
        var result = await _step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.BytesProcessed.ShouldBe(1024);
        result.Outputs!["archive_path"].ShouldBe("/out.zip");
        await _mockProvider.Received(1).CompressAsync(
            Arg.Is<CompressRequest>(r => r.SourcePaths.Contains("/file.txt") && r.OutputPath == "/out.zip"),
            Arg.Any<IProgress<CompressionProgress>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ResolvesContextRef()
    {
        // Arrange
        _mockProvider.CompressAsync(Arg.Any<CompressRequest>(), Arg.Any<IProgress<CompressionProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new CompressionResult(true, 512, "/archive.zip", null, null));

        var config = new StepConfiguration("""{"source_path": "context:1.copied_file", "output_path": "/archive.zip"}""");
        var context = new JobContext();
        context.Set("1.copied_file", "/data/report.csv");

        // Act
        var result = await _step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        await _mockProvider.Received(1).CompressAsync(
            Arg.Is<CompressRequest>(r => r.SourcePaths.Contains("/data/report.csv")),
            Arg.Any<IProgress<CompressionProgress>?>(),
            Arg.Any<CancellationToken>());
    }
}

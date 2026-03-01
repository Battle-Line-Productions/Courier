using Courier.Domain.Engine;
using Courier.Features.Engine.Compression;
using Courier.Features.Engine.Steps.FileOps;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class FileUnzipStepTests
{
    private readonly ICompressionProvider _mockProvider;
    private readonly FileUnzipStep _step;

    public FileUnzipStepTests()
    {
        _mockProvider = Substitute.For<ICompressionProvider>();
        _mockProvider.FormatKey.Returns("zip");

        var registry = new CompressionProviderRegistry([_mockProvider]);
        _step = new FileUnzipStep(registry);
    }

    [Fact]
    public void TypeKey_IsFileUnzip()
    {
        _step.TypeKey.ShouldBe("file.unzip");
    }

    [Fact]
    public async Task ValidateAsync_MissingArchivePath_ReturnsFailure()
    {
        // Arrange
        var config = new StepConfiguration("""{"output_directory": "/out"}""");

        // Act
        var result = await _step.ValidateAsync(config);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("archive_path");
    }

    [Fact]
    public async Task ValidateAsync_MissingOutputDirectory_ReturnsFailure()
    {
        // Arrange
        var config = new StepConfiguration("""{"archive_path": "/file.zip"}""");

        // Act
        var result = await _step.ValidateAsync(config);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("output_directory");
    }

    [Fact]
    public async Task ValidateAsync_ValidConfig_ReturnsSuccess()
    {
        // Arrange
        var config = new StepConfiguration("""{"archive_path": "/file.zip", "output_directory": "/out"}""");

        // Act
        var result = await _step.ValidateAsync(config);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToProvider()
    {
        // Arrange
        var extractedFiles = new List<string> { "/out/file1.txt", "/out/file2.txt" };
        _mockProvider.DecompressAsync(Arg.Any<DecompressRequest>(), Arg.Any<IProgress<CompressionProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new CompressionResult(true, 2048, "/out", extractedFiles, null));

        var config = new StepConfiguration("""{"archive_path": "/file.zip", "output_directory": "/out"}""");
        var context = new JobContext();

        // Act
        var result = await _step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.BytesProcessed.ShouldBe(2048);
        result.Outputs!["extracted_directory"].ShouldBe("/out");
        result.Outputs!["extracted_files"].ShouldBe(extractedFiles);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderFailure_ReturnsFailure()
    {
        // Arrange
        _mockProvider.DecompressAsync(Arg.Any<DecompressRequest>(), Arg.Any<IProgress<CompressionProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new CompressionResult(false, 0, "/out", null, "Archive not found"));

        var config = new StepConfiguration("""{"archive_path": "/missing.zip", "output_directory": "/out"}""");
        var context = new JobContext();

        // Act
        var result = await _step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Archive not found");
    }
}

using Courier.Features.Engine.Compression;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Compression;

public class GzipCompressionProviderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly GzipCompressionProvider _provider;

    public GzipCompressionProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"courier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _provider = new GzipCompressionProvider();
    }

    [Fact]
    public void FormatKey_IsGzip()
    {
        _provider.FormatKey.ShouldBe("gzip");
    }

    [Fact]
    public async Task CompressAsync_SingleFile_CreatesGzFile()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(sourceFile, "hello world");
        var outputPath = Path.Combine(_tempDir, "test.txt.gz");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([sourceFile], outputPath, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.OutputPath.ShouldBe(outputPath);
        result.BytesProcessed.ShouldBeGreaterThan(0);
        File.Exists(outputPath).ShouldBeTrue();
    }

    [Fact]
    public async Task DecompressAsync_ValidGz_ExtractsOriginalFile()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "data.txt");
        await File.WriteAllTextAsync(sourceFile, "extracted content");
        var gzPath = Path.Combine(_tempDir, "data.txt.gz");
        await _provider.CompressAsync(
            new CompressRequest([sourceFile], gzPath, null), null, CancellationToken.None);

        var extractDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(gzPath, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ExtractedFiles.ShouldNotBeNull();
        result.ExtractedFiles!.Count.ShouldBe(1);
        var extractedContent = await File.ReadAllTextAsync(result.ExtractedFiles[0]);
        extractedContent.ShouldBe("extracted content");
    }

    [Fact]
    public async Task RoundTrip_CompressThenDecompress_PreservesContentExactly()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "roundtrip.txt");
        var originalContent = "The quick brown fox jumps over the lazy dog.\nLine two.\n";
        await File.WriteAllTextAsync(sourceFile, originalContent);
        var gzPath = Path.Combine(_tempDir, "roundtrip.txt.gz");
        var extractDir = Path.Combine(_tempDir, "roundtrip-out");

        // Act
        await _provider.CompressAsync(
            new CompressRequest([sourceFile], gzPath, null), null, CancellationToken.None);
        var result = await _provider.DecompressAsync(
            new DecompressRequest(gzPath, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ExtractedFiles.ShouldNotBeNull();
        var extractedContent = await File.ReadAllTextAsync(result.ExtractedFiles![0]);
        extractedContent.ShouldBe(originalContent);
    }

    [Fact]
    public async Task CompressAsync_NonExistentSource_ReturnsFailure()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "output.gz");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest(["/nonexistent/file.txt"], outputPath, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Source file not found");
    }

    [Fact]
    public async Task CompressAsync_MultipleFiles_ReturnsError()
    {
        // Arrange
        var file1 = Path.Combine(_tempDir, "file1.txt");
        var file2 = Path.Combine(_tempDir, "file2.txt");
        await File.WriteAllTextAsync(file1, "content 1");
        await File.WriteAllTextAsync(file2, "content 2");
        var outputPath = Path.Combine(_tempDir, "multi.gz");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([file1, file2], outputPath, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("GZIP compression supports only a single source file");
    }

    [Fact]
    public async Task CompressAsync_WithPassword_ReturnsError()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "secret.txt");
        await File.WriteAllTextAsync(sourceFile, "secret data");
        var outputPath = Path.Combine(_tempDir, "encrypted.gz");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([sourceFile], outputPath, "mypassword"), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("GZIP does not support password protection");
    }

    [Fact]
    public async Task DecompressAsync_CorruptedFile_ReturnsError()
    {
        // Arrange — write random bytes that are not a valid gzip archive
        var corruptedPath = Path.Combine(_tempDir, "corrupted.gz");
        var randomBytes = new byte[256];
        Random.Shared.NextBytes(randomBytes);
        await File.WriteAllBytesAsync(corruptedPath, randomBytes);

        var extractDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(corruptedPath, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Invalid GZIP archive");
    }

    [Fact]
    public async Task DecompressAsync_ArchiveNotFound_ReturnsFailure()
    {
        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest("/nonexistent.gz", _tempDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Archive not found");
    }

    [Fact]
    public async Task DecompressAsync_OutputDirectoryCreated_WhenMissing()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(sourceFile, "test");
        var gzPath = Path.Combine(_tempDir, "file.txt.gz");
        await _provider.CompressAsync(
            new CompressRequest([sourceFile], gzPath, null), null, CancellationToken.None);

        var deepExtractDir = Path.Combine(_tempDir, "deep", "nested", "dir");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(gzPath, deepExtractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        Directory.Exists(deepExtractDir).ShouldBeTrue();
    }

    [Fact]
    public async Task CompressAsync_EmptyFile_CompressesAndDecompressesCorrectly()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "empty.txt");
        await File.WriteAllTextAsync(sourceFile, string.Empty);
        var gzPath = Path.Combine(_tempDir, "empty.txt.gz");
        var extractDir = Path.Combine(_tempDir, "extracted");

        // Act
        var compressResult = await _provider.CompressAsync(
            new CompressRequest([sourceFile], gzPath, null), null, CancellationToken.None);
        var decompressResult = await _provider.DecompressAsync(
            new DecompressRequest(gzPath, extractDir, null), null, CancellationToken.None);

        // Assert
        compressResult.Success.ShouldBeTrue();
        decompressResult.Success.ShouldBeTrue();
        decompressResult.ExtractedFiles.ShouldNotBeNull();
        var extractedContent = await File.ReadAllTextAsync(decompressResult.ExtractedFiles![0]);
        extractedContent.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task InspectAsync_ReturnsFileSizeInfo()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "inspect.txt");
        await File.WriteAllTextAsync(sourceFile, "some content for inspection");
        var gzPath = Path.Combine(_tempDir, "inspect.txt.gz");
        await _provider.CompressAsync(
            new CompressRequest([sourceFile], gzPath, null), null, CancellationToken.None);

        // Act
        var contents = await _provider.InspectAsync(gzPath, CancellationToken.None);

        // Assert
        contents.Entries.Count.ShouldBe(1);
        contents.TotalCompressedSize.ShouldBeGreaterThan(0);
        contents.TotalUncompressedSize.ShouldBeGreaterThan(0);
        contents.Entries[0].IsDirectory.ShouldBeFalse();
    }

    [Fact]
    public async Task DecompressAsync_GzExtension_OutputFilenameStripsGz()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "report.csv");
        await File.WriteAllTextAsync(sourceFile, "col1,col2\nval1,val2");
        var gzPath = Path.Combine(_tempDir, "report.csv.gz");
        await _provider.CompressAsync(
            new CompressRequest([sourceFile], gzPath, null), null, CancellationToken.None);

        var extractDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(gzPath, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ExtractedFiles.ShouldNotBeNull();
        var outputFileName = Path.GetFileName(result.ExtractedFiles![0]);
        outputFileName.ShouldBe("report.csv");
    }

    [Fact]
    public async Task DecompressAsync_NoGzExtension_OutputFilenameAddsDecompressed()
    {
        // Arrange — create a valid gzip file but with a non-.gz extension
        var sourceFile = Path.Combine(_tempDir, "data.txt");
        await File.WriteAllTextAsync(sourceFile, "some data");
        var gzPath = Path.Combine(_tempDir, "data.txt.gz");
        await _provider.CompressAsync(
            new CompressRequest([sourceFile], gzPath, null), null, CancellationToken.None);

        // Rename to a non-.gz extension
        var renamedPath = Path.Combine(_tempDir, "data.archive");
        File.Move(gzPath, renamedPath);

        var extractDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(renamedPath, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ExtractedFiles.ShouldNotBeNull();
        var outputFileName = Path.GetFileName(result.ExtractedFiles![0]);
        outputFileName.ShouldBe("data.archive.decompressed");
    }

    [Fact]
    public async Task CompressAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(sourceFile, "content");
        var outputPath = Path.Combine(_tempDir, "cancelled.gz");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            _provider.CompressAsync(
                new CompressRequest([sourceFile], outputPath, null), null, cts.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

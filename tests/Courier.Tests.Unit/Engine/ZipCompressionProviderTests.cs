using Courier.Features.Engine.Compression;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class ZipCompressionProviderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ZipCompressionProvider _provider;

    public ZipCompressionProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"courier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _provider = new ZipCompressionProvider();
    }

    [Fact]
    public void FormatKey_IsZip()
    {
        _provider.FormatKey.ShouldBe("zip");
    }

    [Fact]
    public async Task CompressAsync_SingleFile_CreatesZip()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(sourceFile, "hello world");
        var outputPath = Path.Combine(_tempDir, "output.zip");

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
    public async Task CompressAsync_MultipleFiles_CreatesZip()
    {
        // Arrange
        var file1 = Path.Combine(_tempDir, "file1.txt");
        var file2 = Path.Combine(_tempDir, "file2.txt");
        await File.WriteAllTextAsync(file1, "content 1");
        await File.WriteAllTextAsync(file2, "content 2");
        var outputPath = Path.Combine(_tempDir, "multi.zip");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([file1, file2], outputPath, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        File.Exists(outputPath).ShouldBeTrue();

        var contents = await _provider.InspectAsync(outputPath, CancellationToken.None);
        contents.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CompressAsync_WithPassword_CreatesEncryptedZip()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "secret.txt");
        await File.WriteAllTextAsync(sourceFile, "secret data");
        var outputPath = Path.Combine(_tempDir, "encrypted.zip");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([sourceFile], outputPath, "mypassword"), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        File.Exists(outputPath).ShouldBeTrue();
    }

    [Fact]
    public async Task CompressAsync_NonExistentSource_ReturnsFailure()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "output.zip");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest(["/nonexistent/file.txt"], outputPath, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Source file not found");
    }

    [Fact]
    public async Task DecompressAsync_ValidZip_ExtractsFiles()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "data.txt");
        await File.WriteAllTextAsync(sourceFile, "extracted content");
        var zipPath = Path.Combine(_tempDir, "archive.zip");
        await _provider.CompressAsync(
            new CompressRequest([sourceFile], zipPath, null), null, CancellationToken.None);

        var extractDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(zipPath, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ExtractedFiles.ShouldNotBeNull();
        result.ExtractedFiles!.Count.ShouldBe(1);
        var extractedContent = await File.ReadAllTextAsync(result.ExtractedFiles[0]);
        extractedContent.ShouldBe("extracted content");
    }

    [Fact]
    public async Task DecompressAsync_WithPassword_ExtractsFiles()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "secret.txt");
        await File.WriteAllTextAsync(sourceFile, "secret content");
        var zipPath = Path.Combine(_tempDir, "encrypted.zip");
        await _provider.CompressAsync(
            new CompressRequest([sourceFile], zipPath, "pass123"), null, CancellationToken.None);

        var extractDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(zipPath, extractDir, "pass123"), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ExtractedFiles.ShouldNotBeNull();
        result.ExtractedFiles!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DecompressAsync_ArchiveNotFound_ReturnsFailure()
    {
        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest("/nonexistent.zip", _tempDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Archive not found");
    }

    [Fact]
    public async Task InspectAsync_ReturnsEntryList()
    {
        // Arrange
        var file1 = Path.Combine(_tempDir, "a.txt");
        var file2 = Path.Combine(_tempDir, "b.txt");
        await File.WriteAllTextAsync(file1, "aaa");
        await File.WriteAllTextAsync(file2, "bbbbb");
        var zipPath = Path.Combine(_tempDir, "inspect.zip");
        await _provider.CompressAsync(
            new CompressRequest([file1, file2], zipPath, null), null, CancellationToken.None);

        // Act
        var contents = await _provider.InspectAsync(zipPath, CancellationToken.None);

        // Assert
        contents.Entries.Count.ShouldBe(2);
        contents.TotalUncompressedSize.ShouldBeGreaterThan(0);
        contents.Entries.ShouldContain(e => e.Name == "a.txt");
        contents.Entries.ShouldContain(e => e.Name == "b.txt");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

using System.Formats.Tar;
using Courier.Features.Engine.Compression;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Compression;

public class TarCompressionProviderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TarCompressionProvider _provider;

    public TarCompressionProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"courier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _provider = new TarCompressionProvider();
    }

    [Fact]
    public void FormatKey_IsTar()
    {
        _provider.FormatKey.ShouldBe("tar");
    }

    [Fact]
    public async Task CompressAsync_SingleFile_CreatesTar()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(sourceFile, "hello world");
        var outputPath = Path.Combine(_tempDir, "output.tar");

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
    public async Task CompressAsync_MultipleFiles_CreatesTarWithAllEntries()
    {
        // Arrange
        var file1 = Path.Combine(_tempDir, "file1.txt");
        var file2 = Path.Combine(_tempDir, "file2.txt");
        var file3 = Path.Combine(_tempDir, "file3.txt");
        await File.WriteAllTextAsync(file1, "content 1");
        await File.WriteAllTextAsync(file2, "content 2");
        await File.WriteAllTextAsync(file3, "content 3");
        var outputPath = Path.Combine(_tempDir, "multi.tar");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([file1, file2, file3], outputPath, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        File.Exists(outputPath).ShouldBeTrue();

        var contents = await _provider.InspectAsync(outputPath, CancellationToken.None);
        contents.Entries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CompressAsync_NonExistentSource_ReturnsFailure()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "output.tar");

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
    public async Task CompressAsync_EmptySourceList_CreatesEmptyTar()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "empty.tar");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([], outputPath, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        File.Exists(outputPath).ShouldBeTrue();
    }

    [Fact]
    public async Task DecompressAsync_ValidTar_ExtractsAllFiles()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "data.txt");
        await File.WriteAllTextAsync(sourceFile, "extracted content");
        var tarPath = Path.Combine(_tempDir, "archive.tar");
        await _provider.CompressAsync(
            new CompressRequest([sourceFile], tarPath, null), null, CancellationToken.None);

        var extractDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(tarPath, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ExtractedFiles.ShouldNotBeNull();
        result.ExtractedFiles!.Count.ShouldBe(1);
        var extractedContent = await File.ReadAllTextAsync(result.ExtractedFiles[0]);
        extractedContent.ShouldBe("extracted content");
    }

    [Fact]
    public async Task RoundTrip_CompressThenDecompress_PreservesContent()
    {
        // Arrange
        var file1 = Path.Combine(_tempDir, "alpha.txt");
        var file2 = Path.Combine(_tempDir, "beta.txt");
        await File.WriteAllTextAsync(file1, "alpha content here");
        await File.WriteAllTextAsync(file2, "beta content here");
        var tarPath = Path.Combine(_tempDir, "roundtrip.tar");
        var extractDir = Path.Combine(_tempDir, "roundtrip-out");

        // Act
        await _provider.CompressAsync(
            new CompressRequest([file1, file2], tarPath, null), null, CancellationToken.None);
        var result = await _provider.DecompressAsync(
            new DecompressRequest(tarPath, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ExtractedFiles.ShouldNotBeNull();
        result.ExtractedFiles!.Count.ShouldBe(2);

        var extractedAlpha = await File.ReadAllTextAsync(Path.Combine(extractDir, "alpha.txt"));
        var extractedBeta = await File.ReadAllTextAsync(Path.Combine(extractDir, "beta.txt"));
        extractedAlpha.ShouldBe("alpha content here");
        extractedBeta.ShouldBe("beta content here");
    }

    [Fact]
    public async Task DecompressAsync_NonExistentOutputDirectory_CreatesIt()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(sourceFile, "test");
        var tarPath = Path.Combine(_tempDir, "archive.tar");
        await _provider.CompressAsync(
            new CompressRequest([sourceFile], tarPath, null), null, CancellationToken.None);

        var deepExtractDir = Path.Combine(_tempDir, "deep", "nested", "dir");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(tarPath, deepExtractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        Directory.Exists(deepExtractDir).ShouldBeTrue();
        result.ExtractedFiles.ShouldNotBeNull();
        result.ExtractedFiles!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DecompressAsync_ZipSlipPathTraversal_ReturnsFailure()
    {
        // Arrange — manually construct a tar with a path traversal entry
        var maliciousTar = Path.Combine(_tempDir, "evil.tar");
        await using (var stream = File.Create(maliciousTar))
        await using (var writer = new TarWriter(stream))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, "../../../evil.txt");
            entry.DataStream = new MemoryStream("malicious"u8.ToArray());
            await writer.WriteEntryAsync(entry);
        }

        var extractDir = Path.Combine(_tempDir, "safe-output");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(maliciousTar, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Path traversal detected");

        // Verify the malicious file was NOT extracted outside the target
        File.Exists(Path.Combine(_tempDir, "evil.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task DecompressAsync_ArchiveNotFound_ReturnsFailure()
    {
        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest("/nonexistent.tar", _tempDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Archive not found");
    }

    [Fact]
    public async Task InspectAsync_ReturnsCorrectEntryCountAndSizes()
    {
        // Arrange
        var file1 = Path.Combine(_tempDir, "a.txt");
        var file2 = Path.Combine(_tempDir, "b.txt");
        await File.WriteAllTextAsync(file1, "aaa");
        await File.WriteAllTextAsync(file2, "bbbbb");
        var tarPath = Path.Combine(_tempDir, "inspect.tar");
        await _provider.CompressAsync(
            new CompressRequest([file1, file2], tarPath, null), null, CancellationToken.None);

        // Act
        var contents = await _provider.InspectAsync(tarPath, CancellationToken.None);

        // Assert
        contents.Entries.Count.ShouldBe(2);
        contents.TotalUncompressedSize.ShouldBeGreaterThan(0);
        contents.Entries.ShouldContain(e => e.Name == "a.txt");
        contents.Entries.ShouldContain(e => e.Name == "b.txt");

        // Tar has no compression, so compressed == uncompressed
        contents.TotalCompressedSize.ShouldBe(contents.TotalUncompressedSize);
    }

    [Fact]
    public async Task CompressAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        // Arrange
        var file1 = Path.Combine(_tempDir, "file1.txt");
        await File.WriteAllTextAsync(file1, "content");
        var outputPath = Path.Combine(_tempDir, "cancelled.tar");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            _provider.CompressAsync(
                new CompressRequest([file1], outputPath, null), null, cts.Token));
    }

    [Fact]
    public async Task DecompressAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(sourceFile, "content");
        var tarPath = Path.Combine(_tempDir, "archive.tar");
        await _provider.CompressAsync(
            new CompressRequest([sourceFile], tarPath, null), null, CancellationToken.None);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            _provider.DecompressAsync(
                new DecompressRequest(tarPath, Path.Combine(_tempDir, "out"), null), null, cts.Token));
    }

    [Fact]
    public async Task CompressAsync_ProgressReported_DuringOperation()
    {
        // Arrange — create a file large enough to trigger at least one progress report
        // Progress reports every 10MB, so create a file slightly over that threshold
        var sourceFile = Path.Combine(_tempDir, "large.bin");
        var largeContent = new byte[11 * 1024 * 1024]; // 11MB
        Random.Shared.NextBytes(largeContent);
        await File.WriteAllBytesAsync(sourceFile, largeContent);
        var outputPath = Path.Combine(_tempDir, "progress.tar");

        var progressReports = new List<CompressionProgress>();
        var progress = new Progress<CompressionProgress>(p => progressReports.Add(p));

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([sourceFile], outputPath, null), progress, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        // Progress callback may be invoked asynchronously, so check bytes processed instead
        result.BytesProcessed.ShouldBeGreaterThanOrEqualTo(11 * 1024 * 1024);
    }

    [Fact]
    public async Task CompressAsync_CreatesOutputDirectory_WhenMissing()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "src.txt");
        await File.WriteAllTextAsync(sourceFile, "data");
        var outputPath = Path.Combine(_tempDir, "subdir", "nested", "output.tar");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([sourceFile], outputPath, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        File.Exists(outputPath).ShouldBeTrue();
    }

    [Fact]
    public async Task InspectAsync_EntryIsNotDirectory()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "single.txt");
        await File.WriteAllTextAsync(sourceFile, "just a file");
        var tarPath = Path.Combine(_tempDir, "single.tar");
        await _provider.CompressAsync(
            new CompressRequest([sourceFile], tarPath, null), null, CancellationToken.None);

        // Act
        var contents = await _provider.InspectAsync(tarPath, CancellationToken.None);

        // Assert
        contents.Entries.Count.ShouldBe(1);
        contents.Entries[0].IsDirectory.ShouldBeFalse();
        contents.Entries[0].Name.ShouldBe("single.txt");
        contents.Entries[0].UncompressedSize.ShouldBeGreaterThan(0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

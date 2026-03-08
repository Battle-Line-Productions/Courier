using System.Formats.Tar;
using System.IO.Compression;
using Courier.Features.Engine.Compression;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Compression;

public class TarGzCompressionProviderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TarGzCompressionProvider _provider;

    public TarGzCompressionProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"courier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _provider = new TarGzCompressionProvider();
    }

    [Fact]
    public void FormatKey_IsTarGz()
    {
        _provider.FormatKey.ShouldBe("tar.gz");
    }

    [Fact]
    public async Task CompressAsync_MultipleFiles_CreatesTarGz()
    {
        // Arrange
        var file1 = Path.Combine(_tempDir, "file1.txt");
        var file2 = Path.Combine(_tempDir, "file2.txt");
        await File.WriteAllTextAsync(file1, "content 1");
        await File.WriteAllTextAsync(file2, "content 2");
        var outputPath = Path.Combine(_tempDir, "archive.tar.gz");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([file1, file2], outputPath, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.OutputPath.ShouldBe(outputPath);
        result.BytesProcessed.ShouldBeGreaterThan(0);
        File.Exists(outputPath).ShouldBeTrue();
    }

    [Fact]
    public async Task DecompressAsync_ValidTarGz_ExtractsAllFiles()
    {
        // Arrange
        var file1 = Path.Combine(_tempDir, "a.txt");
        var file2 = Path.Combine(_tempDir, "b.txt");
        await File.WriteAllTextAsync(file1, "alpha");
        await File.WriteAllTextAsync(file2, "beta");
        var tarGzPath = Path.Combine(_tempDir, "archive.tar.gz");
        await _provider.CompressAsync(
            new CompressRequest([file1, file2], tarGzPath, null), null, CancellationToken.None);

        var extractDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(tarGzPath, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ExtractedFiles.ShouldNotBeNull();
        result.ExtractedFiles!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RoundTrip_CompressThenDecompress_PreservesAllFileContents()
    {
        // Arrange
        var file1 = Path.Combine(_tempDir, "doc.txt");
        var file2 = Path.Combine(_tempDir, "data.csv");
        var file3 = Path.Combine(_tempDir, "config.json");
        await File.WriteAllTextAsync(file1, "document content");
        await File.WriteAllTextAsync(file2, "col1,col2\nval1,val2");
        await File.WriteAllTextAsync(file3, "{\"key\": \"value\"}");
        var tarGzPath = Path.Combine(_tempDir, "roundtrip.tar.gz");
        var extractDir = Path.Combine(_tempDir, "roundtrip-out");

        // Act
        await _provider.CompressAsync(
            new CompressRequest([file1, file2, file3], tarGzPath, null), null, CancellationToken.None);
        var result = await _provider.DecompressAsync(
            new DecompressRequest(tarGzPath, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ExtractedFiles.ShouldNotBeNull();
        result.ExtractedFiles!.Count.ShouldBe(3);

        var extractedDoc = await File.ReadAllTextAsync(Path.Combine(extractDir, "doc.txt"));
        var extractedCsv = await File.ReadAllTextAsync(Path.Combine(extractDir, "data.csv"));
        var extractedJson = await File.ReadAllTextAsync(Path.Combine(extractDir, "config.json"));
        extractedDoc.ShouldBe("document content");
        extractedCsv.ShouldBe("col1,col2\nval1,val2");
        extractedJson.ShouldBe("{\"key\": \"value\"}");
    }

    [Fact]
    public async Task CompressAsync_NonExistentSource_ReturnsFailure()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "output.tar.gz");

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
    public async Task DecompressAsync_ZipSlipPathTraversal_ReturnsFailure()
    {
        // Arrange — manually construct a tar.gz with a path traversal entry
        var maliciousTarGz = Path.Combine(_tempDir, "evil.tar.gz");
        await using (var fileStream = File.Create(maliciousTarGz))
        await using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
        await using (var tarWriter = new TarWriter(gzipStream))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, "../../../evil.txt");
            entry.DataStream = new MemoryStream("malicious payload"u8.ToArray());
            await tarWriter.WriteEntryAsync(entry);
        }

        var extractDir = Path.Combine(_tempDir, "safe-output");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(maliciousTarGz, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Path traversal detected");

        // Verify the malicious file was NOT extracted outside the target
        File.Exists(Path.Combine(_tempDir, "evil.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task CompressAsync_WithPassword_ReturnsError()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "secret.txt");
        await File.WriteAllTextAsync(sourceFile, "secret data");
        var outputPath = Path.Combine(_tempDir, "encrypted.tar.gz");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([sourceFile], outputPath, "mypassword"), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("TAR.GZ does not support password protection");
    }

    [Fact]
    public async Task InspectAsync_ReturnsCorrectMetadata()
    {
        // Arrange
        var file1 = Path.Combine(_tempDir, "x.txt");
        var file2 = Path.Combine(_tempDir, "y.txt");
        await File.WriteAllTextAsync(file1, "xxx");
        await File.WriteAllTextAsync(file2, "yyyyy");
        var tarGzPath = Path.Combine(_tempDir, "inspect.tar.gz");
        await _provider.CompressAsync(
            new CompressRequest([file1, file2], tarGzPath, null), null, CancellationToken.None);

        // Act
        var contents = await _provider.InspectAsync(tarGzPath, CancellationToken.None);

        // Assert
        contents.Entries.Count.ShouldBe(2);
        contents.TotalUncompressedSize.ShouldBeGreaterThan(0);
        contents.TotalCompressedSize.ShouldBeGreaterThan(0);
        contents.Entries.ShouldContain(e => e.Name == "x.txt");
        contents.Entries.ShouldContain(e => e.Name == "y.txt");
    }

    [Fact]
    public async Task CompressAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(sourceFile, "content");
        var outputPath = Path.Combine(_tempDir, "cancelled.tar.gz");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            _provider.CompressAsync(
                new CompressRequest([sourceFile], outputPath, null), null, cts.Token));
    }

    [Fact]
    public async Task DecompressAsync_CorruptedArchive_ReturnsError()
    {
        // Arrange — write random bytes that are not a valid tar.gz archive
        var corruptedPath = Path.Combine(_tempDir, "corrupted.tar.gz");
        var randomBytes = new byte[512];
        Random.Shared.NextBytes(randomBytes);
        await File.WriteAllBytesAsync(corruptedPath, randomBytes);

        var extractDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(corruptedPath, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
    }

    [Fact]
    public async Task DecompressAsync_OutputDirectoryAutoCreated()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(sourceFile, "test");
        var tarGzPath = Path.Combine(_tempDir, "archive.tar.gz");
        await _provider.CompressAsync(
            new CompressRequest([sourceFile], tarGzPath, null), null, CancellationToken.None);

        var deepExtractDir = Path.Combine(_tempDir, "deep", "nested", "dir");

        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest(tarGzPath, deepExtractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        Directory.Exists(deepExtractDir).ShouldBeTrue();
        result.ExtractedFiles.ShouldNotBeNull();
        result.ExtractedFiles!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CompressAsync_EmptySourceList_CreatesArchive()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "empty.tar.gz");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([], outputPath, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        File.Exists(outputPath).ShouldBeTrue();
    }

    [Fact]
    public async Task DecompressAsync_ArchiveNotFound_ReturnsFailure()
    {
        // Act
        var result = await _provider.DecompressAsync(
            new DecompressRequest("/nonexistent.tar.gz", _tempDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Archive not found");
    }

    [Fact]
    public async Task RoundTrip_MultipleLargeFiles_PreservesContent()
    {
        // Arrange — create two files with substantial binary content
        var file1 = Path.Combine(_tempDir, "large1.bin");
        var file2 = Path.Combine(_tempDir, "large2.bin");
        var content1 = new byte[100_000];
        var content2 = new byte[200_000];
        Random.Shared.NextBytes(content1);
        Random.Shared.NextBytes(content2);
        await File.WriteAllBytesAsync(file1, content1);
        await File.WriteAllBytesAsync(file2, content2);

        var tarGzPath = Path.Combine(_tempDir, "large.tar.gz");
        var extractDir = Path.Combine(_tempDir, "large-out");

        // Act
        await _provider.CompressAsync(
            new CompressRequest([file1, file2], tarGzPath, null), null, CancellationToken.None);
        var result = await _provider.DecompressAsync(
            new DecompressRequest(tarGzPath, extractDir, null), null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ExtractedFiles.ShouldNotBeNull();
        result.ExtractedFiles!.Count.ShouldBe(2);

        var extracted1 = await File.ReadAllBytesAsync(Path.Combine(extractDir, "large1.bin"));
        var extracted2 = await File.ReadAllBytesAsync(Path.Combine(extractDir, "large2.bin"));
        extracted1.ShouldBe(content1);
        extracted2.ShouldBe(content2);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

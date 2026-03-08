using Courier.Features.Engine.Compression;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Compression;

public class SplitArchiveTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ZipCompressionProvider _provider;

    public SplitArchiveTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"courier-split-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _provider = new ZipCompressionProvider();
    }

    /// <summary>
    /// Creates a file with random (incompressible) data so ZIP can't shrink it.
    /// </summary>
    private string CreateRandomFile(string name, int sizeBytes)
    {
        var path = Path.Combine(_tempDir, name);
        var data = new byte[sizeBytes];
        Random.Shared.NextBytes(data);
        File.WriteAllBytes(path, data);
        return path;
    }

    [Fact]
    public async Task CompressAsync_FileSmallerThanSplitSize_SingleFile()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "small.txt");
        await File.WriteAllTextAsync(sourceFile, "small content");
        var outputPath = Path.Combine(_tempDir, "small.zip");

        // Act — split size is much larger than the file
        var result = await _provider.CompressAsync(
            new CompressRequest([sourceFile], outputPath, null, SplitMaxSizeBytes: 10 * 1024 * 1024),
            null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.SplitParts.ShouldNotBeNull();
        result.SplitParts!.Count.ShouldBe(1);
        result.SplitParts[0].ShouldBe(outputPath);
    }

    [Fact]
    public async Task CompressAsync_FileLargerThanSplitSize_CorrectParts()
    {
        // Arrange — random data is incompressible, so 50KB file → ~50KB ZIP
        var sourceFile = CreateRandomFile("large.bin", 50_000);
        var outputPath = Path.Combine(_tempDir, "large.zip");

        // Act — split at 10KB
        var result = await _provider.CompressAsync(
            new CompressRequest([sourceFile], outputPath, null, SplitMaxSizeBytes: 10_000),
            null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.SplitParts.ShouldNotBeNull();
        result.SplitParts!.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task CompressAsync_SplitParts_FollowNamingConvention()
    {
        // Arrange — random data to ensure large enough ZIP
        var sourceFile = CreateRandomFile("naming.bin", 100_000);
        var outputPath = Path.Combine(_tempDir, "naming.zip");

        // Act — split at 10KB to guarantee multiple parts
        var result = await _provider.CompressAsync(
            new CompressRequest([sourceFile], outputPath, null, SplitMaxSizeBytes: 10_000),
            null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.SplitParts.ShouldNotBeNull();
        result.SplitParts!.Count.ShouldBeGreaterThan(1);

        // First part keeps the original name
        result.SplitParts[0].ShouldBe(outputPath);

        // Subsequent parts use .z01, .z02, etc.
        for (var i = 1; i < result.SplitParts.Count; i++)
        {
            var expectedSuffix = $".z{i:D2}";
            Path.GetExtension(result.SplitParts[i]).ShouldBe(expectedSuffix);
        }
    }

    [Fact]
    public async Task CompressAsync_AllPartsSumCorrectly()
    {
        // Arrange — random data is incompressible
        var sourceFile = CreateRandomFile("sum.bin", 80_000);
        var outputPath = Path.Combine(_tempDir, "sum.zip");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([sourceFile], outputPath, null, SplitMaxSizeBytes: 10_000),
            null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.SplitParts.ShouldNotBeNull();
        result.SplitParts!.Count.ShouldBeGreaterThan(1);

        // All parts should exist and have content
        var totalPartSize = result.SplitParts.Sum(p => new FileInfo(p).Length);
        totalPartSize.ShouldBeGreaterThan(0);

        foreach (var part in result.SplitParts)
        {
            File.Exists(part).ShouldBeTrue($"Part file should exist: {part}");
        }
    }

    [Fact]
    public async Task CompressAsync_SplitResult_IncludesAllPartPaths()
    {
        // Arrange — random data
        var sourceFile = CreateRandomFile("paths.bin", 60_000);
        var outputPath = Path.Combine(_tempDir, "paths.zip");

        // Act
        var result = await _provider.CompressAsync(
            new CompressRequest([sourceFile], outputPath, null, SplitMaxSizeBytes: 8_000),
            null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.SplitParts.ShouldNotBeNull();

        // Every returned path should be an actual file
        foreach (var partPath in result.SplitParts!)
        {
            File.Exists(partPath).ShouldBeTrue($"Part {partPath} should exist on disk");
            new FileInfo(partPath).Length.ShouldBeGreaterThan(0,
                $"Part {partPath} should have content");
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best effort cleanup
        }
    }
}

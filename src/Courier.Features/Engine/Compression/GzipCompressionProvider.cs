using System.IO.Compression;

namespace Courier.Features.Engine.Compression;

public class GzipCompressionProvider : ICompressionProvider
{
    private const int BufferSize = 81920; // 80KB
    private const long ProgressIntervalBytes = 10 * 1024 * 1024; // 10MB

    // Decompression bomb protection limits
    private const long MaxTotalExtractedSize = 10L * 1024 * 1024 * 1024; // 10 GB
    private const long MaxCompressionRatio = 100; // 100:1
    private const long MaxSingleEntrySize = 5L * 1024 * 1024 * 1024; // 5 GB

    public string FormatKey => "gzip";

    public async Task<CompressionResult> CompressAsync(
        CompressRequest request,
        IProgress<CompressionProgress>? progress,
        CancellationToken ct)
    {
        try
        {
            // GZIP is single-file compression only
            if (request.SourcePaths.Count != 1)
                return new CompressionResult(false, 0, request.OutputPath, null,
                    "GZIP compression supports only a single source file");

            var sourcePath = request.SourcePaths[0];

            if (!File.Exists(sourcePath))
                return new CompressionResult(false, 0, request.OutputPath, null,
                    $"Source file not found: {sourcePath}");

            if (!string.IsNullOrEmpty(request.Password))
                return new CompressionResult(false, 0, request.OutputPath, null,
                    "GZIP does not support password protection");

            var outputDir = Path.GetDirectoryName(request.OutputPath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            long totalBytesProcessed = 0;
            long lastProgressReport = 0;

            await using var inputStream = File.OpenRead(sourcePath);
            await using var outputFileStream = File.Create(request.OutputPath);
            await using var gzipStream = new GZipStream(outputFileStream, CompressionLevel.Optimal);

            var buffer = new byte[BufferSize];
            int bytesRead;
            while ((bytesRead = await inputStream.ReadAsync(buffer, ct)) > 0)
            {
                await gzipStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalBytesProcessed += bytesRead;

                if (progress is not null && totalBytesProcessed - lastProgressReport >= ProgressIntervalBytes)
                {
                    progress.Report(new CompressionProgress(totalBytesProcessed, 0, "Compressing"));
                    lastProgressReport = totalBytesProcessed;
                }
            }

            return new CompressionResult(true, totalBytesProcessed, request.OutputPath, null, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new CompressionResult(false, 0, request.OutputPath, null, ex.Message);
        }
    }

    public async Task<CompressionResult> DecompressAsync(
        DecompressRequest request,
        IProgress<CompressionProgress>? progress,
        CancellationToken ct)
    {
        try
        {
            if (!File.Exists(request.ArchivePath))
                return new CompressionResult(false, 0, request.OutputDirectory, null,
                    $"Archive not found: {request.ArchivePath}");

            Directory.CreateDirectory(request.OutputDirectory);

            // Output filename = archive filename without .gz extension
            var archiveFileName = Path.GetFileName(request.ArchivePath);
            var outputFileName = archiveFileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                ? archiveFileName[..^3]
                : archiveFileName + ".decompressed";

            var outputPath = Path.Combine(request.OutputDirectory, outputFileName);
            var outputRoot = Path.GetFullPath(request.OutputDirectory);
            var fullOutputPath = Path.GetFullPath(outputPath);

            // Path traversal protection
            if (!fullOutputPath.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                return new CompressionResult(false, 0, request.OutputDirectory, null,
                    "Path traversal detected: output path would escape target directory");

            var archiveSize = new FileInfo(request.ArchivePath).Length;
            long totalBytesProcessed = 0;
            long lastProgressReport = 0;

            await using var inputFileStream = File.OpenRead(request.ArchivePath);
            await using var gzipStream = new GZipStream(inputFileStream, CompressionMode.Decompress);
            await using var outputFileStream = File.Create(fullOutputPath);

            var buffer = new byte[BufferSize];
            int bytesRead;
            while ((bytesRead = await gzipStream.ReadAsync(buffer, ct)) > 0)
            {
                await outputFileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalBytesProcessed += bytesRead;

                if (progress is not null && totalBytesProcessed - lastProgressReport >= ProgressIntervalBytes)
                {
                    progress.Report(new CompressionProgress(totalBytesProcessed, 0, "Decompressing"));
                    lastProgressReport = totalBytesProcessed;
                }

                // Check single entry / total size limit (GZIP is single-file, so they're the same)
                if (totalBytesProcessed > MaxSingleEntrySize)
                    return new CompressionResult(false, totalBytesProcessed, request.OutputDirectory, null,
                        "Decompression bomb detected: decompressed size exceeds 5 GB limit");

                // Check compression ratio limit
                if (archiveSize > 0 && totalBytesProcessed / archiveSize > MaxCompressionRatio)
                    return new CompressionResult(false, totalBytesProcessed, request.OutputDirectory, null,
                        "Decompression bomb detected: compression ratio exceeds 100:1");
            }

            // Check total extracted size limit
            if (totalBytesProcessed > MaxTotalExtractedSize)
                return new CompressionResult(false, totalBytesProcessed, request.OutputDirectory, null,
                    "Decompression bomb detected: total extracted size exceeds 10 GB limit");

            // Integrity verification: GZipStream automatically verifies the CRC-32 and
            // ISIZE fields in the GZIP footer during decompression. If the checksum does
            // not match, an InvalidDataException is thrown before we reach this point.
            // The VerifyIntegrity flag is preserved for future use (e.g., post-extraction
            // re-read verification or additional hash-based checks).

            return new CompressionResult(true, totalBytesProcessed, request.OutputDirectory,
                [fullOutputPath], null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException ex)
        {
            return new CompressionResult(false, 0, request.OutputDirectory, null,
                $"Invalid GZIP archive: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new CompressionResult(false, 0, request.OutputDirectory, null, ex.Message);
        }
    }

    public async Task<ArchiveContents> InspectAsync(string archivePath, CancellationToken ct)
    {
        var archiveFileInfo = new FileInfo(archivePath);
        var compressedSize = archiveFileInfo.Length;

        // To get the uncompressed size, we must decompress and count bytes
        // (GZIP footer has original size mod 2^32, which is unreliable for large files)
        long uncompressedSize = 0;

        await using var inputFileStream = File.OpenRead(archivePath);
        await using var gzipStream = new GZipStream(inputFileStream, CompressionMode.Decompress);

        var buffer = new byte[BufferSize];
        int bytesRead;
        while ((bytesRead = await gzipStream.ReadAsync(buffer, ct)) > 0)
        {
            uncompressedSize += bytesRead;

            // Safety limit for inspection
            if (uncompressedSize > MaxTotalExtractedSize)
                break;
        }

        var outputFileName = archivePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(archivePath)[..^3]
            : Path.GetFileName(archivePath) + ".decompressed";

        var entries = new List<ArchiveEntry>
        {
            new(outputFileName, compressedSize, uncompressedSize,
                archiveFileInfo.LastWriteTime, false)
        };

        return new ArchiveContents(entries, compressedSize, uncompressedSize);
    }
}

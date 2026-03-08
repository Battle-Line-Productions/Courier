using System.Formats.Tar;

namespace Courier.Features.Engine.Compression;

public class TarCompressionProvider : ICompressionProvider
{
    private const int BufferSize = 81920; // 80KB
    private const long ProgressIntervalBytes = 10 * 1024 * 1024; // 10MB

    // Decompression bomb protection limits
    private const long MaxTotalExtractedSize = 10L * 1024 * 1024 * 1024; // 10 GB
    private const int MaxFileCount = 10_000;
    private const long MaxCompressionRatio = 100; // 100:1
    private const long MaxSingleEntrySize = 5L * 1024 * 1024 * 1024; // 5 GB

    public string FormatKey => "tar";

    public async Task<CompressionResult> CompressAsync(
        CompressRequest request,
        IProgress<CompressionProgress>? progress,
        CancellationToken ct)
    {
        try
        {
            foreach (var sourcePath in request.SourcePaths)
            {
                if (!File.Exists(sourcePath))
                    return new CompressionResult(false, 0, request.OutputPath, null,
                        $"Source file not found: {sourcePath}");
            }

            var outputDir = Path.GetDirectoryName(request.OutputPath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            long totalBytesProcessed = 0;
            long lastProgressReport = 0;

            await using var outputStream = File.Create(request.OutputPath);
            await using var tarWriter = new TarWriter(outputStream);

            foreach (var sourcePath in request.SourcePaths)
            {
                ct.ThrowIfCancellationRequested();

                var entryName = Path.GetFileName(sourcePath);
                var fileInfo = new FileInfo(sourcePath);

                await tarWriter.WriteEntryAsync(sourcePath, entryName, ct);
                totalBytesProcessed += fileInfo.Length;

                if (progress is not null && totalBytesProcessed - lastProgressReport >= ProgressIntervalBytes)
                {
                    progress.Report(new CompressionProgress(totalBytesProcessed, 0, "Archiving"));
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

            var outputRoot = Path.GetFullPath(request.OutputDirectory);
            long totalBytesProcessed = 0;
            long lastProgressReport = 0;
            var extractedFiles = new List<string>();

            var archiveSize = new FileInfo(request.ArchivePath).Length;

            await using var inputStream = File.OpenRead(request.ArchivePath);
            await using var tarReader = new TarReader(inputStream);

            while (await tarReader.GetNextEntryAsync(true, ct) is { } entry)
            {
                ct.ThrowIfCancellationRequested();

                var outputPath = Path.GetFullPath(Path.Combine(request.OutputDirectory, entry.Name));

                // Zip Slip protection
                if (!outputPath.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                    return new CompressionResult(false, totalBytesProcessed, request.OutputDirectory, null,
                        $"Path traversal detected: entry '{entry.Name}' would extract outside target directory");

                if (entry.EntryType == TarEntryType.Directory)
                {
                    Directory.CreateDirectory(outputPath);
                    continue;
                }

                // Only extract regular files
                if (entry.EntryType != TarEntryType.RegularFile)
                    continue;

                var entryDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(entryDir))
                    Directory.CreateDirectory(entryDir);

                // Check single entry size limit before extracting
                if (entry.Length > MaxSingleEntrySize)
                    return new CompressionResult(false, totalBytesProcessed, request.OutputDirectory, null,
                        "Decompression bomb detected: single entry exceeds 5 GB limit");

                await using var outputFileStream = File.Create(outputPath);
                long entryBytesWritten = 0;

                if (entry.DataStream is not null)
                {
                    var buffer = new byte[BufferSize];
                    int bytesRead;
                    while ((bytesRead = await entry.DataStream.ReadAsync(buffer, ct)) > 0)
                    {
                        await outputFileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                        totalBytesProcessed += bytesRead;
                        entryBytesWritten += bytesRead;

                        if (progress is not null && totalBytesProcessed - lastProgressReport >= ProgressIntervalBytes)
                        {
                            progress.Report(new CompressionProgress(totalBytesProcessed, 0, "Extracting"));
                            lastProgressReport = totalBytesProcessed;
                        }

                        // Check single entry size limit during extraction
                        if (entryBytesWritten > MaxSingleEntrySize)
                            return new CompressionResult(false, totalBytesProcessed, request.OutputDirectory, null,
                                "Decompression bomb detected: single entry exceeds 5 GB limit");
                    }
                }

                // Check total extracted size limit
                if (totalBytesProcessed > MaxTotalExtractedSize)
                    return new CompressionResult(false, totalBytesProcessed, request.OutputDirectory, null,
                        "Decompression bomb detected: total extracted size exceeds 10 GB limit");

                extractedFiles.Add(outputPath);

                // Check file count limit
                if (extractedFiles.Count > MaxFileCount)
                    return new CompressionResult(false, totalBytesProcessed, request.OutputDirectory, null,
                        "Decompression bomb detected: archive contains more than 10000 files");

                // Check compression ratio limit
                if (archiveSize > 0 && totalBytesProcessed / archiveSize > MaxCompressionRatio)
                    return new CompressionResult(false, totalBytesProcessed, request.OutputDirectory, null,
                        "Decompression bomb detected: compression ratio exceeds 100:1");
            }

            // Integrity verification: The TAR format includes header checksums for each
            // entry. System.Formats.Tar validates these checksums when reading entries via
            // GetNextEntryAsync(). If a header checksum is invalid, an exception is thrown
            // during iteration. TAR does not include per-file content checksums (unlike ZIP),
            // so content-level verification would require an additional hash pass. The
            // VerifyIntegrity flag is preserved for future content-hash verification support.

            return new CompressionResult(true, totalBytesProcessed, request.OutputDirectory, extractedFiles, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new CompressionResult(false, 0, request.OutputDirectory, null, ex.Message);
        }
    }

    public async Task<ArchiveContents> InspectAsync(string archivePath, CancellationToken ct)
    {
        var entries = new List<ArchiveEntry>();
        long totalSize = 0;

        await using var inputStream = File.OpenRead(archivePath);
        await using var tarReader = new TarReader(inputStream);

        while (await tarReader.GetNextEntryAsync(true, ct) is { } entry)
        {
            ct.ThrowIfCancellationRequested();

            entries.Add(new ArchiveEntry(
                entry.Name,
                entry.Length, // Tar has no compression, so compressed == uncompressed
                entry.Length,
                entry.ModificationTime.DateTime,
                entry.EntryType == TarEntryType.Directory));

            totalSize += entry.Length;
        }

        // Tar is archive-only (no compression), so compressed == uncompressed
        return new ArchiveContents(entries, totalSize, totalSize);
    }
}

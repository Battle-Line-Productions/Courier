using ICSharpCode.SharpZipLib.Zip;

namespace Courier.Features.Engine.Compression;

public class ZipCompressionProvider : ICompressionProvider
{
    private const int BufferSize = 81920; // 80KB
    private const long ProgressIntervalBytes = 10 * 1024 * 1024; // 10MB

    // Decompression bomb protection limits
    private const long MaxTotalExtractedSize = 10L * 1024 * 1024 * 1024; // 10 GB
    private const int MaxFileCount = 10_000;
    private const long MaxCompressionRatio = 100; // 100:1
    private const long MaxSingleEntrySize = 5L * 1024 * 1024 * 1024; // 5 GB

    public string FormatKey => "zip";

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

            // Scope the ZIP streams so they are closed before any post-compression split
            {
                await using var outputStream = File.Create(request.OutputPath);
                using var zipStream = new ZipOutputStream(outputStream);

                if (!string.IsNullOrEmpty(request.Password))
                {
                    zipStream.Password = request.Password;
                }

                zipStream.SetLevel(6); // Default compression level

                var buffer = new byte[BufferSize];

                foreach (var sourcePath in request.SourcePaths)
                {
                    ct.ThrowIfCancellationRequested();

                    var entryName = Path.GetFileName(sourcePath);
                    var entry = new ZipEntry(entryName)
                    {
                        DateTime = File.GetLastWriteTime(sourcePath),
                    };

                    if (!string.IsNullOrEmpty(request.Password))
                    {
                        entry.AESKeySize = 256;
                    }

                    zipStream.PutNextEntry(entry);

                    await using var inputStream = File.OpenRead(sourcePath);
                    int bytesRead;
                    while ((bytesRead = await inputStream.ReadAsync(buffer, ct)) > 0)
                    {
                        await zipStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                        totalBytesProcessed += bytesRead;

                        if (progress is not null && totalBytesProcessed - lastProgressReport >= ProgressIntervalBytes)
                        {
                            progress.Report(new CompressionProgress(totalBytesProcessed, 0, "Compressing"));
                            lastProgressReport = totalBytesProcessed;
                        }
                    }

                    zipStream.CloseEntry();
                }
            }

            // Split archive into parts if requested (streams are closed at this point)
            if (request.SplitMaxSizeBytes is > 0)
            {
                var splitParts = await SplitFileAsync(request.OutputPath, request.SplitMaxSizeBytes.Value, ct);
                return new CompressionResult(true, totalBytesProcessed, request.OutputPath, null, null, splitParts);
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

            using var zipFile = await Task.Run(() => new ZipFile(request.ArchivePath), ct);

            if (!string.IsNullOrEmpty(request.Password))
            {
                zipFile.Password = request.Password;
            }

            var buffer = new byte[BufferSize];

            foreach (ZipEntry entry in zipFile)
            {
                ct.ThrowIfCancellationRequested();

                var outputPath = Path.GetFullPath(Path.Combine(request.OutputDirectory, entry.Name));

                // Zip Slip protection
                if (!outputPath.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                    return new CompressionResult(false, totalBytesProcessed, request.OutputDirectory, null,
                        $"Zip Slip detected: entry '{entry.Name}' would extract outside target directory");

                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(outputPath);
                    continue;
                }

                var entryDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(entryDir))
                    Directory.CreateDirectory(entryDir);

                await using var entryStream = zipFile.GetInputStream(entry);
                await using var outputFileStream = File.Create(outputPath);
                long entryBytesWritten = 0;
                int bytesRead;
                while ((bytesRead = await entryStream.ReadAsync(buffer, ct)) > 0)
                {
                    await outputFileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalBytesProcessed += bytesRead;
                    entryBytesWritten += bytesRead;

                    if (progress is not null && totalBytesProcessed - lastProgressReport >= ProgressIntervalBytes)
                    {
                        progress.Report(new CompressionProgress(totalBytesProcessed, 0, "Extracting"));
                        lastProgressReport = totalBytesProcessed;
                    }

                    // Check single entry size limit
                    if (entryBytesWritten > MaxSingleEntrySize)
                        return new CompressionResult(false, totalBytesProcessed, request.OutputDirectory, null,
                            "Decompression bomb detected: single entry exceeds 5 GB limit");
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

            // Integrity verification: SharpZipLib automatically verifies CRC-32 checksums
            // during extraction via GetInputStream(). If any entry's CRC does not match,
            // a ZipException is thrown before we reach this point. When VerifyIntegrity is
            // true, extraction itself serves as the integrity check — no additional pass needed.
            // The VerifyIntegrity flag is preserved for future use (e.g., post-extraction
            // re-read verification or hash-based checks for non-ZIP formats).

            return new CompressionResult(true, totalBytesProcessed, request.OutputDirectory, extractedFiles, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ZipException ex) when (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
                                      || ex.Message.Contains("Invalid password", StringComparison.OrdinalIgnoreCase))
        {
            return new CompressionResult(false, 0, request.OutputDirectory, null,
                "Invalid password or corrupted archive");
        }
        catch (Exception ex)
        {
            return new CompressionResult(false, 0, request.OutputDirectory, null, ex.Message);
        }
    }

    private static async Task<List<string>> SplitFileAsync(string filePath, long maxSizeBytes, CancellationToken ct)
    {
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length <= maxSizeBytes)
            return [filePath]; // No split needed — file is already within the size limit

        var parts = new List<string>();
        var baseName = Path.GetFileNameWithoutExtension(filePath);
        var dir = Path.GetDirectoryName(filePath)!;

        // Write part 0 to a temp file to avoid conflicting with the source read stream
        var tempPart0 = Path.Combine(dir, $"{baseName}.z00.tmp");

        await using (var sourceStream = File.OpenRead(filePath))
        {
            var buffer = new byte[BufferSize];
            var partNumber = 0;

            while (sourceStream.Position < sourceStream.Length)
            {
                ct.ThrowIfCancellationRequested();

                var partName = partNumber == 0
                    ? tempPart0
                    : Path.Combine(dir, $"{baseName}.z{partNumber:D2}");

                var bytesRemaining = maxSizeBytes;
                await using var partStream = File.Create(partName);

                while (bytesRemaining > 0 && sourceStream.Position < sourceStream.Length)
                {
                    var toRead = (int)Math.Min(buffer.Length, bytesRemaining);
                    var bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, toRead), ct);
                    if (bytesRead == 0) break;

                    await partStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    bytesRemaining -= bytesRead;
                }

                parts.Add(partNumber == 0 ? filePath : partName);
                partNumber++;
            }
        }

        // Replace the original file with the first part
        File.Delete(filePath);
        File.Move(tempPart0, filePath);

        return parts;
    }

    public async Task<ArchiveContents> InspectAsync(string archivePath, CancellationToken ct)
    {
        var entries = new List<ArchiveEntry>();
        long totalCompressed = 0;
        long totalUncompressed = 0;

        using var zipFile = await Task.Run(() => new ZipFile(archivePath), ct);

        foreach (ZipEntry entry in zipFile)
        {
            ct.ThrowIfCancellationRequested();

            entries.Add(new ArchiveEntry(
                entry.Name,
                entry.CompressedSize,
                entry.Size,
                entry.DateTime,
                entry.IsDirectory));

            totalCompressed += entry.CompressedSize;
            totalUncompressed += entry.Size;
        }

        return new ArchiveContents(entries, totalCompressed, totalUncompressed);
    }
}

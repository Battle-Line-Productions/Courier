using ICSharpCode.SharpZipLib.Zip;

namespace Courier.Features.Engine.Compression;

public class ZipCompressionProvider : ICompressionProvider
{
    private const int BufferSize = 81920; // 80KB
    private const long ProgressIntervalBytes = 10 * 1024 * 1024; // 10MB

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
                int bytesRead;
                while ((bytesRead = await entryStream.ReadAsync(buffer, ct)) > 0)
                {
                    await outputFileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalBytesProcessed += bytesRead;

                    if (progress is not null && totalBytesProcessed - lastProgressReport >= ProgressIntervalBytes)
                    {
                        progress.Report(new CompressionProgress(totalBytesProcessed, 0, "Extracting"));
                        lastProgressReport = totalBytesProcessed;
                    }
                }

                extractedFiles.Add(outputPath);
            }

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

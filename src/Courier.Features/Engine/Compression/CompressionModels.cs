namespace Courier.Features.Engine.Compression;

public record CompressRequest(
    IReadOnlyList<string> SourcePaths,
    string OutputPath,
    string? Password,
    long? SplitMaxSizeBytes = null);

public record DecompressRequest(
    string ArchivePath,
    string OutputDirectory,
    string? Password,
    bool VerifyIntegrity = true);

public record CompressionResult(
    bool Success,
    long BytesProcessed,
    string OutputPath,
    IReadOnlyList<string>? ExtractedFiles,
    string? ErrorMessage,
    IReadOnlyList<string>? SplitParts = null);

public record CompressionProgress(
    long BytesProcessed,
    long TotalBytes,
    string Operation);

public record ArchiveContents(
    IReadOnlyList<ArchiveEntry> Entries,
    long TotalCompressedSize,
    long TotalUncompressedSize);

public record ArchiveEntry(
    string Name,
    long CompressedSize,
    long UncompressedSize,
    DateTime LastModified,
    bool IsDirectory);

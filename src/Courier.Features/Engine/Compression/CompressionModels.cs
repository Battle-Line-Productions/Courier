namespace Courier.Features.Engine.Compression;

public record CompressRequest(
    IReadOnlyList<string> SourcePaths,
    string OutputPath,
    string? Password);

public record DecompressRequest(
    string ArchivePath,
    string OutputDirectory,
    string? Password);

public record CompressionResult(
    bool Success,
    long BytesProcessed,
    string OutputPath,
    IReadOnlyList<string>? ExtractedFiles,
    string? ErrorMessage);

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

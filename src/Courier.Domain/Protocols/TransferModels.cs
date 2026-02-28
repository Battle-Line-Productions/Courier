namespace Courier.Domain.Protocols;

public record UploadRequest(
    string LocalPath,
    string RemotePath,
    bool AtomicUpload = true,
    string AtomicSuffix = ".tmp",
    bool ResumePartial = false);

public record DownloadRequest(
    string RemotePath,
    string LocalPath,
    bool ResumePartial = false,
    string FilePattern = "*",
    bool DeleteAfterDownload = false);

public record TransferProgress(
    long BytesTransferred,
    long TotalBytes,
    string CurrentFile,
    double TransferRateBytesPerSec);

public record RemoteFileInfo(
    string Name,
    string FullPath,
    long Size,
    DateTime LastModified,
    bool IsDirectory);

public record ConnectionTestResult(
    bool Success,
    TimeSpan Latency,
    string? ServerBanner,
    string? ErrorMessage,
    IReadOnlyList<string>? SupportedAlgorithms);

namespace Courier.Domain.Protocols;

public interface ITransferClient : IAsyncDisposable
{
    string Protocol { get; }
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken ct);
    Task DisconnectAsync();

    Task UploadAsync(UploadRequest request, IProgress<TransferProgress>? progress, CancellationToken ct);
    Task DownloadAsync(DownloadRequest request, IProgress<TransferProgress>? progress, CancellationToken ct);
    Task RenameAsync(string oldPath, string newPath, CancellationToken ct);
    Task DeleteFileAsync(string remotePath, CancellationToken ct);

    Task<IReadOnlyList<RemoteFileInfo>> ListDirectoryAsync(string remotePath, CancellationToken ct);
    Task CreateDirectoryAsync(string remotePath, CancellationToken ct);
    Task DeleteDirectoryAsync(string remotePath, bool recursive, CancellationToken ct);

    Task<ConnectionTestResult> TestAsync(CancellationToken ct);
}

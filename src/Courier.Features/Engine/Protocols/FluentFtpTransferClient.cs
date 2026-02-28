using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using FluentFTP;

namespace Courier.Features.Engine.Protocols;

public class FluentFtpTransferClient : ITransferClient
{
    private readonly Connection _connection;
    private readonly byte[]? _decryptedPassword;
    private readonly FtpEncryptionMode _encryptionMode;
    private AsyncFtpClient? _client;

    public FluentFtpTransferClient(Connection connection, byte[]? decryptedPassword, FtpEncryptionMode encryptionMode)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _decryptedPassword = decryptedPassword;
        _encryptionMode = encryptionMode;
    }

    public string Protocol => _encryptionMode == FtpEncryptionMode.None ? "ftp" : "ftps";

    public bool IsConnected => _client?.IsConnected ?? false;

    public async Task ConnectAsync(CancellationToken ct)
    {
        var password = _decryptedPassword is { Length: > 0 }
            ? System.Text.Encoding.UTF8.GetString(_decryptedPassword)
            : string.Empty;

        _client = new AsyncFtpClient(
            _connection.Host,
            _connection.Username,
            password,
            _connection.Port);

        _client.Config.EncryptionMode = _encryptionMode;
        _client.Config.ConnectTimeout = _connection.ConnectTimeoutSec * 1000;
        _client.Config.ReadTimeout = _connection.OperationTimeoutSec * 1000;
        _client.Config.DataConnectionConnectTimeout = _connection.ConnectTimeoutSec * 1000;
        _client.Config.DataConnectionReadTimeout = _connection.OperationTimeoutSec * 1000;

        _client.Config.DataConnectionType = _connection.PassiveMode
            ? FtpDataConnectionType.AutoPassive
            : FtpDataConnectionType.AutoActive;

        if (_connection.TransportRetries > 0)
        {
            _client.Config.RetryAttempts = _connection.TransportRetries;
        }

        if (_encryptionMode != FtpEncryptionMode.None)
        {
            _client.ValidateCertificate += (control, e) =>
            {
                switch (_connection.TlsCertPolicy.ToLowerInvariant())
                {
                    case "system_trust":
                        e.Accept = e.PolicyErrors == SslPolicyErrors.None;
                        break;

                    case "pinned_thumbprint":
                        if (!string.IsNullOrEmpty(_connection.TlsPinnedThumbprint) && e.Certificate is not null)
                        {
                            using var cert = new X509Certificate2(e.Certificate);
                            var thumbprint = Convert.ToHexString(
                                SHA256.HashData(cert.RawData)).ToLowerInvariant();
                            e.Accept = string.Equals(
                                _connection.TlsPinnedThumbprint,
                                thumbprint,
                                StringComparison.OrdinalIgnoreCase);
                        }
                        else
                        {
                            e.Accept = false;
                        }
                        break;

                    case "insecure":
                        e.Accept = true;
                        break;

                    default:
                        e.Accept = false;
                        break;
                }
            };
        }

        await _client.Connect(ct);
    }

    public async Task DisconnectAsync()
    {
        if (_client?.IsConnected == true)
            await _client.Disconnect();
    }

    public async Task UploadAsync(UploadRequest request, IProgress<TransferProgress>? progress, CancellationToken ct)
    {
        EnsureConnected();

        var targetPath = request.AtomicUpload
            ? request.RemotePath + request.AtomicSuffix
            : request.RemotePath;

        var existsMode = request.ResumePartial
            ? FtpRemoteExists.Resume
            : FtpRemoteExists.Overwrite;

        var fileInfo = new FileInfo(request.LocalPath);
        var totalBytes = fileInfo.Length;

        var sw = Stopwatch.StartNew();
        long lastReportedBytes = 0;
        var lastReportTime = Stopwatch.GetTimestamp();
        const long reportIntervalBytes = 1024 * 1024;
        var reportIntervalTicks = Stopwatch.Frequency * 5;

        // FluentFTP progress callback
        IProgress<FtpProgress>? ftpProgress = progress is not null
            ? new Progress<FtpProgress>(p =>
            {
                var bytesTransferred = (long)(totalBytes * p.Progress / 100.0);
                var now = Stopwatch.GetTimestamp();
                var bytesDelta = bytesTransferred - lastReportedBytes;
                var timeDelta = now - lastReportTime;

                if (bytesDelta >= reportIntervalBytes || timeDelta >= reportIntervalTicks)
                {
                    progress.Report(new TransferProgress(
                        bytesTransferred,
                        totalBytes,
                        Path.GetFileName(request.LocalPath),
                        p.TransferSpeed));

                    lastReportedBytes = bytesTransferred;
                    lastReportTime = now;
                }
            })
            : null;

        var status = await _client!.UploadFile(
            request.LocalPath,
            targetPath,
            existsMode,
            createRemoteDir: false,
            progress: ftpProgress,
            token: ct);

        if (request.AtomicUpload)
        {
            await _client.Rename(targetPath, request.RemotePath, ct);
        }

        // Final progress
        progress?.Report(new TransferProgress(
            totalBytes,
            totalBytes,
            Path.GetFileName(request.LocalPath),
            0));
    }

    public async Task DownloadAsync(DownloadRequest request, IProgress<TransferProgress>? progress, CancellationToken ct)
    {
        EnsureConnected();

        var existsMode = request.ResumePartial
            ? FtpLocalExists.Resume
            : FtpLocalExists.Overwrite;

        // Get remote file size for progress reporting
        var remoteSize = await _client!.GetFileSize(request.RemotePath, -1, ct);
        var totalBytes = remoteSize > 0 ? remoteSize : 0L;

        long lastReportedBytes = 0;
        var lastReportTime = Stopwatch.GetTimestamp();
        const long reportIntervalBytes = 1024 * 1024;
        var reportIntervalTicks = Stopwatch.Frequency * 5;

        IProgress<FtpProgress>? ftpProgress = progress is not null && totalBytes > 0
            ? new Progress<FtpProgress>(p =>
            {
                var bytesTransferred = (long)(totalBytes * p.Progress / 100.0);
                var now = Stopwatch.GetTimestamp();
                var bytesDelta = bytesTransferred - lastReportedBytes;
                var timeDelta = now - lastReportTime;

                if (bytesDelta >= reportIntervalBytes || timeDelta >= reportIntervalTicks)
                {
                    progress.Report(new TransferProgress(
                        bytesTransferred,
                        totalBytes,
                        Path.GetFileName(request.RemotePath),
                        p.TransferSpeed));

                    lastReportedBytes = bytesTransferred;
                    lastReportTime = now;
                }
            })
            : null;

        await _client.DownloadFile(
            request.LocalPath,
            request.RemotePath,
            existsMode,
            progress: ftpProgress,
            token: ct);

        if (request.DeleteAfterDownload)
        {
            await _client.DeleteFile(request.RemotePath, ct);
        }

        if (totalBytes > 0)
        {
            progress?.Report(new TransferProgress(
                totalBytes,
                totalBytes,
                Path.GetFileName(request.RemotePath),
                0));
        }
    }

    public async Task<IReadOnlyList<RemoteFileInfo>> ListDirectoryAsync(string remotePath, CancellationToken ct)
    {
        EnsureConnected();

        var items = await _client!.GetListing(remotePath, ct);

        return items
            .Where(f => f.Name != "." && f.Name != "..")
            .Select(f => new RemoteFileInfo(
                f.Name,
                f.FullName,
                f.Size,
                f.Modified,
                f.Type == FtpObjectType.Directory))
            .ToList()
            .AsReadOnly();
    }

    public async Task CreateDirectoryAsync(string remotePath, CancellationToken ct)
    {
        EnsureConnected();
        await _client!.CreateDirectory(remotePath, true, ct);
    }

    public async Task DeleteDirectoryAsync(string remotePath, bool recursive, CancellationToken ct)
    {
        EnsureConnected();
        await _client!.DeleteDirectory(remotePath, ct);
    }

    public async Task RenameAsync(string oldPath, string newPath, CancellationToken ct)
    {
        EnsureConnected();
        await _client!.Rename(oldPath, newPath, ct);
    }

    public async Task DeleteFileAsync(string remotePath, CancellationToken ct)
    {
        EnsureConnected();
        await _client!.DeleteFile(remotePath, ct);
    }

    public async Task<ConnectionTestResult> TestAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!IsConnected)
                await ConnectAsync(ct);

            var items = await ListDirectoryAsync("/", ct);
            sw.Stop();

            return new ConnectionTestResult(
                Success: true,
                Latency: sw.Elapsed,
                ServerBanner: _client?.ServerType.ToString(),
                ErrorMessage: null,
                SupportedAlgorithms: null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionTestResult(
                Success: false,
                Latency: sw.Elapsed,
                ServerBanner: null,
                ErrorMessage: ex.Message,
                SupportedAlgorithms: null);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            if (_client.IsConnected)
            {
                try { await _client.Disconnect(); }
                catch { /* best-effort */ }
            }
            _client.Dispose();
            _client = null;
        }
    }

    private void EnsureConnected()
    {
        if (_client is null || !_client.IsConnected)
            throw new InvalidOperationException("FTP client is not connected. Call ConnectAsync first.");
    }
}

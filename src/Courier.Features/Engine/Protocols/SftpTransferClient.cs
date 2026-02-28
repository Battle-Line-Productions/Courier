using System.Diagnostics;
using System.Text;
using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace Courier.Features.Engine.Protocols;

public class SftpTransferClient : ITransferClient
{
    private readonly Connection _connection;
    private readonly byte[]? _decryptedPassword;
    private readonly byte[]? _sshPrivateKeyData;
    private SftpClient? _client;
    private string? _acceptedFingerprint;

    public SftpTransferClient(Connection connection, byte[]? decryptedPassword, byte[]? sshPrivateKeyData)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _decryptedPassword = decryptedPassword;
        _sshPrivateKeyData = sshPrivateKeyData;
    }

    public string Protocol => "sftp";

    public bool IsConnected => _client?.IsConnected ?? false;

    /// <summary>
    /// If a trust-on-first-use connection accepted a new fingerprint, it is exposed here
    /// so the caller can persist it back to the Connection entity.
    /// </summary>
    public string? AcceptedFingerprint => _acceptedFingerprint;

    public async Task ConnectAsync(CancellationToken ct)
    {
        var authMethods = BuildAuthMethods();
        var connectionInfo = new ConnectionInfo(
            _connection.Host,
            _connection.Port,
            _connection.Username,
            authMethods)
        {
            Timeout = TimeSpan.FromSeconds(_connection.ConnectTimeoutSec)
        };

        _client = new SftpClient(connectionInfo);
        _client.OperationTimeout = TimeSpan.FromSeconds(_connection.OperationTimeoutSec);

        _client.HostKeyReceived += (sender, e) =>
        {
            var fingerprint = e.FingerPrintSHA256;

            switch (_connection.HostKeyPolicy.ToLowerInvariant())
            {
                case "always_trust":
                    e.CanTrust = true;
                    break;

                case "trust_on_first_use":
                    if (string.IsNullOrEmpty(_connection.StoredHostFingerprint))
                    {
                        // First connection — accept and store
                        _acceptedFingerprint = fingerprint;
                        e.CanTrust = true;
                    }
                    else if (string.Equals(_connection.StoredHostFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
                    {
                        e.CanTrust = true;
                    }
                    else
                    {
                        // Fingerprint mismatch — reject
                        e.CanTrust = false;
                    }
                    break;

                case "manual":
                    e.CanTrust = !string.IsNullOrEmpty(_connection.StoredHostFingerprint)
                        && string.Equals(_connection.StoredHostFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase);
                    break;

                default:
                    e.CanTrust = false;
                    break;
            }
        };

        if (_connection.KeepaliveIntervalSec > 0)
        {
            _client.KeepAliveInterval = TimeSpan.FromSeconds(_connection.KeepaliveIntervalSec);
        }

        await Task.Run(() => _client.Connect(), ct);
    }

    public Task DisconnectAsync()
    {
        if (_client?.IsConnected == true)
            _client.Disconnect();

        return Task.CompletedTask;
    }

    public async Task UploadAsync(UploadRequest request, IProgress<TransferProgress>? progress, CancellationToken ct)
    {
        EnsureConnected();

        var targetPath = request.AtomicUpload
            ? request.RemotePath + request.AtomicSuffix
            : request.RemotePath;

        await using var fileStream = File.OpenRead(request.LocalPath);
        var totalBytes = fileStream.Length;

        if (request.ResumePartial && !request.AtomicUpload)
        {
            // Check if remote file exists for resume
            try
            {
                var remoteAttrs = _client!.GetAttributes(targetPath);
                if (remoteAttrs.Size > 0 && remoteAttrs.Size < totalBytes)
                {
                    fileStream.Seek(remoteAttrs.Size, SeekOrigin.Begin);
                }
            }
            catch (Renci.SshNet.Common.SftpPathNotFoundException)
            {
                // File doesn't exist yet — upload from beginning
            }
        }

        long bytesTransferred = totalBytes - fileStream.Length + fileStream.Position;
        var lastReportTime = Stopwatch.GetTimestamp();
        long lastReportedBytes = bytesTransferred;
        const long reportIntervalBytes = 1024 * 1024; // 1 MB
        var reportIntervalTicks = Stopwatch.Frequency * 5; // 5 seconds

        await Task.Run(() =>
        {
            _client!.UploadFile(fileStream, targetPath, canOverride: true, offset =>
            {
                bytesTransferred = (long)offset;
                var now = Stopwatch.GetTimestamp();
                var bytesDelta = bytesTransferred - lastReportedBytes;
                var timeDelta = now - lastReportTime;

                if (bytesDelta >= reportIntervalBytes || timeDelta >= reportIntervalTicks)
                {
                    var elapsedSec = (double)timeDelta / Stopwatch.Frequency;
                    var rate = elapsedSec > 0 ? bytesDelta / elapsedSec : 0;

                    progress?.Report(new TransferProgress(
                        bytesTransferred,
                        totalBytes,
                        Path.GetFileName(request.LocalPath),
                        rate));

                    lastReportTime = now;
                    lastReportedBytes = bytesTransferred;
                }
            });
        }, ct);

        if (request.AtomicUpload)
        {
            await Task.Run(() => _client!.RenameFile(targetPath, request.RemotePath), ct);
        }

        // Final progress report
        progress?.Report(new TransferProgress(
            totalBytes,
            totalBytes,
            Path.GetFileName(request.LocalPath),
            0));
    }

    public async Task DownloadAsync(DownloadRequest request, IProgress<TransferProgress>? progress, CancellationToken ct)
    {
        EnsureConnected();

        long offset = 0;
        if (request.ResumePartial && File.Exists(request.LocalPath))
        {
            var localInfo = new FileInfo(request.LocalPath);
            offset = localInfo.Length;
        }

        var remoteAttrs = _client!.GetAttributes(request.RemotePath);
        var totalBytes = remoteAttrs.Size;

        await using var fileStream = new FileStream(
            request.LocalPath,
            offset > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write);

        long bytesTransferred = offset;
        var lastReportTime = Stopwatch.GetTimestamp();
        long lastReportedBytes = bytesTransferred;
        const long reportIntervalBytes = 1024 * 1024;
        var reportIntervalTicks = Stopwatch.Frequency * 5;

        await Task.Run(() =>
        {
            _client.DownloadFile(request.RemotePath, fileStream, downloadCallback: downloaded =>
            {
                bytesTransferred = offset + (long)downloaded;
                var now = Stopwatch.GetTimestamp();
                var bytesDelta = bytesTransferred - lastReportedBytes;
                var timeDelta = now - lastReportTime;

                if (bytesDelta >= reportIntervalBytes || timeDelta >= reportIntervalTicks)
                {
                    var elapsedSec = (double)timeDelta / Stopwatch.Frequency;
                    var rate = elapsedSec > 0 ? bytesDelta / elapsedSec : 0;

                    progress?.Report(new TransferProgress(
                        bytesTransferred,
                        totalBytes,
                        Path.GetFileName(request.RemotePath),
                        rate));

                    lastReportTime = now;
                    lastReportedBytes = bytesTransferred;
                }
            });
        }, ct);

        if (request.DeleteAfterDownload)
        {
            await Task.Run(() => _client.DeleteFile(request.RemotePath), ct);
        }

        progress?.Report(new TransferProgress(
            totalBytes,
            totalBytes,
            Path.GetFileName(request.RemotePath),
            0));
    }

    public async Task<IReadOnlyList<RemoteFileInfo>> ListDirectoryAsync(string remotePath, CancellationToken ct)
    {
        EnsureConnected();

        var items = await Task.Run(() => _client!.ListDirectory(remotePath), ct);

        return items
            .Where(f => f.Name != "." && f.Name != "..")
            .Select(f => new RemoteFileInfo(
                f.Name,
                f.FullName,
                f.Attributes.Size,
                f.Attributes.LastWriteTime,
                f.Attributes.IsDirectory))
            .ToList()
            .AsReadOnly();
    }

    public async Task CreateDirectoryAsync(string remotePath, CancellationToken ct)
    {
        EnsureConnected();

        // Split the path into segments and create each if not exists
        var segments = remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var currentPath = remotePath.StartsWith('/') ? "/" : "";

        foreach (var segment in segments)
        {
            currentPath = currentPath.Length > 0 && !currentPath.EndsWith('/')
                ? $"{currentPath}/{segment}"
                : $"{currentPath}{segment}";

            try
            {
                var attrs = await Task.Run(() => _client!.GetAttributes(currentPath), ct);
                if (attrs.IsDirectory)
                    continue;
            }
            catch (Renci.SshNet.Common.SftpPathNotFoundException)
            {
                // Directory doesn't exist — create it
            }

            await Task.Run(() => _client!.CreateDirectory(currentPath), ct);
        }
    }

    public async Task DeleteDirectoryAsync(string remotePath, bool recursive, CancellationToken ct)
    {
        EnsureConnected();

        if (recursive)
        {
            await DeleteDirectoryRecursiveAsync(remotePath, ct);
        }
        else
        {
            await Task.Run(() => _client!.DeleteDirectory(remotePath), ct);
        }
    }

    public async Task RenameAsync(string oldPath, string newPath, CancellationToken ct)
    {
        EnsureConnected();
        await Task.Run(() => _client!.RenameFile(oldPath, newPath), ct);
    }

    public async Task DeleteFileAsync(string remotePath, CancellationToken ct)
    {
        EnsureConnected();
        await Task.Run(() => _client!.DeleteFile(remotePath), ct);
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
                ServerBanner: _client?.ConnectionInfo?.ServerVersion,
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
                try { _client.Disconnect(); }
                catch { /* best-effort */ }
            }
            _client.Dispose();
            _client = null;
        }

        await ValueTask.CompletedTask;
    }

    private AuthenticationMethod[] BuildAuthMethods()
    {
        var methods = new List<AuthenticationMethod>();

        switch (_connection.AuthMethod.ToLowerInvariant())
        {
            case "password":
                methods.Add(CreatePasswordAuth());
                break;

            case "ssh_key":
                methods.Add(CreatePrivateKeyAuth());
                break;

            case "password_and_ssh_key":
                methods.Add(CreatePasswordAuth());
                methods.Add(CreatePrivateKeyAuth());
                break;

            default:
                throw new ArgumentException($"Unsupported auth method: {_connection.AuthMethod}");
        }

        return methods.ToArray();
    }

    private PasswordAuthenticationMethod CreatePasswordAuth()
    {
        if (_decryptedPassword is null || _decryptedPassword.Length == 0)
            throw new InvalidOperationException("Password auth requires a decrypted password.");

        var password = Encoding.UTF8.GetString(_decryptedPassword);
        return new PasswordAuthenticationMethod(_connection.Username, password);
    }

    private PrivateKeyAuthenticationMethod CreatePrivateKeyAuth()
    {
        if (_sshPrivateKeyData is null || _sshPrivateKeyData.Length == 0)
            throw new InvalidOperationException("SSH key auth requires private key data.");

        using var keyStream = new MemoryStream(_sshPrivateKeyData);
        var privateKeyFile = new PrivateKeyFile(keyStream);
        return new PrivateKeyAuthenticationMethod(_connection.Username, privateKeyFile);
    }

    private void EnsureConnected()
    {
        if (_client is null || !_client.IsConnected)
            throw new InvalidOperationException("SFTP client is not connected. Call ConnectAsync first.");
    }

    private async Task DeleteDirectoryRecursiveAsync(string path, CancellationToken ct)
    {
        var items = await Task.Run(() => _client!.ListDirectory(path), ct);

        foreach (var item in items)
        {
            if (item.Name == "." || item.Name == "..")
                continue;

            if (item.Attributes.IsDirectory)
            {
                await DeleteDirectoryRecursiveAsync(item.FullName, ct);
            }
            else
            {
                await Task.Run(() => _client!.DeleteFile(item.FullName), ct);
            }
        }

        await Task.Run(() => _client!.DeleteDirectory(path), ct);
    }
}

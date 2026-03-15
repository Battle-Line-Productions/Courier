using Courier.Domain.Encryption;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Domain.Protocols;
using Courier.Features;
using Courier.Features.Engine.Protocols;
using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileSystemGlobbing;
using System.Diagnostics;
using System.Security.Authentication;
using System.Text.Json;

namespace Courier.Worker.Services;

public class MonitorPollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonitorPollingService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(15);

    public MonitorPollingService(IServiceScopeFactory scopeFactory, ILogger<MonitorPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MonitorPollingService started. Polling every {Interval}s.", _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollMonitorsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in monitor polling loop");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("MonitorPollingService stopping.");
    }

    private async Task PollMonitorsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourierDbContext>();

        var now = DateTime.UtcNow;
        var activeMonitors = await db.FileMonitors
            .Include(m => m.Bindings)
            .Where(m => m.State == "active")
            .ToListAsync(ct);

        var monitors = activeMonitors
            .Where(m => m.LastPolledAt == null ||
                        (now - m.LastPolledAt.Value).TotalSeconds >= m.PollingIntervalSec)
            .ToList();

        foreach (var monitor in monitors)
        {
            try
            {
                await ProcessMonitorAsync(db, scope.ServiceProvider, monitor, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing monitor {MonitorId} ({MonitorName})", monitor.Id, monitor.Name);
                await HandleFailureAsync(db, monitor, ct);
            }
        }
    }

    private async Task ProcessMonitorAsync(
        CourierDbContext db,
        IServiceProvider serviceProvider,
        FileMonitor monitor,
        CancellationToken ct)
    {
        using var activity = CourierDiagnostics.FileMonitor.StartActivity("monitor.poll");
        activity?.SetTag("monitor.id", monitor.Id.ToString());
        activity?.SetTag("monitor.name", monitor.Name);
        activity?.SetTag("monitor.type", "local");

        var pollStopwatch = Stopwatch.StartNew();

        var watchTarget = ParseWatchTarget(monitor.WatchTarget);
        if (watchTarget is null)
        {
            _logger.LogWarning("Monitor {MonitorId} has invalid watch target", monitor.Id);
            return;
        }

        // Route to remote monitoring for non-local watch targets
        if (watchTarget.Type != "local")
        {
            await ProcessRemoteMonitorAsync(db, serviceProvider, monitor, watchTarget, pollStopwatch, ct);
            return;
        }

        if (!Directory.Exists(watchTarget.Path))
        {
            _logger.LogWarning("Monitor {MonitorId} watch path does not exist: {Path}", monitor.Id, watchTarget.Path);
            monitor.LastPolledAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        // Enumerate files
        var allFiles = Directory.EnumerateFiles(watchTarget.Path)
            .Select(f => new FileInfo(f))
            .ToList();

        // Apply glob patterns
        var matchedFiles = ApplyPatterns(allFiles, monitor.FilePatterns);

        // Apply stability window — only include files that haven't been modified recently
        if (monitor.StabilityWindowSec > 0)
        {
            var stableThreshold = DateTime.UtcNow.AddSeconds(-monitor.StabilityWindowSec);
            var unstableCount = matchedFiles.Count;
            matchedFiles = matchedFiles
                .Where(f => f.LastWriteTimeUtc <= stableThreshold)
                .ToList();

            var skippedCount = unstableCount - matchedFiles.Count;
            if (skippedCount > 0)
            {
                _logger.LogDebug(
                    "Monitor {MonitorId} skipped {SkippedCount} file(s) not yet stable (window: {WindowSec}s)",
                    monitor.Id, skippedCount, monitor.StabilityWindowSec);
            }
        }

        // Get existing file log entries for this monitor
        var existingLogs = await db.MonitorFileLogs
            .Where(l => l.MonitorId == monitor.Id)
            .Select(l => new { l.FilePath, l.LastModified })
            .ToDictionaryAsync(l => l.FilePath, l => l.LastModified, ct);

        var triggerEvents = (TriggerEvent)monitor.TriggerEvents;
        var detectedFiles = new List<(FileInfo Info, string Event)>();

        foreach (var file in matchedFiles)
        {
            var fullPath = file.FullName;

            if (triggerEvents.HasFlag(TriggerEvent.FileExists))
            {
                detectedFiles.Add((file, "file_exists"));
            }
            else if (!existingLogs.ContainsKey(fullPath) && triggerEvents.HasFlag(TriggerEvent.FileCreated))
            {
                detectedFiles.Add((file, "file_created"));
            }
            else if (existingLogs.TryGetValue(fullPath, out var lastMod) &&
                     file.LastWriteTimeUtc != lastMod &&
                     triggerEvents.HasFlag(TriggerEvent.FileModified))
            {
                detectedFiles.Add((file, "file_modified"));
            }
        }

        pollStopwatch.Stop();

        if (detectedFiles.Count == 0)
        {
            monitor.LastPolledAt = DateTime.UtcNow;
            monitor.LastPollDurationMs = pollStopwatch.ElapsedMilliseconds;
            monitor.LastPollFileCount = matchedFiles.Count;
            monitor.ConsecutiveFailureCount = 0;
            await db.SaveChangesAsync(ct);
            return;
        }

        activity?.SetTag("files.detected", detectedFiles.Count);

        _logger.LogInformation(
            "Monitor {MonitorId} detected {FileCount} file(s) in {Path}",
            monitor.Id, detectedFiles.Count, watchTarget.Path);

        // Trigger jobs
        var executionService = serviceProvider.GetRequiredService<ExecutionService>();

        if (monitor.BatchMode)
        {
            // All files → one execution per bound job
            foreach (var binding in monitor.Bindings)
            {
                var triggerResult = await executionService.TriggerAsync(
                    binding.JobId,
                    $"monitor:{monitor.Id}",
                    ct);

                var executionId = triggerResult.Data?.Id;

                foreach (var (file, eventType) in detectedFiles)
                {
                    db.MonitorFileLogs.Add(new MonitorFileLog
                    {
                        Id = Guid.CreateVersion7(),
                        MonitorId = monitor.Id,
                        FilePath = file.FullName,
                        FileSize = file.Length,
                        LastModified = file.LastWriteTimeUtc,
                        TriggeredAt = DateTime.UtcNow,
                        ExecutionId = executionId,
                    });
                }
            }
        }
        else
        {
            // One execution per file per bound job
            foreach (var (file, eventType) in detectedFiles)
            {
                foreach (var binding in monitor.Bindings)
                {
                    var triggerResult = await executionService.TriggerAsync(
                        binding.JobId,
                        $"monitor:{monitor.Id}",
                        ct);

                    db.MonitorFileLogs.Add(new MonitorFileLog
                    {
                        Id = Guid.CreateVersion7(),
                        MonitorId = monitor.Id,
                        FilePath = file.FullName,
                        FileSize = file.Length,
                        LastModified = file.LastWriteTimeUtc,
                        TriggeredAt = DateTime.UtcNow,
                        ExecutionId = triggerResult.Data?.Id,
                    });
                }
            }
        }

        monitor.LastPolledAt = DateTime.UtcNow;
        monitor.LastPollDurationMs = pollStopwatch.ElapsedMilliseconds;
        monitor.LastPollFileCount = detectedFiles.Count;
        monitor.ConsecutiveFailureCount = 0;
        await db.SaveChangesAsync(ct);
    }

    private async Task ProcessRemoteMonitorAsync(
        CourierDbContext db,
        IServiceProvider serviceProvider,
        FileMonitor monitor,
        WatchTargetConfig watchTarget,
        Stopwatch pollStopwatch,
        CancellationToken ct)
    {
        using var activity = CourierDiagnostics.FileMonitor.StartActivity("monitor.poll.remote");
        activity?.SetTag("monitor.id", monitor.Id.ToString());
        activity?.SetTag("monitor.name", monitor.Name);
        activity?.SetTag("monitor.type", "remote");

        if (string.IsNullOrEmpty(watchTarget.ConnectionId))
        {
            _logger.LogWarning("Monitor {MonitorId} has remote watch target but no ConnectionId", monitor.Id);
            return;
        }

        if (!Guid.TryParse(watchTarget.ConnectionId, out var connectionId))
        {
            _logger.LogWarning("Monitor {MonitorId} has invalid ConnectionId: {ConnectionId}", monitor.Id, watchTarget.ConnectionId);
            return;
        }

        var connection = await db.Connections.FirstOrDefaultAsync(c => c.Id == connectionId, ct);
        if (connection is null)
        {
            _logger.LogWarning("Monitor {MonitorId} references non-existent connection {ConnectionId}", monitor.Id, connectionId);
            monitor.State = "error";
            monitor.LastPolledAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        // Decrypt credentials
        var encryptor = serviceProvider.GetRequiredService<ICredentialEncryptor>();
        var clientFactory = serviceProvider.GetRequiredService<ITransferClientFactory>();

        byte[]? decryptedPassword = null;
        if (connection.PasswordEncrypted is not null)
        {
            var passwordStr = encryptor.Decrypt(connection.PasswordEncrypted);
            decryptedPassword = System.Text.Encoding.UTF8.GetBytes(passwordStr);
        }

        byte[]? sshPrivateKey = null;
        if (connection.SshKeyId is not null)
        {
            var sshKey = await db.SshKeys.FirstOrDefaultAsync(k => k.Id == connection.SshKeyId && !k.IsDeleted, ct);
            if (sshKey?.PrivateKeyData is not null)
            {
                sshPrivateKey = sshKey.PrivateKeyData;
            }
        }

        IReadOnlyList<RemoteFileInfo> remoteFiles;
        await using var client = clientFactory.Create(connection, decryptedPassword, sshPrivateKey);
        try
        {
            await client.ConnectAsync(ct);
            remoteFiles = await client.ListDirectoryAsync(watchTarget.Path, ct);
        }
        catch (AuthenticationException ex)
        {
            _logger.LogError(ex, "Monitor {MonitorId} authentication failure connecting to {Host}:{Port}",
                monitor.Id, connection.Host, connection.Port);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            monitor.State = "error";
            monitor.LastPolledAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }
        catch (Exception ex) when (ex is Renci.SshNet.Common.SshAuthenticationException
                                     or Renci.SshNet.Common.SshConnectionException)
        {
            _logger.LogError(ex, "Monitor {MonitorId} SSH auth/connection failure connecting to {Host}:{Port}",
                monitor.Id, connection.Host, connection.Port);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            monitor.State = "error";
            monitor.LastPolledAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Monitor {MonitorId} failed to list remote directory {Path} on {Host}:{Port}",
                monitor.Id, watchTarget.Path, connection.Host, connection.Port);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            await HandleFailureAsync(db, monitor, ct);
            return;
        }
        finally
        {
            try { await client.DisconnectAsync(); }
            catch { /* best-effort disconnect */ }
        }

        // Filter to files only (exclude directories)
        var files = remoteFiles.Where(f => !f.IsDirectory).ToList();

        // Apply glob patterns
        var matchedFiles = ApplyRemotePatterns(files, monitor.FilePatterns);

        // Apply stability window — only include files that haven't been modified recently
        if (monitor.StabilityWindowSec > 0)
        {
            var stableThreshold = DateTime.UtcNow.AddSeconds(-monitor.StabilityWindowSec);
            var unstableCount = matchedFiles.Count;
            matchedFiles = matchedFiles
                .Where(f => f.LastModified <= stableThreshold)
                .ToList();

            var skippedCount = unstableCount - matchedFiles.Count;
            if (skippedCount > 0)
            {
                _logger.LogDebug(
                    "Monitor {MonitorId} skipped {SkippedCount} remote file(s) not yet stable (window: {WindowSec}s)",
                    monitor.Id, skippedCount, monitor.StabilityWindowSec);
            }
        }

        // Get existing file log entries for this monitor
        var existingLogs = await db.MonitorFileLogs
            .Where(l => l.MonitorId == monitor.Id)
            .Select(l => new { l.FilePath, l.LastModified })
            .ToDictionaryAsync(l => l.FilePath, l => l.LastModified, ct);

        var triggerEvents = (TriggerEvent)monitor.TriggerEvents;
        var detectedFiles = new List<(RemoteFileInfo Info, string Event)>();

        foreach (var file in matchedFiles)
        {
            // Build a consistent file path for log comparison: {remotePath}/{fileName}
            var remoteFilePath = watchTarget.Path.TrimEnd('/') + "/" + file.Name;

            if (triggerEvents.HasFlag(TriggerEvent.FileExists))
            {
                detectedFiles.Add((file, "file_exists"));
            }
            else if (!existingLogs.ContainsKey(remoteFilePath) && triggerEvents.HasFlag(TriggerEvent.FileCreated))
            {
                detectedFiles.Add((file, "file_created"));
            }
            else if (existingLogs.TryGetValue(remoteFilePath, out var lastMod) &&
                     file.LastModified != lastMod &&
                     triggerEvents.HasFlag(TriggerEvent.FileModified))
            {
                detectedFiles.Add((file, "file_modified"));
            }
        }

        pollStopwatch.Stop();

        if (detectedFiles.Count == 0)
        {
            monitor.LastPolledAt = DateTime.UtcNow;
            monitor.LastPollDurationMs = pollStopwatch.ElapsedMilliseconds;
            monitor.LastPollFileCount = matchedFiles.Count;
            monitor.ConsecutiveFailureCount = 0;
            await db.SaveChangesAsync(ct);
            return;
        }

        activity?.SetTag("files.detected", detectedFiles.Count);

        _logger.LogInformation(
            "Monitor {MonitorId} detected {FileCount} remote file(s) in {Path} on {Host}",
            monitor.Id, detectedFiles.Count, watchTarget.Path, connection.Host);

        // Trigger jobs
        var executionService = serviceProvider.GetRequiredService<ExecutionService>();

        if (monitor.BatchMode)
        {
            // All files → one execution per bound job
            foreach (var binding in monitor.Bindings)
            {
                var triggerResult = await executionService.TriggerAsync(
                    binding.JobId,
                    $"monitor:{monitor.Id}",
                    ct);

                var executionId = triggerResult.Data?.Id;

                foreach (var (file, eventType) in detectedFiles)
                {
                    var remoteFilePath = watchTarget.Path.TrimEnd('/') + "/" + file.Name;

                    db.MonitorFileLogs.Add(new MonitorFileLog
                    {
                        Id = Guid.CreateVersion7(),
                        MonitorId = monitor.Id,
                        FilePath = remoteFilePath,
                        FileSize = file.Size,
                        LastModified = file.LastModified,
                        TriggeredAt = DateTime.UtcNow,
                        ExecutionId = executionId,
                    });
                }
            }
        }
        else
        {
            // One execution per file per bound job
            foreach (var (file, eventType) in detectedFiles)
            {
                var remoteFilePath = watchTarget.Path.TrimEnd('/') + "/" + file.Name;

                foreach (var binding in monitor.Bindings)
                {
                    var triggerResult = await executionService.TriggerAsync(
                        binding.JobId,
                        $"monitor:{monitor.Id}",
                        ct);

                    db.MonitorFileLogs.Add(new MonitorFileLog
                    {
                        Id = Guid.CreateVersion7(),
                        MonitorId = monitor.Id,
                        FilePath = remoteFilePath,
                        FileSize = file.Size,
                        LastModified = file.LastModified,
                        TriggeredAt = DateTime.UtcNow,
                        ExecutionId = triggerResult.Data?.Id,
                    });
                }
            }
        }

        monitor.LastPolledAt = DateTime.UtcNow;
        monitor.LastPollDurationMs = pollStopwatch.ElapsedMilliseconds;
        monitor.LastPollFileCount = detectedFiles.Count;
        monitor.ConsecutiveFailureCount = 0;
        await db.SaveChangesAsync(ct);
    }

    private async Task HandleFailureAsync(CourierDbContext db, FileMonitor monitor, CancellationToken ct)
    {
        monitor.ConsecutiveFailureCount++;
        monitor.LastPolledAt = DateTime.UtcNow;

        if (monitor.ConsecutiveFailureCount >= monitor.MaxConsecutiveFailures)
        {
            monitor.State = "error";
            _logger.LogWarning(
                "Monitor {MonitorId} transitioned to Error after {Count} consecutive failures",
                monitor.Id, monitor.ConsecutiveFailureCount);
        }

        await db.SaveChangesAsync(ct);
    }

    private List<FileInfo> ApplyPatterns(List<FileInfo> files, string? patternsJson)
    {
        if (string.IsNullOrWhiteSpace(patternsJson))
            return files;

        try
        {
            var patterns = JsonSerializer.Deserialize<string[]>(patternsJson);
            if (patterns is null || patterns.Length == 0)
                return files;

            var matcher = new Matcher();
            foreach (var pattern in patterns)
            {
                matcher.AddInclude(pattern);
            }

            return files
                .Where(f => matcher.Match(f.Name).HasMatches)
                .ToList();
        }
        catch
        {
            return files;
        }
    }

    private List<RemoteFileInfo> ApplyRemotePatterns(List<RemoteFileInfo> files, string? patternsJson)
    {
        if (string.IsNullOrWhiteSpace(patternsJson))
            return files;

        try
        {
            var patterns = JsonSerializer.Deserialize<string[]>(patternsJson);
            if (patterns is null || patterns.Length == 0)
                return files;

            var matcher = new Matcher();
            foreach (var pattern in patterns)
            {
                matcher.AddInclude(pattern);
            }

            return files
                .Where(f => matcher.Match(f.Name).HasMatches)
                .ToList();
        }
        catch
        {
            return files;
        }
    }

    private static WatchTargetConfig? ParseWatchTarget(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<WatchTargetConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch
        {
            return null;
        }
    }

    private sealed class WatchTargetConfig
    {
        public string Type { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? ConnectionId { get; set; }
    }
}

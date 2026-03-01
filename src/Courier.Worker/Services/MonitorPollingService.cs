using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileSystemGlobbing;
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
        var watchTarget = ParseWatchTarget(monitor.WatchTarget);
        if (watchTarget is null)
        {
            _logger.LogWarning("Monitor {MonitorId} has invalid watch target", monitor.Id);
            return;
        }

        // Only local directory monitoring for now
        if (watchTarget.Type != "local")
        {
            _logger.LogDebug("Monitor {MonitorId} uses remote watch target — skipping (not yet implemented)", monitor.Id);
            monitor.LastPolledAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
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

        if (detectedFiles.Count == 0)
        {
            monitor.LastPolledAt = DateTime.UtcNow;
            monitor.ConsecutiveFailureCount = 0;
            await db.SaveChangesAsync(ct);
            return;
        }

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

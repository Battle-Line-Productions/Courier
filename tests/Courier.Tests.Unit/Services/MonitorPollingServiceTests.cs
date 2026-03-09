using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Courier.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Services;

public class MonitorPollingServiceTests : IDisposable
{
    private readonly string _tempDir;

    public MonitorPollingServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"courier-monitor-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    private static (IServiceScopeFactory scopeFactory, CourierDbContext db) CreateScopeFactory()
    {
        var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var executionService = new ExecutionService(db, audit);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(CourierDbContext)).Returns(db);
        serviceProvider.GetService(typeof(ExecutionService)).Returns(executionService);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        return (scopeFactory, db);
    }

    private async Task<(Guid jobId, Guid monitorId)> SetupLocalMonitor(
        CourierDbContext db,
        string watchPath,
        int triggerEvents = (int)TriggerEvent.FileCreated,
        string? filePatterns = null,
        int stabilityWindowSec = 0,
        bool batchMode = false)
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = "Test Job",
            CreatedAt = DateTime.UtcNow,
        };
        db.Jobs.Add(job);

        // ExecutionService.TriggerAsync requires at least one step
        db.JobSteps.Add(new JobStep
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            TypeKey = "file.copy",
            Configuration = "{}",
            StepOrder = 1,
        });

        var monitorId = Guid.NewGuid();
        var escapedPath = watchPath.Replace("\\", "\\\\");
        var monitor = new FileMonitor
        {
            Id = monitorId,
            Name = "Test Monitor",
            State = "active",
            WatchTarget = $$"""{"type":"local","path":"{{escapedPath}}"}""",
            TriggerEvents = triggerEvents,
            FilePatterns = filePatterns,
            PollingIntervalSec = 1,
            StabilityWindowSec = stabilityWindowSec,
            BatchMode = batchMode,
            MaxConsecutiveFailures = 5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Bindings =
            [
                new MonitorJobBinding
                {
                    Id = Guid.NewGuid(),
                    MonitorId = monitorId,
                    JobId = job.Id,
                }
            ],
        };
        db.FileMonitors.Add(monitor);

        await db.SaveChangesAsync();
        return (job.Id, monitorId);
    }

    private static async Task RunOnePoll(MonitorPollingService service)
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(1000, cts.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Poll_ActiveLocalMonitor_DetectsNewFile_TriggersJob()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var (jobId, monitorId) = await SetupLocalMonitor(db, _tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "report.csv"), "data");

        var service = new MonitorPollingService(scopeFactory, NullLogger<MonitorPollingService>.Instance);

        // Act
        await RunOnePoll(service);

        // Assert — should have created a job execution
        var execution = await db.JobExecutions.FirstOrDefaultAsync(e => e.JobId == jobId);
        execution.ShouldNotBeNull();
        execution!.State.ShouldBe(JobExecutionState.Queued);
        execution.TriggeredBy.ShouldStartWith("monitor:");

        // File log should be recorded
        var log = await db.MonitorFileLogs.FirstOrDefaultAsync(l => l.MonitorId == monitorId);
        log.ShouldNotBeNull();
    }

    [Fact]
    public async Task Poll_PatternFilter_OnlyMatchesGlob()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var (_, monitorId) = await SetupLocalMonitor(db, _tempDir,
            filePatterns: """["*.csv"]""");

        await File.WriteAllTextAsync(Path.Combine(_tempDir, "report.csv"), "data");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "image.png"), "not data");

        var service = new MonitorPollingService(scopeFactory, NullLogger<MonitorPollingService>.Instance);

        // Act
        await RunOnePoll(service);

        // Assert — only one file log (csv), not two
        var logs = await db.MonitorFileLogs.Where(l => l.MonitorId == monitorId).ToListAsync();
        logs.Count.ShouldBe(1);
        logs[0].FilePath.ShouldContain("report.csv");
    }

    [Fact]
    public async Task Poll_StabilityWindow_SkipsRecentFiles()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var (jobId, monitorId) = await SetupLocalMonitor(db, _tempDir,
            triggerEvents: (int)TriggerEvent.FileExists,
            stabilityWindowSec: 3600); // 1 hour window

        // File just written — its LastWriteTimeUtc is now, within the stability window
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "recent.csv"), "data");

        var service = new MonitorPollingService(scopeFactory, NullLogger<MonitorPollingService>.Instance);

        // Act
        await RunOnePoll(service);

        // Assert — no job execution (file too recent)
        var execution = await db.JobExecutions.FirstOrDefaultAsync(e => e.JobId == jobId);
        execution.ShouldBeNull();

        // Monitor should still have been polled
        var monitor = await db.FileMonitors.FindAsync(monitorId);
        monitor!.LastPolledAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Poll_NoMatchingFiles_UpdatesLastPolledAt()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var (_, monitorId) = await SetupLocalMonitor(db, _tempDir);
        // Empty directory — no files to detect

        var service = new MonitorPollingService(scopeFactory, NullLogger<MonitorPollingService>.Instance);

        // Act
        await RunOnePoll(service);

        // Assert
        var monitor = await db.FileMonitors.FindAsync(monitorId);
        monitor!.LastPolledAt.ShouldNotBeNull();
        monitor.ConsecutiveFailureCount.ShouldBe(0);
    }

    [Fact]
    public async Task Poll_FileCreated_AlreadyInLog_NotDetectedAgain()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var filePath = Path.Combine(_tempDir, "already-seen.csv");
        await File.WriteAllTextAsync(filePath, "data");

        var (jobId, monitorId) = await SetupLocalMonitor(db, _tempDir);

        // Pre-populate the file log (as if previous poll already detected it)
        db.MonitorFileLogs.Add(new MonitorFileLog
        {
            Id = Guid.NewGuid(),
            MonitorId = monitorId,
            FilePath = filePath,
            FileSize = new FileInfo(filePath).Length,
            LastModified = new FileInfo(filePath).LastWriteTimeUtc,
            TriggeredAt = DateTime.UtcNow.AddMinutes(-5),
        });
        await db.SaveChangesAsync();

        var service = new MonitorPollingService(scopeFactory, NullLogger<MonitorPollingService>.Instance);

        // Act
        await RunOnePoll(service);

        // Assert — no new execution (file already in log)
        var executions = await db.JobExecutions.Where(e => e.JobId == jobId).ToListAsync();
        executions.Count.ShouldBe(0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

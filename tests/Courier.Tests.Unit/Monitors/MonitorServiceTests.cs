using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Features.AuditLog;
using Courier.Features.Monitors;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Monitors;

public class MonitorServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    private static async Task<Job> SeedJobAsync(CourierDbContext db, string name = "Test Job")
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    private static CreateMonitorRequest MakeCreateRequest(List<Guid> jobIds) => new()
    {
        Name = "Test Monitor",
        WatchTarget = """{"type":"local","path":"/data/incoming"}""",
        TriggerEvents = 1,
        PollingIntervalSec = 60,
        MaxConsecutiveFailures = 5,
        JobIds = jobIds,
    };

    [Fact]
    public async Task Create_ValidRequest_ReturnsSuccessWithMonitor()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var request = MakeCreateRequest([job.Id]);

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Name.ShouldBe("Test Monitor");
        result.Data.State.ShouldBe("active");
        result.Data.TriggerEvents.ShouldBe(1);
        result.Data.PollingIntervalSec.ShouldBe(60);
        result.Data.Bindings.Count.ShouldBe(1);
        result.Data.Bindings[0].JobId.ShouldBe(job.Id);
        result.Data.Bindings[0].JobName.ShouldBe("Test Job");
    }

    [Fact]
    public async Task Create_InvalidJobId_ReturnsNotFoundError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new MonitorService(db, new AuditService(db));
        var request = MakeCreateRequest([Guid.NewGuid()]);

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }

    [Fact]
    public async Task Create_MultipleJobs_CreatesAllBindings()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job1 = await SeedJobAsync(db, "Job 1");
        var job2 = await SeedJobAsync(db, "Job 2");
        var service = new MonitorService(db, new AuditService(db));
        var request = MakeCreateRequest([job1.Id, job2.Id]);

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Bindings.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetById_ExistingMonitor_ReturnsMonitor()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));

        // Act
        var result = await service.GetByIdAsync(created.Data!.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Id.ShouldBe(created.Data.Id);
        result.Data.Name.ShouldBe("Test Monitor");
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFoundError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new MonitorService(db, new AuditService(db));

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }

    [Fact]
    public async Task List_ReturnsPagedResults()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));

        await service.CreateAsync(MakeCreateRequest([job.Id]));
        await service.CreateAsync(new CreateMonitorRequest
        {
            Name = "Another Monitor",
            WatchTarget = """{"type":"local","path":"/data/other"}""",
            TriggerEvents = 2,
            PollingIntervalSec = 120,
            MaxConsecutiveFailures = 3,
            JobIds = [job.Id],
        });

        // Act
        var result = await service.ListAsync(1, 10);

        // Assert
        result.Data.Count.ShouldBe(2);
        result.Pagination.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task List_FilterByState_ReturnsMatching()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));

        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));
        await service.PauseAsync(created.Data!.Id);

        await service.CreateAsync(new CreateMonitorRequest
        {
            Name = "Active Monitor",
            WatchTarget = """{"type":"local","path":"/data/other"}""",
            TriggerEvents = 1,
            PollingIntervalSec = 60,
            MaxConsecutiveFailures = 5,
            JobIds = [job.Id],
        });

        // Act
        var result = await service.ListAsync(1, 10, state: "active");

        // Assert
        result.Data.Count.ShouldBe(1);
        result.Data[0].State.ShouldBe("active");
    }

    [Fact]
    public async Task List_SearchByName_ReturnsMatching()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));

        await service.CreateAsync(MakeCreateRequest([job.Id]));
        await service.CreateAsync(new CreateMonitorRequest
        {
            Name = "Special Watch",
            WatchTarget = """{"type":"local","path":"/data/other"}""",
            TriggerEvents = 1,
            PollingIntervalSec = 60,
            MaxConsecutiveFailures = 5,
            JobIds = [job.Id],
        });

        // Act
        var result = await service.ListAsync(1, 10, search: "special");

        // Assert
        result.Data.Count.ShouldBe(1);
        result.Data[0].Name.ShouldBe("Special Watch");
    }

    [Fact]
    public async Task Update_ValidRequest_UpdatesMonitor()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));

        // Act
        var result = await service.UpdateAsync(created.Data!.Id, new UpdateMonitorRequest
        {
            Name = "Updated Name",
            PollingIntervalSec = 120,
        });

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Name.ShouldBe("Updated Name");
        result.Data.PollingIntervalSec.ShouldBe(120);
    }

    [Fact]
    public async Task Update_WithNewJobIds_ReplacesBindings()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job1 = await SeedJobAsync(db, "Job 1");
        var job2 = await SeedJobAsync(db, "Job 2");
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job1.Id]));

        // Act
        var result = await service.UpdateAsync(created.Data!.Id, new UpdateMonitorRequest
        {
            JobIds = [job2.Id],
        });

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Bindings.Count.ShouldBe(1);
        result.Data.Bindings[0].JobId.ShouldBe(job2.Id);
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNotFoundError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new MonitorService(db, new AuditService(db));

        // Act
        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateMonitorRequest { Name = "x" });

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }

    [Fact]
    public async Task Delete_ExistingMonitor_SoftDeletes()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));

        // Act
        var result = await service.DeleteAsync(created.Data!.Id);

        // Assert
        result.Success.ShouldBeTrue();

        var deleted = await db.FileMonitors.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == created.Data.Id);
        deleted.ShouldNotBeNull();
        deleted!.IsDeleted.ShouldBeTrue();
        deleted.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Delete_NonExistent_ReturnsNotFoundError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new MonitorService(db, new AuditService(db));

        // Act
        var result = await service.DeleteAsync(Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }

    [Fact]
    public async Task Activate_FromPaused_ReturnsActiveMonitor()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));
        await service.PauseAsync(created.Data!.Id);

        // Act
        var result = await service.ActivateAsync(created.Data.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.State.ShouldBe("active");
    }

    [Fact]
    public async Task Activate_FromDisabled_ReturnsActiveMonitor()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));
        await service.DisableAsync(created.Data!.Id);

        // Act
        var result = await service.ActivateAsync(created.Data.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.State.ShouldBe("active");
    }

    [Fact]
    public async Task Activate_FromError_ResetsFailureCountAndActivates()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));

        // Manually set to error state with failure count
        var monitor = await db.FileMonitors.FindAsync(created.Data!.Id);
        monitor!.State = "error";
        monitor.ConsecutiveFailureCount = 5;
        await db.SaveChangesAsync();

        // Act
        var result = await service.ActivateAsync(created.Data.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.State.ShouldBe("active");
        result.Data.ConsecutiveFailureCount.ShouldBe(0);
    }

    [Fact]
    public async Task Activate_AlreadyActive_ReturnsAlreadyActiveError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));

        // Act
        var result = await service.ActivateAsync(created.Data!.Id);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.MonitorAlreadyActive);
    }

    [Fact]
    public async Task Pause_FromActive_ReturnsPausedMonitor()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));

        // Act
        var result = await service.PauseAsync(created.Data!.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.State.ShouldBe("paused");
    }

    [Fact]
    public async Task Pause_FromDisabled_ReturnsStateConflictError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));
        await service.DisableAsync(created.Data!.Id);

        // Act
        var result = await service.PauseAsync(created.Data.Id);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.StateConflict);
    }

    [Fact]
    public async Task Disable_FromActive_ReturnsDisabledMonitor()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));

        // Act
        var result = await service.DisableAsync(created.Data!.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.State.ShouldBe("disabled");
    }

    [Fact]
    public async Task Disable_FromPaused_ReturnsDisabledMonitor()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));
        await service.PauseAsync(created.Data!.Id);

        // Act
        var result = await service.DisableAsync(created.Data.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.State.ShouldBe("disabled");
    }

    [Fact]
    public async Task Disable_FromError_ReturnsStateConflictError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));

        var monitor = await db.FileMonitors.FindAsync(created.Data!.Id);
        monitor!.State = "error";
        await db.SaveChangesAsync();

        // Act
        var result = await service.DisableAsync(created.Data.Id);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.StateConflict);
    }

    [Fact]
    public async Task AcknowledgeError_FromError_ResetsAndActivates()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));

        var monitor = await db.FileMonitors.FindAsync(created.Data!.Id);
        monitor!.State = "error";
        monitor.ConsecutiveFailureCount = 5;
        await db.SaveChangesAsync();

        // Act
        var result = await service.AcknowledgeErrorAsync(created.Data.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.State.ShouldBe("active");
        result.Data.ConsecutiveFailureCount.ShouldBe(0);
    }

    [Fact]
    public async Task AcknowledgeError_NotInError_ReturnsNotInErrorState()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));

        // Act
        var result = await service.AcknowledgeErrorAsync(created.Data!.Id);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.MonitorNotInError);
    }

    [Fact]
    public async Task ListFileLog_NonExistentMonitor_ReturnsNotFoundError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new MonitorService(db, new AuditService(db));

        // Act
        var result = await service.ListFileLogAsync(Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }

    [Fact]
    public async Task ListFileLog_ExistingMonitor_ReturnsPagedResults()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = await SeedJobAsync(db);
        var service = new MonitorService(db, new AuditService(db));
        var created = await service.CreateAsync(MakeCreateRequest([job.Id]));

        // Seed some file logs
        db.MonitorFileLogs.Add(new MonitorFileLog
        {
            Id = Guid.NewGuid(),
            MonitorId = created.Data!.Id,
            FilePath = "/data/incoming/file1.csv",
            FileSize = 1024,
            LastModified = DateTime.UtcNow.AddMinutes(-5),
            TriggeredAt = DateTime.UtcNow,
        });
        db.MonitorFileLogs.Add(new MonitorFileLog
        {
            Id = Guid.NewGuid(),
            MonitorId = created.Data.Id,
            FilePath = "/data/incoming/file2.csv",
            FileSize = 2048,
            LastModified = DateTime.UtcNow.AddMinutes(-3),
            TriggeredAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.ListFileLogAsync(created.Data.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.Count.ShouldBe(2);
        result.Pagination.TotalCount.ShouldBe(2);
    }
}

using System.Text.Json;
using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Features.AuditLog;
using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Jobs;

public class JobVersionTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    private static async Task<JobDto> CreateJobWithSteps(JobService service, CourierDbContext db, string name = "Test Job")
    {
        var created = await service.CreateAsync(new CreateJobRequest { Name = name, Description = "desc" });
        var jobId = created.Data!.Id;

        db.JobSteps.Add(new JobStep
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            StepOrder = 1,
            Name = "Copy File",
            TypeKey = "file.copy",
            Configuration = """{"source_path": "/tmp/a.txt", "destination_path": "/tmp/b.txt"}""",
            TimeoutSeconds = 60
        });
        await db.SaveChangesAsync();

        return created.Data;
    }

    [Fact]
    public async Task UpdateAsync_CreatesVersionSnapshot()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));
        var job = await CreateJobWithSteps(service, db);

        // Act
        await service.UpdateAsync(job.Id, "Updated Name", "Updated desc");

        // Assert
        var versions = await db.JobVersions.Where(v => v.JobId == job.Id).ToListAsync();
        versions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateAsync_VersionSnapshotContainsCorrectConfig()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));
        var job = await CreateJobWithSteps(service, db);

        // Act
        await service.UpdateAsync(job.Id, "Updated Name", "Updated desc");

        // Assert
        var version = await db.JobVersions.FirstAsync(v => v.JobId == job.Id);
        var snapshot = JsonDocument.Parse(version.ConfigSnapshot).RootElement;

        snapshot.GetProperty("name").GetString().ShouldBe("Updated Name");
        snapshot.GetProperty("description").GetString().ShouldBe("Updated desc");
        snapshot.GetProperty("steps").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task UpdateAsync_VersionSnapshotIncludesStepDetails()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));
        var job = await CreateJobWithSteps(service, db);

        // Act
        await service.UpdateAsync(job.Id, "Updated", null);

        // Assert
        var version = await db.JobVersions.FirstAsync(v => v.JobId == job.Id);
        var snapshot = JsonDocument.Parse(version.ConfigSnapshot).RootElement;
        var step = snapshot.GetProperty("steps")[0];

        step.GetProperty("name").GetString().ShouldBe("Copy File");
        step.GetProperty("typeKey").GetString().ShouldBe("file.copy");
        step.GetProperty("stepOrder").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task UpdateAsync_FirstUpdateCreatesVersion2()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));
        var job = await CreateJobWithSteps(service, db);

        // Act
        await service.UpdateAsync(job.Id, "Updated", null);

        // Assert
        var version = await db.JobVersions.FirstAsync(v => v.JobId == job.Id);
        version.VersionNumber.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateAsync_MultipleUpdatesCreateSequentialVersions()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));
        var job = await CreateJobWithSteps(service, db);

        // Act
        await service.UpdateAsync(job.Id, "V2", "second");
        await service.UpdateAsync(job.Id, "V3", "third");
        await service.UpdateAsync(job.Id, "V4", "fourth");

        // Assert
        var versions = await db.JobVersions
            .Where(v => v.JobId == job.Id)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync();

        versions.Count.ShouldBe(3);
        versions[0].VersionNumber.ShouldBe(2);
        versions[1].VersionNumber.ShouldBe(3);
        versions[2].VersionNumber.ShouldBe(4);
    }

    [Fact]
    public async Task UpdateAsync_IncrementsCurrentVersionOnJob()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));
        var job = await CreateJobWithSteps(service, db);

        // Act
        var result = await service.UpdateAsync(job.Id, "Updated", null);

        // Assert
        result.Data!.CurrentVersion.ShouldBe(2);
    }

    [Fact]
    public async Task GetVersionsAsync_ReturnsAllVersionsForJob()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));
        var job = await CreateJobWithSteps(service, db);

        await service.UpdateAsync(job.Id, "V2", null);
        await service.UpdateAsync(job.Id, "V3", null);

        // Act
        var result = await service.GetVersionsAsync(job.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Count.ShouldBe(2);
        result.Data[0].VersionNumber.ShouldBe(3); // Descending order
        result.Data[1].VersionNumber.ShouldBe(2);
    }

    [Fact]
    public async Task GetVersionsAsync_NonExistentJob_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));

        // Act
        var result = await service.GetVersionsAsync(Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }

    [Fact]
    public async Task GetVersionAsync_ReturnsCorrectSnapshot()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));
        var job = await CreateJobWithSteps(service, db);

        await service.UpdateAsync(job.Id, "V2 Name", "V2 desc");

        // Act
        var result = await service.GetVersionAsync(job.Id, 2);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.VersionNumber.ShouldBe(2);
        result.Data.JobId.ShouldBe(job.Id);
        result.Data.ConfigSnapshot.ShouldContain("V2 Name");
    }

    [Fact]
    public async Task GetVersionAsync_NonExistentVersion_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));
        var job = await CreateJobWithSteps(service, db);

        // Act
        var result = await service.GetVersionAsync(job.Id, 999);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.JobVersionNotFound);
    }

    [Fact]
    public async Task GetVersionAsync_NonExistentJob_ReturnsNotFoundError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));

        // Act
        var result = await service.GetVersionAsync(Guid.NewGuid(), 1);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }
}

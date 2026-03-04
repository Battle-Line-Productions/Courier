using Courier.Domain.Common;
using Courier.Features.AuditLog;
using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Jobs;

public class JobDependencyTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    [Fact]
    public async Task AddDependency_ValidPair_Succeeds()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var jobService = new JobService(db, audit);
        var depService = new JobDependencyService(db);

        var upstream = await jobService.CreateAsync(new CreateJobRequest { Name = "Upstream" });
        var downstream = await jobService.CreateAsync(new CreateJobRequest { Name = "Downstream" });

        // Act
        var result = await depService.AddDependencyAsync(downstream.Data!.Id, upstream.Data!.Id, false);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.UpstreamJobId.ShouldBe(upstream.Data.Id);
        result.Data.DownstreamJobId.ShouldBe(downstream.Data.Id);
        result.Data.UpstreamJobName.ShouldBe("Upstream");
    }

    [Fact]
    public async Task AddDependency_SelfDependency_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var jobService = new JobService(db, audit);
        var depService = new JobDependencyService(db);

        var job = await jobService.CreateAsync(new CreateJobRequest { Name = "Self" });

        // Act
        var result = await depService.AddDependencyAsync(job.Data!.Id, job.Data.Id, false);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.SelfDependency);
    }

    [Fact]
    public async Task AddDependency_Duplicate_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var jobService = new JobService(db, audit);
        var depService = new JobDependencyService(db);

        var upstream = await jobService.CreateAsync(new CreateJobRequest { Name = "Up" });
        var downstream = await jobService.CreateAsync(new CreateJobRequest { Name = "Down" });

        await depService.AddDependencyAsync(downstream.Data!.Id, upstream.Data!.Id, false);

        // Act
        var result = await depService.AddDependencyAsync(downstream.Data.Id, upstream.Data.Id, false);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.DuplicateDependency);
    }

    [Fact]
    public async Task AddDependency_Circular_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var jobService = new JobService(db, audit);
        var depService = new JobDependencyService(db);

        var jobA = await jobService.CreateAsync(new CreateJobRequest { Name = "A" });
        var jobB = await jobService.CreateAsync(new CreateJobRequest { Name = "B" });
        var jobC = await jobService.CreateAsync(new CreateJobRequest { Name = "C" });

        // A → B → C, then try C → A (circular)
        await depService.AddDependencyAsync(jobB.Data!.Id, jobA.Data!.Id, false);
        await depService.AddDependencyAsync(jobC.Data!.Id, jobB.Data.Id, false);

        // Act
        var result = await depService.AddDependencyAsync(jobA.Data.Id, jobC.Data.Id, false);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.CircularDependency);
    }

    [Fact]
    public async Task RemoveDependency_Existing_Succeeds()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var jobService = new JobService(db, audit);
        var depService = new JobDependencyService(db);

        var upstream = await jobService.CreateAsync(new CreateJobRequest { Name = "Up" });
        var downstream = await jobService.CreateAsync(new CreateJobRequest { Name = "Down" });

        var added = await depService.AddDependencyAsync(downstream.Data!.Id, upstream.Data!.Id, false);

        // Act
        var result = await depService.RemoveDependencyAsync(downstream.Data.Id, added.Data!.Id);

        // Assert
        result.Success.ShouldBeTrue();

        var deps = await depService.ListDependenciesAsync(downstream.Data.Id);
        deps.Data.ShouldNotBeNull();
        deps.Data!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ListDependencies_ReturnsUpstreamDependencies()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var jobService = new JobService(db, audit);
        var depService = new JobDependencyService(db);

        var up1 = await jobService.CreateAsync(new CreateJobRequest { Name = "Upstream 1" });
        var up2 = await jobService.CreateAsync(new CreateJobRequest { Name = "Upstream 2" });
        var down = await jobService.CreateAsync(new CreateJobRequest { Name = "Downstream" });

        await depService.AddDependencyAsync(down.Data!.Id, up1.Data!.Id, false);
        await depService.AddDependencyAsync(down.Data.Id, up2.Data!.Id, true);

        // Act
        var result = await depService.ListDependenciesAsync(down.Data.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Count.ShouldBe(2);
    }
}

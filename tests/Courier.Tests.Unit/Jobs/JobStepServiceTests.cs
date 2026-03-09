using Courier.Domain.Entities;
using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Jobs;

public class JobStepServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    [Fact]
    public async Task ReplaceStepsAsync_ReplacesAllSteps()
    {
        using var db = CreateInMemoryContext();
        var service = new JobStepService(db);

        var job = new Job { Id = Guid.NewGuid(), Name = "Test Job" };
        var oldStep = new JobStep
        {
            Id = Guid.NewGuid(), JobId = job.Id, Name = "Old Step",
            TypeKey = "file.copy", StepOrder = 1
        };
        db.Jobs.Add(job);
        db.JobSteps.Add(oldStep);
        await db.SaveChangesAsync();

        var newSteps = new List<StepInput>
        {
            new() { Name = "New Step 1", TypeKey = "file.copy", StepOrder = 1 },
            new() { Name = "New Step 2", TypeKey = "file.move", StepOrder = 2 }
        };

        var result = await service.ReplaceStepsAsync(job.Id, newSteps);

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Count.ShouldBe(2);
        result.Data[0].Name.ShouldBe("New Step 1");
        result.Data[1].Name.ShouldBe("New Step 2");

        var allSteps = await db.JobSteps.Where(s => s.JobId == job.Id).ToListAsync();
        allSteps.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ReplaceStepsAsync_NonExistentJob_ReturnsNotFound()
    {
        using var db = CreateInMemoryContext();
        var service = new JobStepService(db);

        var result = await service.ReplaceStepsAsync(Guid.NewGuid(), []);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(1030);
    }

    [Theory]
    [InlineData("job")]
    [InlineData("Job")]
    [InlineData("JOB")]
    [InlineData("loop")]
    [InlineData("Loop")]
    public async Task AddStepAsync_ReservedAlias_ReturnsValidationError(string alias)
    {
        using var db = CreateInMemoryContext();
        var service = new JobStepService(db);

        var job = new Job { Id = Guid.NewGuid(), Name = "Test Job" };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var result = await service.AddStepAsync(job.Id, new AddJobStepRequest
        {
            Name = "Test Step",
            TypeKey = "file.copy",
            StepOrder = 1,
            Alias = alias,
        }, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(1000);
        result.Error.Message.ShouldContain("reserved");
    }

    [Theory]
    [InlineData("job")]
    [InlineData("loop")]
    public async Task ReplaceStepsAsync_ReservedAlias_ReturnsValidationError(string alias)
    {
        using var db = CreateInMemoryContext();
        var service = new JobStepService(db);

        var job = new Job { Id = Guid.NewGuid(), Name = "Test Job" };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var steps = new List<StepInput>
        {
            new() { Name = "Step 1", TypeKey = "file.copy", StepOrder = 1, Alias = alias }
        };

        var result = await service.ReplaceStepsAsync(job.Id, steps);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(1000);
        result.Error.Message.ShouldContain("reserved");
    }
}

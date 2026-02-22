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
}

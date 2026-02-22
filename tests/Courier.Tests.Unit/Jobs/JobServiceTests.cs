using Courier.Domain.Entities;
using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Jobs;

public class JobServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsSuccessWithJob()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db);
        var request = new CreateJobRequest { Name = "Test Job", Description = "A test" };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Name.ShouldBe("Test Job");
        result.Data.Description.ShouldBe("A test");
        result.Data.Id.ShouldNotBe(Guid.Empty);
        result.Data.CurrentVersion.ShouldBe(1);
        result.Data.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task List_ReturnsAllJobs()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db);

        await service.CreateAsync(new CreateJobRequest { Name = "Job A" });
        await service.CreateAsync(new CreateJobRequest { Name = "Job B" });

        // Act
        var result = await service.ListAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.Count.ShouldBe(2);
        result.Pagination.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetById_ExistingJob_ReturnsJob()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db);
        var created = await service.CreateAsync(new CreateJobRequest { Name = "Find Me" });

        // Act
        var result = await service.GetByIdAsync(created.Data!.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Name.ShouldBe("Find Me");
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFoundError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobService(db);

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(1030);
    }
}

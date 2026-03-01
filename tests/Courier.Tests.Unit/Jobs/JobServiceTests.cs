using Courier.Domain.Entities;
using Courier.Features.AuditLog;
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
        var service = new JobService(db, new AuditService(db));
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
        var service = new JobService(db, new AuditService(db));

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
        var service = new JobService(db, new AuditService(db));
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
        var service = new JobService(db, new AuditService(db));

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(1030);
    }

    [Fact]
    public async Task UpdateAsync_ExistingJob_ReturnsUpdatedDto()
    {
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));
        var created = await service.CreateAsync(new CreateJobRequest { Name = "Old Name", Description = "Old Desc" });

        var result = await service.UpdateAsync(created.Data!.Id, "New Name", "New Desc");

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Name.ShouldBe("New Name");
        result.Data.Description.ShouldBe("New Desc");
        result.Data.CurrentVersion.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentJob_ReturnsNotFoundError()
    {
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));

        var result = await service.UpdateAsync(Guid.NewGuid(), "Name", null);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(1030);
    }

    [Fact]
    public async Task DeleteAsync_ExistingJob_SoftDeletes()
    {
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));
        var created = await service.CreateAsync(new CreateJobRequest { Name = "To Delete" });

        var result = await service.DeleteAsync(created.Data!.Id);

        result.Success.ShouldBeTrue();

        // Verify soft delete — bypass query filter
        var deleted = await db.Jobs.IgnoreQueryFilters()
            .FirstOrDefaultAsync(j => j.Id == created.Data.Id);
        deleted.ShouldNotBeNull();
        deleted!.IsDeleted.ShouldBeTrue();
        deleted.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentJob_ReturnsNotFound()
    {
        using var db = CreateInMemoryContext();
        var service = new JobService(db, new AuditService(db));

        var result = await service.DeleteAsync(Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(1030);
    }
}

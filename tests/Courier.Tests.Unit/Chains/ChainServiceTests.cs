using Courier.Domain.Common;
using Courier.Features.AuditLog;
using Courier.Features.Chains;
using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Chains;

public class ChainServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsSuccessWithChain()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new ChainService(db, new AuditService(db));
        var request = new CreateChainRequest { Name = "Test Chain", Description = "A test chain" };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Name.ShouldBe("Test Chain");
        result.Data.Description.ShouldBe("A test chain");
        result.Data.Id.ShouldNotBe(Guid.Empty);
        result.Data.IsEnabled.ShouldBeTrue();
        result.Data.Members.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetById_ExistingChain_ReturnsChain()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new ChainService(db, new AuditService(db));
        var created = await service.CreateAsync(new CreateChainRequest { Name = "Find Me" });

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
        var service = new ChainService(db, new AuditService(db));

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(ErrorCodes.ChainNotFound);
    }

    [Fact]
    public async Task List_ReturnsAllChains()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new ChainService(db, new AuditService(db));
        await service.CreateAsync(new CreateChainRequest { Name = "Chain A" });
        await service.CreateAsync(new CreateChainRequest { Name = "Chain B" });

        // Act
        var result = await service.ListAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.Count.ShouldBe(2);
        result.Pagination.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Update_ExistingChain_ReturnsUpdatedDto()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new ChainService(db, new AuditService(db));
        var created = await service.CreateAsync(new CreateChainRequest { Name = "Old Name", Description = "Old" });

        // Act
        var result = await service.UpdateAsync(created.Data!.Id, "New Name", "New");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Name.ShouldBe("New Name");
        result.Data.Description.ShouldBe("New");
    }

    [Fact]
    public async Task Delete_ExistingChain_SoftDeletes()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new ChainService(db, new AuditService(db));
        var created = await service.CreateAsync(new CreateChainRequest { Name = "To Delete" });

        // Act
        var result = await service.DeleteAsync(created.Data!.Id);

        // Assert
        result.Success.ShouldBeTrue();
        var deleted = await db.JobChains.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == created.Data.Id);
        deleted.ShouldNotBeNull();
        deleted!.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task ReplaceMembers_ValidMembers_Succeeds()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var auditService = new AuditService(db);
        var chainService = new ChainService(db, auditService);
        var jobService = new JobService(db, auditService);

        var job1 = await jobService.CreateAsync(new CreateJobRequest { Name = "Job 1" });
        var job2 = await jobService.CreateAsync(new CreateJobRequest { Name = "Job 2" });

        var chain = await chainService.CreateAsync(new CreateChainRequest { Name = "Chain" });

        var members = new List<ChainMemberInput>
        {
            new() { JobId = job1.Data!.Id, ExecutionOrder = 1 },
            new() { JobId = job2.Data!.Id, ExecutionOrder = 2, DependsOnMemberIndex = 0 }
        };

        // Act
        var result = await chainService.ReplaceMembersAsync(chain.Data!.Id, members);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Count.ShouldBe(2);
        result.Data[0].JobName.ShouldBe("Job 1");
        result.Data[1].JobName.ShouldBe("Job 2");
        result.Data[1].DependsOnMemberId.ShouldBe(result.Data[0].Id);
    }

    [Fact]
    public async Task ReplaceMembers_InvalidJobId_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new ChainService(db, new AuditService(db));
        var chain = await service.CreateAsync(new CreateChainRequest { Name = "Chain" });

        var members = new List<ChainMemberInput>
        {
            new() { JobId = Guid.NewGuid(), ExecutionOrder = 1 }
        };

        // Act
        var result = await service.ReplaceMembersAsync(chain.Data!.Id, members);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ChainMemberJobNotFound);
    }

    [Fact]
    public async Task ReplaceMembers_SelfDependency_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var auditService = new AuditService(db);
        var chainService = new ChainService(db, auditService);
        var jobService = new JobService(db, auditService);

        var job = await jobService.CreateAsync(new CreateJobRequest { Name = "Job 1" });
        var chain = await chainService.CreateAsync(new CreateChainRequest { Name = "Chain" });

        var members = new List<ChainMemberInput>
        {
            new() { JobId = job.Data!.Id, ExecutionOrder = 1, DependsOnMemberIndex = 0 }
        };

        // Act
        var result = await chainService.ReplaceMembersAsync(chain.Data!.Id, members);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.CircularDependency);
    }
}

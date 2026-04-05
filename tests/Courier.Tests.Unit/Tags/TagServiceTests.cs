using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Features.AuditLog;
using Courier.Features.Tags;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Tags;

public class TagServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    private static TagService CreateService(CourierDbContext db) =>
        new(db, new AuditService(db));

    #region Create

    [Fact]
    public async Task Create_ValidRequest_ReturnsSuccessWithTag()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);
        var request = new CreateTagRequest
        {
            Name = "Production",
            Color = "#FF0000",
            Category = "environment",
            Description = "Production environment tag"
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Name.ShouldBe("Production");
        result.Data.Color.ShouldBe("#FF0000");
        result.Data.Category.ShouldBe("environment");
        result.Data.Description.ShouldBe("Production environment tag");
        result.Data.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsDuplicateError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);
        await service.CreateAsync(new CreateTagRequest { Name = "Production" });

        // Act — same name, different case
        var result = await service.CreateAsync(new CreateTagRequest { Name = "production" });

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(ErrorCodes.DuplicateTagName);
    }

    #endregion

    #region GetById

    [Fact]
    public async Task GetById_ExistingTag_ReturnsTag()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);
        var created = await service.CreateAsync(new CreateTagRequest { Name = "Find Me" });

        // Act
        var result = await service.GetByIdAsync(created.Data!.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Name.ShouldBe("Find Me");
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFoundError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }

    #endregion

    #region List

    [Fact]
    public async Task List_ReturnsAll()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);
        await service.CreateAsync(new CreateTagRequest { Name = "Tag A" });
        await service.CreateAsync(new CreateTagRequest { Name = "Tag B" });
        await service.CreateAsync(new CreateTagRequest { Name = "Tag C" });

        // Act
        var result = await service.ListAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.Count.ShouldBe(3);
        result.Pagination.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task List_FilterBySearch_ReturnsMatching()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);
        await service.CreateAsync(new CreateTagRequest { Name = "Production" });
        await service.CreateAsync(new CreateTagRequest { Name = "Staging" });
        await service.CreateAsync(new CreateTagRequest { Name = "Prod-Legacy" });

        // Act
        var result = await service.ListAsync(search: "prod");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.Count.ShouldBe(2);
        result.Data.ShouldAllBe(t => t.Name.Contains("prod", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task List_FilterByCategory_ReturnsMatching()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);
        await service.CreateAsync(new CreateTagRequest { Name = "Production", Category = "environment" });
        await service.CreateAsync(new CreateTagRequest { Name = "Critical", Category = "priority" });
        await service.CreateAsync(new CreateTagRequest { Name = "Staging", Category = "environment" });

        // Act
        var result = await service.ListAsync(category: "environment");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.Count.ShouldBe(2);
        result.Data.ShouldAllBe(t => t.Category == "environment");
    }

    #endregion

    #region Update

    [Fact]
    public async Task Update_ValidRequest_ReturnsUpdatedTag()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);
        var created = await service.CreateAsync(new CreateTagRequest
        {
            Name = "Old Name",
            Color = "#000000",
            Category = "old",
            Description = "Old description"
        });

        var updateRequest = new UpdateTagRequest
        {
            Name = "New Name",
            Color = "#FFFFFF",
            Category = "new",
            Description = "New description"
        };

        // Act
        var result = await service.UpdateAsync(created.Data!.Id, updateRequest);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Name.ShouldBe("New Name");
        result.Data.Color.ShouldBe("#FFFFFF");
        result.Data.Category.ShouldBe("new");
        result.Data.Description.ShouldBe("New description");
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNotFoundError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        // Act
        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateTagRequest { Name = "Whatever" });

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }

    [Fact]
    public async Task Update_DuplicateName_ReturnsDuplicateError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);
        await service.CreateAsync(new CreateTagRequest { Name = "Existing Tag" });
        var second = await service.CreateAsync(new CreateTagRequest { Name = "Second Tag" });

        // Act — try to rename second tag to existing name (case-insensitive)
        var result = await service.UpdateAsync(
            second.Data!.Id,
            new UpdateTagRequest { Name = "existing tag" });

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(ErrorCodes.DuplicateTagName);
    }

    #endregion

    #region Delete

    [Fact]
    public async Task Delete_ExistingTag_SoftDeletes()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);
        var created = await service.CreateAsync(new CreateTagRequest { Name = "To Delete" });
        var tagId = created.Data!.Id;

        // Act
        var result = await service.DeleteAsync(tagId);

        // Assert
        result.Success.ShouldBeTrue();

        // Verify soft delete — bypass query filter
        var deleted = await db.Tags.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tagId);
        deleted.ShouldNotBeNull();
        deleted!.IsDeleted.ShouldBeTrue();
        deleted.DeletedAt.ShouldNotBeNull();

        // Should not appear in normal queries
        var notFound = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId);
        notFound.ShouldBeNull();
    }

    #endregion

    #region AssignTags

    [Fact]
    public async Task AssignTags_ValidAssignment_CreatesEntityTag()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        var tag = await service.CreateAsync(new CreateTagRequest { Name = "Important" });
        var tagId = tag.Data!.Id;

        var jobId = Guid.CreateVersion7();
        db.Jobs.Add(new Job
        {
            Id = jobId,
            Name = "Test Job",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var request = new BulkTagAssignmentRequest
        {
            Assignments =
            [
                new TagAssignment { TagId = tagId, EntityType = "job", EntityId = jobId }
            ]
        };

        // Act
        var result = await service.AssignTagsAsync(request);

        // Assert
        result.Success.ShouldBeTrue();

        var entityTag = await db.EntityTags.FirstOrDefaultAsync(et => et.TagId == tagId && et.EntityId == jobId);
        entityTag.ShouldNotBeNull();
        entityTag!.EntityType.ShouldBe("job");
    }

    [Fact]
    public async Task AssignTags_DuplicateAssignment_IsIdempotent()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        var tag = await service.CreateAsync(new CreateTagRequest { Name = "Important" });
        var tagId = tag.Data!.Id;

        var jobId = Guid.CreateVersion7();
        db.Jobs.Add(new Job
        {
            Id = jobId,
            Name = "Test Job",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var request = new BulkTagAssignmentRequest
        {
            Assignments =
            [
                new TagAssignment { TagId = tagId, EntityType = "job", EntityId = jobId }
            ]
        };

        // Assign once
        await service.AssignTagsAsync(request);

        // Act — assign again (duplicate)
        var result = await service.AssignTagsAsync(request);

        // Assert
        result.Success.ShouldBeTrue();

        var count = await db.EntityTags.CountAsync(et => et.TagId == tagId && et.EntityId == jobId);
        count.ShouldBe(1); // Should not create duplicate
    }

    [Fact]
    public async Task AssignTags_MissingTag_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        var jobId = Guid.CreateVersion7();
        db.Jobs.Add(new Job
        {
            Id = jobId,
            Name = "Test Job",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var request = new BulkTagAssignmentRequest
        {
            Assignments =
            [
                new TagAssignment { TagId = Guid.NewGuid(), EntityType = "job", EntityId = jobId }
            ]
        };

        // Act
        var result = await service.AssignTagsAsync(request);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }

    [Fact]
    public async Task AssignTags_JobChainEntityType_NonExistentChain_ReturnsEntityNotFound()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        var tag = await service.CreateAsync(new CreateTagRequest { Name = "Chained" });

        var request = new BulkTagAssignmentRequest
        {
            Assignments =
            [
                new TagAssignment { TagId = tag.Data!.Id, EntityType = "job_chain", EntityId = Guid.NewGuid() }
            ]
        };

        // Act
        var result = await service.AssignTagsAsync(request);

        // Assert — job_chain is a valid entity type, but the chain doesn't exist
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(ErrorCodes.TagEntityNotFound);
    }

    #endregion

    #region UnassignTags

    [Fact]
    public async Task UnassignTags_ExistingAssignment_RemovesEntityTag()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        var tag = await service.CreateAsync(new CreateTagRequest { Name = "ToRemove" });
        var tagId = tag.Data!.Id;

        var jobId = Guid.CreateVersion7();
        db.Jobs.Add(new Job
        {
            Id = jobId,
            Name = "Test Job",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var assignRequest = new BulkTagAssignmentRequest
        {
            Assignments =
            [
                new TagAssignment { TagId = tagId, EntityType = "job", EntityId = jobId }
            ]
        };
        await service.AssignTagsAsync(assignRequest);

        // Act
        var result = await service.UnassignTagsAsync(assignRequest);

        // Assert
        result.Success.ShouldBeTrue();

        var entityTag = await db.EntityTags.FirstOrDefaultAsync(et => et.TagId == tagId && et.EntityId == jobId);
        entityTag.ShouldBeNull();
    }

    [Fact]
    public async Task UnassignTags_NonExistent_IsIdempotent()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        var request = new BulkTagAssignmentRequest
        {
            Assignments =
            [
                new TagAssignment { TagId = Guid.NewGuid(), EntityType = "job", EntityId = Guid.NewGuid() }
            ]
        };

        // Act — unassign something that was never assigned
        var result = await service.UnassignTagsAsync(request);

        // Assert
        result.Success.ShouldBeTrue();
    }

    #endregion

    #region ListEntitiesByTag

    [Fact]
    public async Task ListEntitiesByTag_ReturnsAssignedEntities()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        var tag = await service.CreateAsync(new CreateTagRequest { Name = "Shared" });
        var tagId = tag.Data!.Id;

        var jobId1 = Guid.CreateVersion7();
        var jobId2 = Guid.CreateVersion7();
        db.Jobs.Add(new Job { Id = jobId1, Name = "Job 1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Jobs.Add(new Job { Id = jobId2, Name = "Job 2", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await service.AssignTagsAsync(new BulkTagAssignmentRequest
        {
            Assignments =
            [
                new TagAssignment { TagId = tagId, EntityType = "job", EntityId = jobId1 },
                new TagAssignment { TagId = tagId, EntityType = "job", EntityId = jobId2 }
            ]
        });

        // Act
        var result = await service.ListEntitiesByTagAsync(tagId);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.Count.ShouldBe(2);
        result.Pagination.TotalCount.ShouldBe(2);
        result.Data.ShouldContain(e => e.EntityId == jobId1);
        result.Data.ShouldContain(e => e.EntityId == jobId2);
    }

    #endregion
}

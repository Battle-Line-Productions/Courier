using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Features.AuditLog;
using Courier.Features.Notifications;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Courier.Tests.Unit.Notifications;

public class NotificationRuleServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    private static NotificationRuleService CreateService(CourierDbContext db)
    {
        var audit = new AuditService(db);
        var dispatcher = new NotificationDispatcher(db, [], NullLogger<NotificationDispatcher>.Instance);
        return new NotificationRuleService(db, audit, dispatcher);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsSuccessWithRule()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);
        var request = new CreateNotificationRuleRequest
        {
            Name = "Job Failure Alert",
            Description = "Alert on job failures",
            EntityType = "job",
            EventTypes = ["job_failed"],
            Channel = "webhook",
            ChannelConfig = new { url = "https://example.com/webhook" },
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Name.ShouldBe("Job Failure Alert");
        result.Data.Channel.ShouldBe("webhook");
        result.Data.EventTypes.ShouldContain("job_failed");
        result.Data.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        db.NotificationRules.Add(new NotificationRule
        {
            Id = Guid.CreateVersion7(),
            Name = "Existing Rule",
            EntityType = "job",
            Channel = "webhook",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var request = new CreateNotificationRuleRequest
        {
            Name = "existing rule",
            EntityType = "job",
            EventTypes = ["job_failed"],
            Channel = "webhook",
            ChannelConfig = new { url = "https://example.com" },
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.DuplicateNotificationRuleName);
    }

    [Fact]
    public async Task GetById_ExistingRule_ReturnsRule()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);
        var ruleId = Guid.CreateVersion7();

        db.NotificationRules.Add(new NotificationRule
        {
            Id = ruleId,
            Name = "Test Rule",
            EntityType = "job",
            Channel = "email",
            EventTypes = """["job_completed"]""",
            ChannelConfig = """{"recipients":["test@example.com"]}""",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(ruleId);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Name.ShouldBe("Test Rule");
        result.Data.Channel.ShouldBe("email");
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.NotificationRuleNotFound);
    }

    [Fact]
    public async Task List_ReturnsPagedResults()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        for (var i = 0; i < 5; i++)
        {
            db.NotificationRules.Add(new NotificationRule
            {
                Id = Guid.CreateVersion7(),
                Name = $"Rule {i}",
                EntityType = "job",
                Channel = i % 2 == 0 ? "webhook" : "email",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();

        // Act
        var result = await service.ListAsync(page: 1, pageSize: 3);

        // Assert
        result.Data.Count.ShouldBe(3);
        result.Pagination.TotalCount.ShouldBe(5);
        result.Pagination.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task List_FilterByChannel_ReturnsFilteredResults()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        db.NotificationRules.Add(new NotificationRule { Id = Guid.CreateVersion7(), Name = "Webhook Rule", EntityType = "job", Channel = "webhook", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.NotificationRules.Add(new NotificationRule { Id = Guid.CreateVersion7(), Name = "Email Rule", EntityType = "job", Channel = "email", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        // Act
        var result = await service.ListAsync(channel: "webhook");

        // Assert
        result.Data.Count.ShouldBe(1);
        result.Data[0].Channel.ShouldBe("webhook");
    }

    [Fact]
    public async Task Update_ValidRequest_UpdatesRule()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);
        var ruleId = Guid.CreateVersion7();

        db.NotificationRules.Add(new NotificationRule
        {
            Id = ruleId,
            Name = "Original",
            EntityType = "job",
            Channel = "webhook",
            ChannelConfig = "{}",
            EventTypes = "[]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var request = new UpdateNotificationRuleRequest
        {
            Name = "Updated",
            EntityType = "monitor",
            EventTypes = ["job_completed"],
            Channel = "email",
            ChannelConfig = new { recipients = new[] { "admin@example.com" } },
            IsEnabled = false,
        };

        // Act
        var result = await service.UpdateAsync(ruleId, request);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Name.ShouldBe("Updated");
        result.Data.EntityType.ShouldBe("monitor");
        result.Data.Channel.ShouldBe("email");
        result.Data.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_ExistingRule_SoftDeletes()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = CreateService(db);
        var ruleId = Guid.CreateVersion7();

        db.NotificationRules.Add(new NotificationRule
        {
            Id = ruleId,
            Name = "To Delete",
            EntityType = "job",
            Channel = "webhook",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.DeleteAsync(ruleId);

        // Assert
        result.Success.ShouldBeTrue();

        var deleted = await db.NotificationRules.IgnoreQueryFilters().FirstAsync(r => r.Id == ruleId);
        deleted.IsDeleted.ShouldBeTrue();
        deleted.DeletedAt.ShouldNotBeNull();
    }
}

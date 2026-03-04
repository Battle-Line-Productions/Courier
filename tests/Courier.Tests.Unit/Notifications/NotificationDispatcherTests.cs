using Courier.Domain.Entities;
using Courier.Features.Notifications;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Notifications;

public class NotificationDispatcherTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    [Fact]
    public async Task DispatchAsync_MatchingRule_SendsAndLogs()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var ruleId = Guid.CreateVersion7();

        db.NotificationRules.Add(new NotificationRule
        {
            Id = ruleId,
            Name = "Failure Alert",
            EntityType = "job",
            EventTypes = """["job_failed"]""",
            Channel = "webhook",
            ChannelConfig = """{"url":"https://example.com/hook"}""",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var channel = Substitute.For<INotificationChannel>();
        channel.ChannelKey.Returns("webhook");
        channel.SendAsync(Arg.Any<string>(), Arg.Any<NotificationEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ChannelResult(true, "https://example.com/hook"));

        var dispatcher = new NotificationDispatcher(db, [channel], NullLogger<NotificationDispatcher>.Instance);

        var notifEvent = new NotificationEvent
        {
            EventType = "job_failed",
            EntityType = "job",
            EntityId = Guid.NewGuid(),
            EntityName = "Test Job",
        };

        // Act
        await dispatcher.DispatchAsync(notifEvent);

        // Assert
        await channel.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<NotificationEvent>(), Arg.Any<CancellationToken>());

        var log = await db.NotificationLogs.FirstOrDefaultAsync();
        log.ShouldNotBeNull();
        log.NotificationRuleId.ShouldBe(ruleId);
        log.Success.ShouldBeTrue();
        log.Channel.ShouldBe("webhook");
    }

    [Fact]
    public async Task DispatchAsync_NonMatchingEventType_DoesNotSend()
    {
        // Arrange
        using var db = CreateInMemoryContext();

        db.NotificationRules.Add(new NotificationRule
        {
            Id = Guid.CreateVersion7(),
            Name = "Completion Alert",
            EntityType = "job",
            EventTypes = """["job_completed"]""",
            Channel = "webhook",
            ChannelConfig = "{}",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var channel = Substitute.For<INotificationChannel>();
        channel.ChannelKey.Returns("webhook");

        var dispatcher = new NotificationDispatcher(db, [channel], NullLogger<NotificationDispatcher>.Instance);

        var notifEvent = new NotificationEvent
        {
            EventType = "job_failed",
            EntityType = "job",
            EntityId = Guid.NewGuid(),
        };

        // Act
        await dispatcher.DispatchAsync(notifEvent);

        // Assert
        await channel.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<NotificationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_DisabledRule_DoesNotSend()
    {
        // Arrange
        using var db = CreateInMemoryContext();

        db.NotificationRules.Add(new NotificationRule
        {
            Id = Guid.CreateVersion7(),
            Name = "Disabled Rule",
            EntityType = "job",
            EventTypes = """["job_failed"]""",
            Channel = "webhook",
            ChannelConfig = "{}",
            IsEnabled = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var channel = Substitute.For<INotificationChannel>();
        channel.ChannelKey.Returns("webhook");

        var dispatcher = new NotificationDispatcher(db, [channel], NullLogger<NotificationDispatcher>.Instance);

        var notifEvent = new NotificationEvent
        {
            EventType = "job_failed",
            EntityType = "job",
            EntityId = Guid.NewGuid(),
        };

        // Act
        await dispatcher.DispatchAsync(notifEvent);

        // Assert
        await channel.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<NotificationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_EntitySpecificRule_MatchesCorrectEntity()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var targetJobId = Guid.NewGuid();

        db.NotificationRules.Add(new NotificationRule
        {
            Id = Guid.CreateVersion7(),
            Name = "Specific Job Alert",
            EntityType = "job",
            EntityId = targetJobId,
            EventTypes = """["job_failed"]""",
            Channel = "webhook",
            ChannelConfig = """{"url":"https://example.com"}""",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var channel = Substitute.For<INotificationChannel>();
        channel.ChannelKey.Returns("webhook");
        channel.SendAsync(Arg.Any<string>(), Arg.Any<NotificationEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ChannelResult(true, "https://example.com"));

        var dispatcher = new NotificationDispatcher(db, [channel], NullLogger<NotificationDispatcher>.Instance);

        // Act — matching entity
        await dispatcher.DispatchAsync(new NotificationEvent { EventType = "job_failed", EntityType = "job", EntityId = targetJobId });

        // Assert
        await channel.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<NotificationEvent>(), Arg.Any<CancellationToken>());

        // Act — different entity
        channel.ClearReceivedCalls();
        await dispatcher.DispatchAsync(new NotificationEvent { EventType = "job_failed", EntityType = "job", EntityId = Guid.NewGuid() });

        // Assert — should not match
        await channel.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<NotificationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_ChannelFailure_LogsErrorAndDoesNotThrow()
    {
        // Arrange
        using var db = CreateInMemoryContext();

        db.NotificationRules.Add(new NotificationRule
        {
            Id = Guid.CreateVersion7(),
            Name = "Failing Rule",
            EntityType = "job",
            EventTypes = """["job_failed"]""",
            Channel = "webhook",
            ChannelConfig = "{}",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var channel = Substitute.For<INotificationChannel>();
        channel.ChannelKey.Returns("webhook");
        channel.SendAsync(Arg.Any<string>(), Arg.Any<NotificationEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ChannelResult(false, "https://example.com", "Connection refused"));

        var dispatcher = new NotificationDispatcher(db, [channel], NullLogger<NotificationDispatcher>.Instance);

        var notifEvent = new NotificationEvent
        {
            EventType = "job_failed",
            EntityType = "job",
            EntityId = Guid.NewGuid(),
        };

        // Act — should not throw
        await dispatcher.DispatchAsync(notifEvent);

        // Assert
        var log = await db.NotificationLogs.FirstOrDefaultAsync();
        log.ShouldNotBeNull();
        log.Success.ShouldBeFalse();
        log.ErrorMessage.ShouldBe("Connection refused");
    }
}

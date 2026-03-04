using System.Text.Json;
using Courier.Domain.Entities;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Courier.Features.Notifications;

public class NotificationDispatcher
{
    private readonly CourierDbContext _db;
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(CourierDbContext db, IEnumerable<INotificationChannel> channels, ILogger<NotificationDispatcher> logger)
    {
        _db = db;
        _channels = channels;
        _logger = logger;
    }

    public async Task DispatchAsync(NotificationEvent notificationEvent, CancellationToken ct = default)
    {
        try
        {
            var rules = await _db.NotificationRules
                .Where(r => r.IsEnabled
                    && r.EntityType == notificationEvent.EntityType
                    && (r.EntityId == null || r.EntityId == notificationEvent.EntityId))
                .ToListAsync(ct);

            foreach (var rule in rules)
            {
                // Check if the rule's event types include this event
                if (!RuleMatchesEvent(rule, notificationEvent.EventType))
                    continue;

                var channel = _channels.FirstOrDefault(c => c.ChannelKey == rule.Channel);
                if (channel is null)
                {
                    _logger.LogWarning("No channel handler found for channel '{Channel}' on rule '{RuleName}'", rule.Channel, rule.Name);
                    await LogNotificationAsync(rule, notificationEvent, false, rule.Channel, "unknown", $"Channel '{rule.Channel}' not registered.", ct);
                    continue;
                }

                ChannelResult result;
                try
                {
                    result = await channel.SendAsync(rule.ChannelConfig, notificationEvent, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception sending notification via {Channel} for rule '{RuleName}'", rule.Channel, rule.Name);
                    result = new ChannelResult(false, "unknown", ex.Message);
                }

                await LogNotificationAsync(rule, notificationEvent, result.Success, rule.Channel, result.Recipient, result.ErrorMessage, ct);
            }
        }
        catch (Exception ex)
        {
            // Notification dispatch must never fail the calling operation
            _logger.LogError(ex, "Failed to dispatch notifications for event {EventType} on {EntityType}/{EntityId}",
                notificationEvent.EventType, notificationEvent.EntityType, notificationEvent.EntityId);
        }
    }

    private static bool RuleMatchesEvent(NotificationRule rule, string eventType)
    {
        try
        {
            var eventTypes = JsonSerializer.Deserialize<List<string>>(rule.EventTypes);
            return eventTypes is not null && eventTypes.Contains(eventType);
        }
        catch
        {
            return false;
        }
    }

    private async Task LogNotificationAsync(
        NotificationRule rule,
        NotificationEvent notificationEvent,
        bool success,
        string channel,
        string recipient,
        string? errorMessage,
        CancellationToken ct)
    {
        var log = new NotificationLog
        {
            Id = Guid.CreateVersion7(),
            NotificationRuleId = rule.Id,
            EventType = notificationEvent.EventType,
            EntityType = notificationEvent.EntityType,
            EntityId = notificationEvent.EntityId,
            Channel = channel,
            Recipient = recipient,
            Payload = JsonSerializer.Serialize(new
            {
                eventType = notificationEvent.EventType,
                entityType = notificationEvent.EntityType,
                entityId = notificationEvent.EntityId,
                entityName = notificationEvent.EntityName,
            }),
            Success = success,
            ErrorMessage = errorMessage,
            SentAt = DateTime.UtcNow,
        };

        _db.NotificationLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        if (success)
            _logger.LogInformation("Notification sent via {Channel} to {Recipient} for rule '{RuleName}'", channel, recipient, rule.Name);
        else
            _logger.LogWarning("Notification failed via {Channel} for rule '{RuleName}': {Error}", channel, rule.Name, errorMessage);
    }
}

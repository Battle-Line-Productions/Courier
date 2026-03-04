namespace Courier.Features.Notifications;

public interface INotificationChannel
{
    string ChannelKey { get; }

    Task<ChannelResult> SendAsync(string channelConfigJson, NotificationEvent notificationEvent, CancellationToken ct = default);
}

public record ChannelResult(bool Success, string Recipient, string? ErrorMessage = null);

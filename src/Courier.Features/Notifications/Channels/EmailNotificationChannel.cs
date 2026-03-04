using System.Text.Json;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Courier.Features.Notifications.Channels;

public class EmailNotificationChannel : INotificationChannel
{
    private readonly SmtpSettings _smtp;
    private readonly ILogger<EmailNotificationChannel> _logger;

    public string ChannelKey => "email";

    public EmailNotificationChannel(IOptions<SmtpSettings> smtpOptions, ILogger<EmailNotificationChannel> logger)
    {
        _smtp = smtpOptions.Value;
        _logger = logger;
    }

    public async Task<ChannelResult> SendAsync(string channelConfigJson, NotificationEvent notificationEvent, CancellationToken ct = default)
    {
        var config = JsonSerializer.Deserialize<EmailConfig>(channelConfigJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (config is null || config.Recipients is null || config.Recipients.Count == 0)
            return new ChannelResult(false, "unknown", "No email recipients configured.");

        var recipientList = string.Join(", ", config.Recipients);
        var subjectPrefix = config.SubjectPrefix ?? "[Courier]";

        var subject = $"{subjectPrefix} {FormatEventType(notificationEvent.EventType)}: {notificationEvent.EntityName ?? notificationEvent.EntityId.ToString()}";

        var body = FormatPlainTextBody(notificationEvent);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromAddress));

        foreach (var recipient in config.Recipients)
            message.To.Add(MailboxAddress.Parse(recipient));

        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_smtp.Host, _smtp.Port, _smtp.UseSsl, ct);

            if (!string.IsNullOrWhiteSpace(_smtp.Username))
                await client.AuthenticateAsync(_smtp.Username, _smtp.Password, ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Email notification sent to {Recipients}", recipientList);
            return new ChannelResult(true, recipientList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipients}", recipientList);
            return new ChannelResult(false, recipientList, ex.Message);
        }
    }

    private static string FormatEventType(string eventType) =>
        eventType.Replace('_', ' ').ToUpperInvariant();

    private static string FormatPlainTextBody(NotificationEvent e)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Event: {e.EventType}");
        sb.AppendLine($"Entity Type: {e.EntityType}");
        sb.AppendLine($"Entity ID: {e.EntityId}");

        if (!string.IsNullOrWhiteSpace(e.EntityName))
            sb.AppendLine($"Entity Name: {e.EntityName}");

        sb.AppendLine($"Time: {DateTime.UtcNow:u}");

        if (e.Context.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Details:");
            foreach (var kvp in e.Context)
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
        }

        return sb.ToString();
    }

    private class EmailConfig
    {
        public List<string>? Recipients { get; set; }
        public string? SubjectPrefix { get; set; }
    }
}

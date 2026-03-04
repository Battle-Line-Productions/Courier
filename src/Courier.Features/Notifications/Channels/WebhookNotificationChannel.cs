using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Courier.Features.Notifications.Channels;

public class WebhookNotificationChannel : INotificationChannel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookNotificationChannel> _logger;

    public string ChannelKey => "webhook";

    public WebhookNotificationChannel(IHttpClientFactory httpClientFactory, ILogger<WebhookNotificationChannel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ChannelResult> SendAsync(string channelConfigJson, NotificationEvent notificationEvent, CancellationToken ct = default)
    {
        var config = JsonSerializer.Deserialize<WebhookConfig>(channelConfigJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (config is null || string.IsNullOrWhiteSpace(config.Url))
            return new ChannelResult(false, "unknown", "Webhook URL is not configured.");

        var client = _httpClientFactory.CreateClient("Webhooks");

        var payload = new
        {
            eventType = notificationEvent.EventType,
            entityType = notificationEvent.EntityType,
            entityId = notificationEvent.EntityId,
            entityName = notificationEvent.EntityName,
            context = notificationEvent.Context,
            timestamp = DateTime.UtcNow,
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Add HMAC signature if secret is configured
        if (!string.IsNullOrWhiteSpace(config.Secret))
        {
            var signature = ComputeHmacSha256(json, config.Secret);
            content.Headers.Add("X-Courier-Signature", $"sha256={signature}");
        }

        // Add custom headers
        if (config.Headers is not null)
        {
            foreach (var header in config.Headers)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        try
        {
            var response = await client.PostAsync(config.Url, content, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Webhook sent successfully to {Url}", config.Url);
                return new ChannelResult(true, config.Url);
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Webhook to {Url} returned {StatusCode}: {Body}", config.Url, response.StatusCode, errorBody);
            return new ChannelResult(false, config.Url, $"HTTP {(int)response.StatusCode}: {errorBody}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send webhook to {Url}", config.Url);
            return new ChannelResult(false, config.Url, ex.Message);
        }
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexStringLower(hash);
    }

    private class WebhookConfig
    {
        public string Url { get; set; } = string.Empty;
        public string? Secret { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
    }
}

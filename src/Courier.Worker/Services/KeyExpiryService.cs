using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Features.Notifications;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Worker.Services;

public class KeyExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KeyExpiryService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

    public KeyExpiryService(IServiceScopeFactory scopeFactory, ILogger<KeyExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KeyExpiryService started. Checking every {Hours}h", _checkInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckKeyExpirationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in key expiry check");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("KeyExpiryService stopping");
    }

    private async Task CheckKeyExpirationsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourierDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<AuditService>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();

        var now = DateTime.UtcNow;

        // Read warning window from system settings (default 30 days)
        var warningDaysSetting = await db.SystemSettings
            .Where(s => s.Key == "key.expiration_warning_days")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
        var warningDays = int.TryParse(warningDaysSetting, out var wd) ? wd : 30;
        var warningThreshold = now.AddDays(warningDays);

        // Find active keys that are within the warning window but not yet expired
        var expiringKeys = await db.PgpKeys
            .Where(k => k.Status == "active"
                     && k.ExpiresAt != null
                     && k.ExpiresAt <= warningThreshold
                     && k.ExpiresAt > now
                     && !k.IsDeleted)
            .ToListAsync(ct);

        foreach (var key in expiringKeys)
        {
            key.Status = "expiring";
            key.UpdatedAt = now;

            _logger.LogInformation("PGP key {KeyId} ({KeyName}) transitioning to Expiring — expires at {ExpiresAt}",
                key.Id, key.Name, key.ExpiresAt);
        }

        // Find active or expiring keys that have already expired
        var expiredKeys = await db.PgpKeys
            .Where(k => (k.Status == "active" || k.Status == "expiring")
                     && k.ExpiresAt != null
                     && k.ExpiresAt <= now
                     && !k.IsDeleted)
            .ToListAsync(ct);

        foreach (var key in expiredKeys)
        {
            key.Status = "retired";
            key.UpdatedAt = now;

            _logger.LogInformation("PGP key {KeyId} ({KeyName}) transitioning to Retired — expired at {ExpiresAt}",
                key.Id, key.Name, key.ExpiresAt);
        }

        if (expiringKeys.Count == 0 && expiredKeys.Count == 0)
            return;

        // Persist status changes before audit/notification side effects
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Key expiry check: {ExpiringCount} keys now expiring, {ExpiredCount} keys retired",
            expiringKeys.Count, expiredKeys.Count);

        // Audit and notify after persisting — these each call SaveChangesAsync internally
        foreach (var key in expiringKeys)
        {
            await audit.LogAsync(AuditableEntityType.PgpKey, key.Id, "KeyExpiring",
                details: new { expiresAt = key.ExpiresAt, daysRemaining = (key.ExpiresAt!.Value - now).Days }, ct: ct);

            await DispatchKeyNotificationAsync(dispatcher, key.Id, key.Name, key.ExpiresAt, "key_expiring", ct);
        }

        foreach (var key in expiredKeys)
        {
            await audit.LogAsync(AuditableEntityType.PgpKey, key.Id, "KeyRetired",
                details: new { expiresAt = key.ExpiresAt, reason = "expired" }, ct: ct);

            await DispatchKeyNotificationAsync(dispatcher, key.Id, key.Name, key.ExpiresAt, "key_expired", ct);
        }
    }

    private async Task DispatchKeyNotificationAsync(
        NotificationDispatcher dispatcher, Guid keyId, string keyName, DateTime? expiresAt, string eventType, CancellationToken ct)
    {
        try
        {
            var notificationEvent = new NotificationEvent
            {
                EventType = eventType,
                EntityType = "pgp_key",
                EntityId = keyId,
                EntityName = keyName,
                Context = new Dictionary<string, object>
                {
                    ["keyId"] = keyId,
                    ["expiresAt"] = expiresAt?.ToString("O") ?? "",
                },
            };

            await dispatcher.DispatchAsync(notificationEvent, ct);
        }
        catch (Exception ex)
        {
            // Non-critical — don't let notification failure break expiry processing
            _logger.LogWarning(ex, "Failed to dispatch {EventType} notification for key {KeyId}", eventType, keyId);
        }
    }
}

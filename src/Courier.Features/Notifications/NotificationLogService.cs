using System.Text.Json;
using Courier.Domain.Common;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Notifications;

public class NotificationLogService
{
    private readonly CourierDbContext _db;

    public NotificationLogService(CourierDbContext db)
    {
        _db = db;
    }

    public async Task<PagedApiResponse<NotificationLogDto>> ListAsync(
        int page = 1,
        int pageSize = 25,
        Guid? ruleId = null,
        string? entityType = null,
        Guid? entityId = null,
        bool? success = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.NotificationLogs
            .Include(l => l.NotificationRule)
            .AsQueryable();

        if (ruleId.HasValue)
            query = query.Where(l => l.NotificationRuleId == ruleId.Value);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(l => l.EntityType == entityType);

        if (entityId.HasValue)
            query = query.Where(l => l.EntityId == entityId.Value);

        if (success.HasValue)
            query = query.Where(l => l.Success == success.Value);

        query = query.OrderByDescending(l => l.SentAt);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedApiResponse<NotificationLogDto>
        {
            Data = items.Select(MapToDto).ToList(),
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    private static NotificationLogDto MapToDto(Domain.Entities.NotificationLog log) => new()
    {
        Id = log.Id,
        NotificationRuleId = log.NotificationRuleId,
        RuleName = log.NotificationRule?.Name,
        EventType = log.EventType,
        EntityType = log.EntityType,
        EntityId = log.EntityId,
        Channel = log.Channel,
        Recipient = log.Recipient,
        Payload = DeserializeJsonObject(log.Payload),
        Success = log.Success,
        ErrorMessage = log.ErrorMessage,
        SentAt = log.SentAt,
    };

    private static object DeserializeJsonObject(string json)
    {
        try { return JsonSerializer.Deserialize<JsonElement>(json); }
        catch { return new { }; }
    }
}

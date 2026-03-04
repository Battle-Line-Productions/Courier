using System.Text.Json;
using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Notifications;

public class NotificationRuleService
{
    private readonly CourierDbContext _db;
    private readonly AuditService _audit;
    private readonly NotificationDispatcher _dispatcher;

    public NotificationRuleService(CourierDbContext db, AuditService audit, NotificationDispatcher dispatcher)
    {
        _db = db;
        _audit = audit;
        _dispatcher = dispatcher;
    }

    public async Task<ApiResponse<NotificationRuleDto>> CreateAsync(CreateNotificationRuleRequest request, CancellationToken ct = default)
    {
        var duplicateExists = await _db.NotificationRules
            .AnyAsync(r => r.Name.ToLower() == request.Name.ToLower(), ct);

        if (duplicateExists)
        {
            return new ApiResponse<NotificationRuleDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.DuplicateNotificationRuleName, $"A notification rule with name '{request.Name}' already exists.")
            };
        }

        var rule = new NotificationRule
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Description = request.Description,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            EventTypes = JsonSerializer.Serialize(request.EventTypes),
            Channel = request.Channel,
            ChannelConfig = JsonSerializer.Serialize(request.ChannelConfig),
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.NotificationRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.NotificationRule, rule.Id, "Created", details: new { rule.Name, rule.Channel, rule.EntityType }, ct: ct);

        return new ApiResponse<NotificationRuleDto> { Data = MapToDto(rule) };
    }

    public async Task<ApiResponse<NotificationRuleDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _db.NotificationRules.FirstOrDefaultAsync(r => r.Id == id, ct);

        if (rule is null)
        {
            return new ApiResponse<NotificationRuleDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.NotificationRuleNotFound, $"Notification rule with id '{id}' not found.")
            };
        }

        return new ApiResponse<NotificationRuleDto> { Data = MapToDto(rule) };
    }

    public async Task<PagedApiResponse<NotificationRuleDto>> ListAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? entityType = null,
        string? channel = null,
        bool? isEnabled = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.NotificationRules.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(r => r.Name.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(r => r.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(r => r.Channel == channel);

        if (isEnabled.HasValue)
            query = query.Where(r => r.IsEnabled == isEnabled.Value);

        query = query.OrderBy(r => r.Name);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedApiResponse<NotificationRuleDto>
        {
            Data = items.Select(MapToDto).ToList(),
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    public async Task<ApiResponse<NotificationRuleDto>> UpdateAsync(Guid id, UpdateNotificationRuleRequest request, CancellationToken ct = default)
    {
        var rule = await _db.NotificationRules.FindAsync([id], ct);

        if (rule is null)
        {
            return new ApiResponse<NotificationRuleDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.NotificationRuleNotFound, $"Notification rule with id '{id}' not found.")
            };
        }

        var duplicateExists = await _db.NotificationRules
            .AnyAsync(r => r.Name.ToLower() == request.Name.ToLower() && r.Id != id, ct);

        if (duplicateExists)
        {
            return new ApiResponse<NotificationRuleDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.DuplicateNotificationRuleName, $"A notification rule with name '{request.Name}' already exists.")
            };
        }

        rule.Name = request.Name;
        rule.Description = request.Description;
        rule.EntityType = request.EntityType;
        rule.EntityId = request.EntityId;
        rule.EventTypes = JsonSerializer.Serialize(request.EventTypes);
        rule.Channel = request.Channel;
        rule.ChannelConfig = JsonSerializer.Serialize(request.ChannelConfig);
        rule.IsEnabled = request.IsEnabled;
        rule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.NotificationRule, rule.Id, "Updated", details: new { rule.Name, rule.Channel, rule.EntityType }, ct: ct);

        return new ApiResponse<NotificationRuleDto> { Data = MapToDto(rule) };
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _db.NotificationRules.FindAsync([id], ct);

        if (rule is null)
        {
            return new ApiResponse<object>
            {
                Error = ErrorMessages.Create(ErrorCodes.NotificationRuleNotFound, $"Notification rule with id '{id}' not found.")
            };
        }

        rule.IsDeleted = true;
        rule.DeletedAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.NotificationRule, rule.Id, "Deleted", details: new { rule.Name }, ct: ct);

        return new ApiResponse<object> { Data = new { } };
    }

    public async Task<ApiResponse<object>> TestAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _db.NotificationRules.FirstOrDefaultAsync(r => r.Id == id, ct);

        if (rule is null)
        {
            return new ApiResponse<object>
            {
                Error = ErrorMessages.Create(ErrorCodes.NotificationRuleNotFound, $"Notification rule with id '{id}' not found.")
            };
        }

        var testEvent = new NotificationEvent
        {
            EventType = "test",
            EntityType = rule.EntityType,
            EntityId = rule.EntityId ?? Guid.Empty,
            EntityName = "Test Notification",
            Context = new Dictionary<string, object>
            {
                ["message"] = "This is a test notification from Courier.",
                ["ruleName"] = rule.Name,
            },
        };

        try
        {
            await _dispatcher.DispatchAsync(testEvent, ct);
            return new ApiResponse<object> { Data = new { sent = true } };
        }
        catch (Exception ex)
        {
            return new ApiResponse<object>
            {
                Error = ErrorMessages.Create(ErrorCodes.NotificationTestFailed, $"Test notification failed: {ex.Message}")
            };
        }
    }

    private static NotificationRuleDto MapToDto(NotificationRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        Description = rule.Description,
        EntityType = rule.EntityType,
        EntityId = rule.EntityId,
        EventTypes = DeserializeEventTypes(rule.EventTypes),
        Channel = rule.Channel,
        ChannelConfig = DeserializeJsonObject(rule.ChannelConfig),
        IsEnabled = rule.IsEnabled,
        CreatedAt = rule.CreatedAt,
        UpdatedAt = rule.UpdatedAt,
    };

    private static List<string> DeserializeEventTypes(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private static object DeserializeJsonObject(string json)
    {
        try { return JsonSerializer.Deserialize<JsonElement>(json); }
        catch { return new { }; }
    }
}

using System.Text.Json;
using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.AuditLog;

public class AuditService
{
    private readonly CourierDbContext _db;

    private static readonly Dictionary<AuditableEntityType, string> EntityTypeMap = new()
    {
        [AuditableEntityType.Job] = "job",
        [AuditableEntityType.JobExecution] = "job_execution",
        [AuditableEntityType.StepExecution] = "step_execution",
        [AuditableEntityType.Connection] = "connection",
        [AuditableEntityType.PgpKey] = "pgp_key",
        [AuditableEntityType.SshKey] = "ssh_key",
        [AuditableEntityType.FileMonitor] = "file_monitor",
    };

    public AuditService(CourierDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        AuditableEntityType entityType,
        Guid entityId,
        string operation,
        string performedBy = "system",
        object? details = null,
        CancellationToken ct = default)
    {
        var entry = new AuditLogEntry
        {
            Id = Guid.CreateVersion7(),
            EntityType = EntityTypeMap[entityType],
            EntityId = entityId,
            Operation = operation,
            PerformedBy = performedBy,
            PerformedAt = DateTime.UtcNow,
            Details = details is not null
                ? JsonSerializer.Serialize(details)
                : "{}",
        };

        _db.AuditLogEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedApiResponse<AuditLogEntryDto>> ListAsync(
        AuditLogFilter? filter = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.AuditLogEntries.AsQueryable();

        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.EntityType))
                query = query.Where(e => e.EntityType == filter.EntityType);

            if (filter.EntityId.HasValue)
                query = query.Where(e => e.EntityId == filter.EntityId.Value);

            if (!string.IsNullOrWhiteSpace(filter.Operation))
                query = query.Where(e => e.Operation.ToLower().Contains(filter.Operation.ToLower()));

            if (!string.IsNullOrWhiteSpace(filter.PerformedBy))
                query = query.Where(e => e.PerformedBy.ToLower().Contains(filter.PerformedBy.ToLower()));

            if (filter.From.HasValue)
                query = query.Where(e => e.PerformedAt >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(e => e.PerformedAt <= filter.To.Value);
        }

        query = query.OrderByDescending(e => e.PerformedAt);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedApiResponse<AuditLogEntryDto>
        {
            Data = items.Select(MapToDto).ToList(),
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    public async Task<PagedApiResponse<AuditLogEntryDto>> ListByEntityAsync(
        string entityType,
        Guid entityId,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.AuditLogEntries
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .OrderByDescending(e => e.PerformedAt);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedApiResponse<AuditLogEntryDto>
        {
            Data = items.Select(MapToDto).ToList(),
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    private static AuditLogEntryDto MapToDto(AuditLogEntry e) => new()
    {
        Id = e.Id,
        EntityType = e.EntityType,
        EntityId = e.EntityId,
        Operation = e.Operation,
        PerformedBy = e.PerformedBy,
        PerformedAt = e.PerformedAt,
        Details = e.Details,
    };
}

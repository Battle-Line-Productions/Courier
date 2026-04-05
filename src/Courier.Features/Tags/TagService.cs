using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Tags;

public class TagService
{
    private readonly CourierDbContext _db;
    private readonly AuditService _audit;

    public TagService(CourierDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<TagDto>> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        var duplicateExists = await _db.Tags
            .AnyAsync(t => t.Name.ToLower() == request.Name.ToLower(), ct);

        if (duplicateExists)
        {
            return new ApiResponse<TagDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.DuplicateTagName, $"A tag with name '{request.Name}' already exists.")
            };
        }

        var tag = new Tag
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Color = request.Color,
            Category = request.Category,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Tags.Add(tag);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Tag, tag.Id, "Created", details: new { tag.Name }, ct: ct);

        return new ApiResponse<TagDto> { Data = MapToDto(tag) };
    }

    public async Task<ApiResponse<TagDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tag is null)
        {
            return new ApiResponse<TagDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Tag with id '{id}' not found.")
            };
        }

        return new ApiResponse<TagDto> { Data = MapToDto(tag) };
    }

    public async Task<PagedApiResponse<TagDto>> ListAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? category = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.Tags.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(t => t.Category == category);

        query = query.OrderBy(t => t.Name);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => MapToDto(t))
            .ToListAsync(ct);

        return new PagedApiResponse<TagDto>
        {
            Data = items,
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    public async Task<ApiResponse<TagDto>> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken ct = default)
    {
        var tag = await _db.Tags.FindAsync([id], ct);

        if (tag is null)
        {
            return new ApiResponse<TagDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Tag with id '{id}' not found.")
            };
        }

        var duplicateExists = await _db.Tags
            .AnyAsync(t => t.Name.ToLower() == request.Name.ToLower() && t.Id != id, ct);

        if (duplicateExists)
        {
            return new ApiResponse<TagDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.DuplicateTagName, $"A tag with name '{request.Name}' already exists.")
            };
        }

        tag.Name = request.Name;
        tag.Color = request.Color;
        tag.Category = request.Category;
        tag.Description = request.Description;
        tag.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Tag, id, "Updated", ct: ct);

        return new ApiResponse<TagDto> { Data = MapToDto(tag) };
    }

    public async Task<ApiResponse> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await _db.Tags.FindAsync([id], ct);

        if (tag is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Tag with id '{id}' not found.")
            };
        }

        tag.IsDeleted = true;
        tag.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Tag, id, "Deleted", ct: ct);

        return new ApiResponse();
    }

    public async Task<ApiResponse> AssignTagsAsync(BulkTagAssignmentRequest request, CancellationToken ct = default)
    {
        foreach (var assignment in request.Assignments)
        {
            var tagExists = await _db.Tags.AnyAsync(t => t.Id == assignment.TagId, ct);
            if (!tagExists)
            {
                return new ApiResponse
                {
                    Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Tag with id '{assignment.TagId}' not found.")
                };
            }

            var entityExists = await EntityExistsAsync(assignment.EntityType, assignment.EntityId, ct);
            if (!entityExists)
            {
                return new ApiResponse
                {
                    Error = ErrorMessages.Create(ErrorCodes.TagEntityNotFound, $"Entity '{assignment.EntityType}' with id '{assignment.EntityId}' not found.")
                };
            }

            var alreadyAssigned = await _db.EntityTags.AnyAsync(
                et => et.TagId == assignment.TagId && et.EntityType == assignment.EntityType && et.EntityId == assignment.EntityId, ct);

            if (!alreadyAssigned)
            {
                _db.EntityTags.Add(new EntityTag
                {
                    Id = Guid.CreateVersion7(),
                    TagId = assignment.TagId,
                    EntityType = assignment.EntityType,
                    EntityId = assignment.EntityId,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        foreach (var assignment in request.Assignments)
        {
            await _audit.LogAsync(AuditableEntityType.Tag, assignment.TagId, "TagAssigned",
                details: new { assignment.EntityType, assignment.EntityId }, ct: ct);
        }

        return new ApiResponse();
    }

    public async Task<ApiResponse> UnassignTagsAsync(BulkTagAssignmentRequest request, CancellationToken ct = default)
    {
        foreach (var assignment in request.Assignments)
        {
            var entityTag = await _db.EntityTags.FirstOrDefaultAsync(
                et => et.TagId == assignment.TagId && et.EntityType == assignment.EntityType && et.EntityId == assignment.EntityId, ct);

            if (entityTag is not null)
            {
                _db.EntityTags.Remove(entityTag);
            }
        }

        await _db.SaveChangesAsync(ct);

        foreach (var assignment in request.Assignments)
        {
            await _audit.LogAsync(AuditableEntityType.Tag, assignment.TagId, "TagUnassigned",
                details: new { assignment.EntityType, assignment.EntityId }, ct: ct);
        }

        return new ApiResponse();
    }

    public async Task<PagedApiResponse<TagEntityDto>> ListEntitiesByTagAsync(
        Guid tagId,
        string? entityType = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var tagExists = await _db.Tags.AnyAsync(t => t.Id == tagId, ct);
        if (!tagExists)
        {
            return new PagedApiResponse<TagEntityDto>
            {
                Data = [],
                Pagination = new PaginationMeta(1, pageSize, 0, 0),
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Tag with id '{tagId}' not found.")
            };
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.EntityTags.Where(et => et.TagId == tagId);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(et => et.EntityType == entityType);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(et => new TagEntityDto { EntityId = et.EntityId, EntityType = et.EntityType })
            .ToListAsync(ct);

        return new PagedApiResponse<TagEntityDto>
        {
            Data = items,
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    private async Task<bool> EntityExistsAsync(string entityType, Guid entityId, CancellationToken ct)
    {
        return entityType switch
        {
            "job" => await _db.Jobs.AnyAsync(e => e.Id == entityId, ct),
            "connection" => await _db.Connections.AnyAsync(e => e.Id == entityId, ct),
            "pgp_key" => await _db.PgpKeys.AnyAsync(e => e.Id == entityId, ct),
            "ssh_key" => await _db.SshKeys.AnyAsync(e => e.Id == entityId, ct),
            "file_monitor" => await _db.FileMonitors.AnyAsync(e => e.Id == entityId, ct),
            "job_chain" => await _db.JobChains.AnyAsync(e => e.Id == entityId, ct),
            _ => false
        };
    }

    private static TagDto MapToDto(Tag t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Color = t.Color,
        Category = t.Category,
        Description = t.Description,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
    };
}

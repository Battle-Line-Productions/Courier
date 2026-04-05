using System.Text.Json;
using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Features.Tags;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Jobs;

public class JobService
{
    private readonly CourierDbContext _db;
    private readonly AuditService _audit;

    public JobService(CourierDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<JobDto>> CreateAsync(CreateJobRequest request, CancellationToken ct = default)
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Job, job.Id, "Created", details: new { job.Name, job.Description }, ct: ct);

        return new ApiResponse<JobDto> { Data = MapToDto(job) };
    }

    public async Task<ApiResponse<JobDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id, ct);

        if (job is null)
        {
            return new ApiResponse<JobDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job with id '{id}' not found.")
            };
        }

        var dto = MapToDto(job);
        dto = dto with { Tags = await TagHelper.GetTagsForEntityAsync(_db, "job", id, ct) };
        return new ApiResponse<JobDto> { Data = dto };
    }

    public async Task<PagedApiResponse<JobDto>> ListAsync(int page = 1, int pageSize = 25, string? search = null, string? tag = null, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.Jobs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(j => j.Name.ToLower().Contains(term) || (j.Description != null && j.Description.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var tagName = tag.ToLower();
            query = query.Where(e => _db.EntityTags
                .Any(et => et.EntityType == "job"
                        && et.EntityId == e.Id
                        && et.Tag.Name.ToLower() == tagName
                        && !et.Tag.IsDeleted));
        }

        query = query.OrderByDescending(j => j.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => MapToDto(j))
            .ToListAsync(ct);

        var entityIds = items.Select(i => i.Id).ToList();
        var tagMap = await TagHelper.GetTagsForEntitiesAsync(_db, "job", entityIds, ct);
        items = items.Select(i => i with { Tags = tagMap.GetValueOrDefault(i.Id, []) }).ToList();

        return new PagedApiResponse<JobDto>
        {
            Data = items,
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    public async Task<ApiResponse<JobDto>> UpdateAsync(Guid id, string name, string? description, CancellationToken ct = default)
    {
        var job = await _db.Jobs.FindAsync([id], ct);

        if (job is null)
            return new ApiResponse<JobDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job with id '{id}' not found.")
            };

        job.Name = name;
        job.Description = description;
        job.CurrentVersion++;
        job.UpdatedAt = DateTime.UtcNow;

        // Create version snapshot
        var steps = await _db.JobSteps
            .Where(s => s.JobId == job.Id)
            .OrderBy(s => s.StepOrder)
            .ToListAsync(ct);

        var snapshot = new
        {
            name = job.Name,
            description = job.Description,
            failurePolicy = job.FailurePolicy,
            steps = steps.Select(s => new
            {
                name = s.Name,
                typeKey = s.TypeKey,
                stepOrder = s.StepOrder,
                configuration = s.Configuration,
                timeoutSeconds = s.TimeoutSeconds,
            }).ToList(),
        };

        var version = new JobVersion
        {
            Id = Guid.CreateVersion7(),
            JobId = job.Id,
            VersionNumber = job.CurrentVersion,
            ConfigSnapshot = JsonSerializer.Serialize(snapshot),
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow,
        };
        _db.JobVersions.Add(version);

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Job, id, "Updated", details: new { name, description, version = job.CurrentVersion }, ct: ct);

        return new ApiResponse<JobDto> { Data = MapToDto(job) };
    }

    public async Task<ApiResponse> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _db.Jobs.FindAsync([id], ct);

        if (job is null)
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job with id '{id}' not found.")
            };

        job.IsDeleted = true;
        job.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Job, id, "Deleted", ct: ct);

        return new ApiResponse();
    }

    public async Task<ApiResponse<List<JobVersionDto>>> GetVersionsAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job is null)
            return new ApiResponse<List<JobVersionDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job with id '{jobId}' not found.")
            };

        var versions = await _db.JobVersions
            .Where(v => v.JobId == jobId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => MapVersionToDto(v))
            .ToListAsync(ct);

        return new ApiResponse<List<JobVersionDto>> { Data = versions };
    }

    public async Task<ApiResponse<JobVersionDto>> GetVersionAsync(Guid jobId, int versionNumber, CancellationToken ct = default)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job is null)
            return new ApiResponse<JobVersionDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job with id '{jobId}' not found.")
            };

        var version = await _db.JobVersions
            .FirstOrDefaultAsync(v => v.JobId == jobId && v.VersionNumber == versionNumber, ct);

        if (version is null)
            return new ApiResponse<JobVersionDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.JobVersionNotFound, $"Version {versionNumber} not found for job '{jobId}'.")
            };

        return new ApiResponse<JobVersionDto> { Data = MapVersionToDto(version) };
    }

    private static JobDto MapToDto(Job job) => new()
    {
        Id = job.Id,
        Name = job.Name,
        Description = job.Description,
        CurrentVersion = job.CurrentVersion,
        IsEnabled = job.IsEnabled,
        CreatedAt = job.CreatedAt,
        UpdatedAt = job.UpdatedAt,
    };

    private static JobVersionDto MapVersionToDto(JobVersion v) => new()
    {
        Id = v.Id,
        JobId = v.JobId,
        VersionNumber = v.VersionNumber,
        ConfigSnapshot = v.ConfigSnapshot,
        CreatedBy = v.CreatedBy,
        CreatedAt = v.CreatedAt,
    };
}

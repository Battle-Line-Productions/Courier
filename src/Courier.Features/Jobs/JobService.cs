using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Jobs;

public class JobService
{
    private readonly CourierDbContext _db;

    public JobService(CourierDbContext db)
    {
        _db = db;
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

        return new ApiResponse<JobDto> { Data = MapToDto(job) };
    }

    public async Task<PagedApiResponse<JobDto>> ListAsync(int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.Jobs.OrderByDescending(j => j.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => MapToDto(j))
            .ToListAsync(ct);

        return new PagedApiResponse<JobDto>
        {
            Data = items,
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
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
}

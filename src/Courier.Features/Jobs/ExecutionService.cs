using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Jobs;

public class ExecutionService
{
    private readonly CourierDbContext _db;

    public ExecutionService(CourierDbContext db) => _db = db;

    public async Task<ApiResponse<JobExecutionDto>> TriggerAsync(Guid jobId, string triggeredBy, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job '{jobId}' not found.") };

        if (!job.IsEnabled)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.JobNotEnabled, $"Job '{jobId}' is disabled.") };

        var hasSteps = await _db.JobSteps.AnyAsync(s => s.JobId == jobId, ct);
        if (!hasSteps)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.JobHasNoSteps, $"Job '{jobId}' has no steps configured.") };

        var execution = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            JobVersionNumber = job.CurrentVersion,
            TriggeredBy = triggeredBy,
            State = JobExecutionState.Queued,
            QueuedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

        _db.JobExecutions.Add(execution);
        await _db.SaveChangesAsync(ct);

        return new ApiResponse<JobExecutionDto> { Data = MapToDto(execution) };
    }

    public async Task<ApiResponse<JobExecutionDto>> GetExecutionAsync(Guid executionId, CancellationToken ct)
    {
        var execution = await _db.JobExecutions.FirstOrDefaultAsync(e => e.Id == executionId, ct);
        if (execution is null)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.ExecutionNotFound, $"Execution '{executionId}' not found.") };

        return new ApiResponse<JobExecutionDto> { Data = MapToDto(execution) };
    }

    public async Task<PagedApiResponse<JobExecutionDto>> ListExecutionsAsync(Guid jobId, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.JobExecutions
            .Where(e => e.JobId == jobId)
            .OrderByDescending(e => e.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => MapToDto(e))
            .ToListAsync(ct);

        return new PagedApiResponse<JobExecutionDto>
        {
            Data = items,
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages),
        };
    }

    private static JobExecutionDto MapToDto(JobExecution e) => new()
    {
        Id = e.Id,
        JobId = e.JobId,
        State = e.State.ToString().ToLowerInvariant(),
        TriggeredBy = e.TriggeredBy,
        QueuedAt = e.QueuedAt,
        StartedAt = e.StartedAt,
        CompletedAt = e.CompletedAt,
        CreatedAt = e.CreatedAt,
    };
}

using Courier.Domain.Common;
using Courier.Domain.Engine;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Jobs;

public class ExecutionService
{
    private readonly CourierDbContext _db;
    private readonly AuditService _audit;

    public ExecutionService(CourierDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

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

        await _audit.LogAsync(AuditableEntityType.Job, jobId, "Triggered", triggeredBy, new { executionId = execution.Id }, ct);

        return new ApiResponse<JobExecutionDto> { Data = MapToDto(execution) };
    }

    public async Task<ApiResponse<JobExecutionDto>> GetExecutionAsync(Guid executionId, CancellationToken ct)
    {
        var execution = await _db.JobExecutions
            .Include(e => e.StepExecutions)
                .ThenInclude(se => se.JobStep)
            .FirstOrDefaultAsync(e => e.Id == executionId, ct);
        if (execution is null)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.ExecutionNotFound, $"Execution '{executionId}' not found.") };

        return new ApiResponse<JobExecutionDto> { Data = MapToDto(execution, includeSteps: true) };
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

    public async Task<ApiResponse<JobExecutionDto>> PauseExecutionAsync(Guid executionId, string pausedBy, CancellationToken ct)
    {
        var execution = await _db.JobExecutions.FirstOrDefaultAsync(e => e.Id == executionId, ct);
        if (execution is null)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.ExecutionNotFound, $"Execution '{executionId}' not found.") };

        if (execution.State != JobExecutionState.Running)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.ExecutionCannotBePaused, $"Execution is '{execution.State.ToString().ToLowerInvariant()}', only running executions can be paused.") };

        execution.RequestedState = "paused";
        execution.PausedBy = pausedBy;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.JobExecution, executionId, "PauseRequested", pausedBy, ct: ct);

        return new ApiResponse<JobExecutionDto> { Data = MapToDto(execution) };
    }

    public async Task<ApiResponse<JobExecutionDto>> ResumeExecutionAsync(Guid executionId, string resumedBy, CancellationToken ct)
    {
        var execution = await _db.JobExecutions.FirstOrDefaultAsync(e => e.Id == executionId, ct);
        if (execution is null)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.ExecutionNotFound, $"Execution '{executionId}' not found.") };

        if (execution.State != JobExecutionState.Paused)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.ExecutionCannotBeResumed, $"Execution is '{execution.State.ToString().ToLowerInvariant()}', only paused executions can be resumed.") };

        execution.State = JobStateMachine.Transition(JobExecutionState.Paused, JobExecutionState.Queued);
        execution.RequestedState = null;
        execution.PausedAt = null;
        execution.PausedBy = null;
        execution.QueuedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.JobExecution, executionId, "Resumed", resumedBy, ct: ct);

        return new ApiResponse<JobExecutionDto> { Data = MapToDto(execution) };
    }

    public async Task<ApiResponse<JobExecutionDto>> CancelExecutionAsync(Guid executionId, string cancelledBy, string? reason, CancellationToken ct)
    {
        var execution = await _db.JobExecutions.FirstOrDefaultAsync(e => e.Id == executionId, ct);
        if (execution is null)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.ExecutionNotFound, $"Execution '{executionId}' not found.") };

        if (!JobStateMachine.CanTransition(execution.State, JobExecutionState.Cancelled))
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.ExecutionCannotBeCancelled, $"Execution is '{execution.State.ToString().ToLowerInvariant()}', it cannot be cancelled.") };

        if (execution.State == JobExecutionState.Running)
        {
            if (execution.RequestedState == "cancelled")
            {
                // Previous cancel was never acknowledged by the engine (likely crashed).
                // Force-transition directly.
                execution.State = JobExecutionState.Cancelled;
                execution.CancelledAt = DateTime.UtcNow;
                execution.CompletedAt = DateTime.UtcNow;
                execution.RequestedState = null;
                execution.CancelledBy = cancelledBy;
                execution.CancelReason = reason;
                await _db.SaveChangesAsync(ct);

                await _audit.LogAsync(AuditableEntityType.JobExecution, executionId, "ForceCancelled", cancelledBy, new { reason }, ct);
            }
            else
            {
                // Signal the engine to cancel — it will acknowledge between steps
                execution.RequestedState = "cancelled";
                execution.CancelledBy = cancelledBy;
                execution.CancelReason = reason;
                await _db.SaveChangesAsync(ct);

                await _audit.LogAsync(AuditableEntityType.JobExecution, executionId, "CancelRequested", cancelledBy, new { reason }, ct);
            }
        }
        else
        {
            // Queued or Paused — transition directly
            execution.State = JobStateMachine.Transition(execution.State, JobExecutionState.Cancelled);
            execution.CancelledAt = DateTime.UtcNow;
            execution.CancelledBy = cancelledBy;
            execution.CancelReason = reason;
            execution.CompletedAt = DateTime.UtcNow;
            execution.RequestedState = null;
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync(AuditableEntityType.JobExecution, executionId, "Cancelled", cancelledBy, new { reason }, ct);
        }

        return new ApiResponse<JobExecutionDto> { Data = MapToDto(execution) };
    }

    private static JobExecutionDto MapToDto(JobExecution e, bool includeSteps = false) => new()
    {
        Id = e.Id,
        JobId = e.JobId,
        State = e.State.ToString().ToLowerInvariant(),
        TriggeredBy = e.TriggeredBy,
        QueuedAt = e.QueuedAt,
        StartedAt = e.StartedAt,
        CompletedAt = e.CompletedAt,
        PausedAt = e.PausedAt,
        PausedBy = e.PausedBy,
        CancelledAt = e.CancelledAt,
        CancelledBy = e.CancelledBy,
        CancelReason = e.CancelReason,
        CreatedAt = e.CreatedAt,
        StepExecutions = includeSteps && e.StepExecutions.Count > 0
            ? e.StepExecutions.OrderBy(se => se.StepOrder).Select(MapStepToDto).ToList()
            : null,
    };

    private static StepExecutionDto MapStepToDto(StepExecution se) => new()
    {
        Id = se.Id,
        StepOrder = se.StepOrder,
        StepName = se.JobStep?.Name ?? $"Step {se.StepOrder}",
        StepTypeKey = se.JobStep?.TypeKey ?? "unknown",
        State = se.State.ToString().ToLowerInvariant(),
        StartedAt = se.StartedAt,
        CompletedAt = se.CompletedAt,
        DurationMs = se.DurationMs,
        BytesProcessed = se.BytesProcessed,
        OutputData = se.OutputData,
        ErrorMessage = se.ErrorMessage,
        RetryAttempt = se.RetryAttempt,
    };
}

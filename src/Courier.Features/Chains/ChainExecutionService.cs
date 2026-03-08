using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Features.Events;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Chains;

public class ChainExecutionService
{
    private readonly CourierDbContext _db;
    private readonly AuditService _audit;
    private readonly DomainEventService _events;

    public ChainExecutionService(CourierDbContext db, AuditService audit, DomainEventService events)
    {
        _db = db;
        _audit = audit;
        _events = events;
    }

    public async Task<ApiResponse<ChainExecutionDto>> TriggerAsync(Guid chainId, string triggeredBy, CancellationToken ct = default)
    {
        var chain = await _db.JobChains
            .Include(c => c.Members.OrderBy(m => m.ExecutionOrder))
                .ThenInclude(m => m.Job)
            .FirstOrDefaultAsync(c => c.Id == chainId, ct);

        if (chain is null)
        {
            return new ApiResponse<ChainExecutionDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ChainNotFound, $"Chain with id '{chainId}' not found.")
            };
        }

        if (!chain.IsEnabled)
        {
            return new ApiResponse<ChainExecutionDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ChainNotEnabled, "Chain is not enabled.")
            };
        }

        if (chain.Members.Count == 0)
        {
            return new ApiResponse<ChainExecutionDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ChainHasNoMembers, "Chain has no members.")
            };
        }

        var chainExecution = new ChainExecution
        {
            Id = Guid.CreateVersion7(),
            ChainId = chainId,
            TriggeredBy = triggeredBy,
            State = ChainExecutionState.Running,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

        _db.ChainExecutions.Add(chainExecution);

        // Find entry-point members (no upstream dependency)
        var entryPoints = chain.Members.Where(m => m.DependsOnMemberId is null).ToList();

        foreach (var member in entryPoints)
        {
            var jobExecution = new JobExecution
            {
                Id = Guid.CreateVersion7(),
                JobId = member.JobId,
                JobVersionNumber = member.Job.CurrentVersion,
                TriggeredBy = $"chain:{chain.Name}",
                State = JobExecutionState.Queued,
                QueuedAt = DateTime.UtcNow,
                ChainExecutionId = chainExecution.Id,
                CreatedAt = DateTime.UtcNow,
            };
            _db.JobExecutions.Add(jobExecution);
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.ChainExecution, chainExecution.Id, "Triggered",
            details: new { ChainName = chain.Name, triggeredBy, EntryPoints = entryPoints.Count }, ct: ct);

        await _events.RecordAsync("ChainStarted", "chain_execution", chainExecution.Id, new { chainId, chainName = chain.Name, triggeredBy }, ct);

        return new ApiResponse<ChainExecutionDto> { Data = MapToDto(chainExecution, chain.Members) };
    }

    public async Task<ApiResponse<ChainExecutionDto>> GetExecutionAsync(Guid executionId, CancellationToken ct = default)
    {
        var execution = await _db.ChainExecutions
            .Include(ce => ce.JobExecutions)
                .ThenInclude(je => je.Job)
            .Include(ce => ce.Chain)
                .ThenInclude(c => c.Members)
            .FirstOrDefaultAsync(ce => ce.Id == executionId, ct);

        if (execution is null)
        {
            return new ApiResponse<ChainExecutionDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ChainExecutionNotFound, $"Chain execution with id '{executionId}' not found.")
            };
        }

        return new ApiResponse<ChainExecutionDto> { Data = MapToDto(execution, execution.Chain.Members) };
    }

    public async Task<PagedApiResponse<ChainExecutionDto>> ListExecutionsAsync(
        Guid chainId, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var chain = await _db.JobChains
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == chainId, ct);

        if (chain is null)
        {
            return new PagedApiResponse<ChainExecutionDto>
            {
                Data = [],
                Pagination = new PaginationMeta(page, pageSize, 0, 0)
            };
        }

        var query = _db.ChainExecutions
            .Where(ce => ce.ChainId == chainId)
            .Include(ce => ce.JobExecutions)
                .ThenInclude(je => je.Job)
            .OrderByDescending(ce => ce.CreatedAt);

        var totalCount = await _db.ChainExecutions.CountAsync(ce => ce.ChainId == chainId, ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedApiResponse<ChainExecutionDto>
        {
            Data = items.Select(e => MapToDto(e, chain.Members)).ToList(),
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    internal static ChainExecutionDto MapToDto(ChainExecution execution, List<JobChainMember> members)
    {
        return new ChainExecutionDto
        {
            Id = execution.Id,
            ChainId = execution.ChainId,
            State = execution.State.ToString().ToLowerInvariant(),
            TriggeredBy = execution.TriggeredBy,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            CreatedAt = execution.CreatedAt,
            JobExecutions = execution.JobExecutions
                .OrderBy(je => je.CreatedAt)
                .Select(je => new ChainJobExecutionDto
                {
                    Id = je.Id,
                    JobId = je.JobId,
                    JobName = je.Job?.Name ?? string.Empty,
                    State = je.State.ToString().ToLowerInvariant(),
                    StartedAt = je.StartedAt,
                    CompletedAt = je.CompletedAt,
                })
                .ToList(),
        };
    }
}

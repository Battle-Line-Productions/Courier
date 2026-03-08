using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.Events;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Courier.Features.Chains;

public class ChainOrchestrator
{
    private readonly CourierDbContext _db;
    private readonly ILogger<ChainOrchestrator> _logger;
    private readonly DomainEventService _events;

    public ChainOrchestrator(CourierDbContext db, ILogger<ChainOrchestrator> logger, DomainEventService events)
    {
        _db = db;
        _logger = logger;
        _events = events;
    }

    public async Task EvaluateChainProgressAsync(Guid jobExecutionId, CancellationToken ct = default)
    {
        var execution = await _db.JobExecutions
            .FirstOrDefaultAsync(e => e.Id == jobExecutionId, ct);

        if (execution?.ChainExecutionId is null)
            return;

        var chainExecution = await _db.ChainExecutions
            .Include(ce => ce.Chain)
                .ThenInclude(c => c.Members)
            .Include(ce => ce.JobExecutions)
            .FirstOrDefaultAsync(ce => ce.Id == execution.ChainExecutionId, ct);

        if (chainExecution is null)
            return;

        var members = chainExecution.Chain.Members;
        var jobExecutions = chainExecution.JobExecutions;

        // Find the member that just completed
        var completedMember = members.FirstOrDefault(m => m.JobId == execution.JobId);
        if (completedMember is null)
            return;

        var isSuccess = execution.State == JobExecutionState.Completed;
        var isFailed = execution.State is JobExecutionState.Failed or JobExecutionState.Cancelled;

        if (!isSuccess && !isFailed)
            return; // Job is still running

        _logger.LogInformation(
            "Chain orchestrator: Job {JobId} in chain execution {ChainExecutionId} finished with state {State}",
            execution.JobId, chainExecution.Id, execution.State);

        // Find downstream members that depend on the completed member
        var downstreamMembers = members.Where(m => m.DependsOnMemberId == completedMember.Id).ToList();

        foreach (var downstream in downstreamMembers)
        {
            // Check if already queued/running for this chain execution
            var alreadyExists = jobExecutions.Any(je => je.JobId == downstream.JobId);
            if (alreadyExists)
                continue;

            if (isSuccess || downstream.RunOnUpstreamFailure)
            {
                // Enqueue downstream job
                var job = await _db.Jobs.FindAsync([downstream.JobId], ct);
                if (job is null) continue;

                var newExecution = new JobExecution
                {
                    Id = Guid.CreateVersion7(),
                    JobId = downstream.JobId,
                    JobVersionNumber = job.CurrentVersion,
                    TriggeredBy = $"chain:{chainExecution.Chain.Name}",
                    State = JobExecutionState.Queued,
                    QueuedAt = DateTime.UtcNow,
                    ChainExecutionId = chainExecution.Id,
                    CreatedAt = DateTime.UtcNow,
                };

                _db.JobExecutions.Add(newExecution);
                _logger.LogInformation("Enqueued downstream job {JobId} for chain execution {ChainExecutionId}",
                    downstream.JobId, chainExecution.Id);
            }
            else
            {
                // Upstream failed and RunOnUpstreamFailure is false — skip downstream and its transitive dependents
                SkipTransitiveDownstream(downstream.Id, members, jobExecutions, chainExecution);
            }
        }

        // Check if all members are terminal
        await _db.SaveChangesAsync(ct);

        // Re-query to get updated job executions
        var allJobExecutions = await _db.JobExecutions
            .Where(je => je.ChainExecutionId == chainExecution.Id)
            .ToListAsync(ct);

        var membersWithExecutions = members
            .Select(m => allJobExecutions.FirstOrDefault(je => je.JobId == m.JobId))
            .ToList();

        var allAccountedFor = membersWithExecutions.All(je => je is not null);
        var allTerminal = allAccountedFor && membersWithExecutions
            .All(je => je!.State is JobExecutionState.Completed
                or JobExecutionState.Failed
                or JobExecutionState.Cancelled);

        if (allTerminal)
        {
            var anyFailed = membersWithExecutions.Any(je =>
                je!.State is JobExecutionState.Failed or JobExecutionState.Cancelled);

            chainExecution.State = anyFailed ? ChainExecutionState.Failed : ChainExecutionState.Completed;
            chainExecution.CompletedAt = DateTime.UtcNow;

            var chainEventType = anyFailed ? "ChainFailed" : "ChainCompleted";
            _events.Record(chainEventType, "chain_execution", chainExecution.Id, new { chainId = chainExecution.ChainId, state = chainExecution.State.ToString().ToLowerInvariant() });

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Chain execution {ChainExecutionId} completed with state {State}",
                chainExecution.Id, chainExecution.State);
        }
    }

    private void SkipTransitiveDownstream(
        Guid memberId,
        List<JobChainMember> members,
        List<JobExecution> existingExecutions,
        ChainExecution chainExecution)
    {
        // Find the member and create a cancelled execution for it
        var member = members.First(m => m.Id == memberId);
        var alreadyExists = existingExecutions.Any(je => je.JobId == member.JobId);
        if (!alreadyExists)
        {
            var skippedExecution = new JobExecution
            {
                Id = Guid.CreateVersion7(),
                JobId = member.JobId,
                JobVersionNumber = 1,
                TriggeredBy = $"chain:{chainExecution.Chain.Name}",
                State = JobExecutionState.Cancelled,
                CancelledAt = DateTime.UtcNow,
                CancelledBy = "chain_orchestrator",
                CancelReason = "Upstream dependency failed",
                ChainExecutionId = chainExecution.Id,
                CreatedAt = DateTime.UtcNow,
            };
            _db.JobExecutions.Add(skippedExecution);

            _logger.LogInformation("Skipped downstream job {JobId} due to upstream failure in chain {ChainExecutionId}",
                member.JobId, chainExecution.Id);
        }

        // Recursively skip transitive dependents
        var transitiveDownstream = members.Where(m => m.DependsOnMemberId == memberId).ToList();
        foreach (var transitive in transitiveDownstream)
        {
            SkipTransitiveDownstream(transitive.Id, members, existingExecutions, chainExecution);
        }
    }
}

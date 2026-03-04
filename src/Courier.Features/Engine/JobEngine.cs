using System.Diagnostics;
using System.Text.Json;
using Courier.Domain.Engine;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Features.Engine.Protocols;
using Courier.Features.Notifications;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Courier.Features.Engine;

public class JobEngine
{
    private readonly CourierDbContext _db;
    private readonly StepTypeRegistry _registry;
    private readonly JobConnectionRegistry _connectionRegistry;
    private readonly ILogger<JobEngine> _logger;
    private readonly AuditService _audit;
    private readonly NotificationDispatcher _dispatcher;

    public JobEngine(CourierDbContext db, StepTypeRegistry registry, JobConnectionRegistry connectionRegistry, ILogger<JobEngine> logger, AuditService audit, NotificationDispatcher dispatcher)
    {
        _db = db;
        _registry = registry;
        _connectionRegistry = connectionRegistry;
        _logger = logger;
        _audit = audit;
        _dispatcher = dispatcher;
    }

    public async Task ExecuteAsync(Guid executionId, CancellationToken cancellationToken)
    {
        var execution = await _db.JobExecutions
            .Include(e => e.Job)
            .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);

        if (execution is null)
        {
            _logger.LogError("JobExecution {ExecutionId} not found", executionId);
            return;
        }

        var steps = await _db.JobSteps
            .Where(s => s.JobId == execution.JobId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync(cancellationToken);

        if (steps.Count == 0)
        {
            _logger.LogWarning("Job {JobId} has no steps. Marking execution as completed.", execution.JobId);
            execution.State = JobExecutionState.Completed;
            execution.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var failurePolicy = ParseFailurePolicy(execution.Job.FailurePolicy);
        var context = new JobContext();
        var allSucceeded = true;

        // Load existing step executions for resume detection
        var existingStepExecutions = await _db.StepExecutions
            .Where(se => se.JobExecutionId == execution.Id)
            .ToListAsync(cancellationToken);

        // Determine starting step for resumed executions
        var isResumed = existingStepExecutions.Any(s => s.State == StepExecutionState.Completed);
        var stepsToExecute = steps;

        if (isResumed)
        {
            var lastCompletedOrder = existingStepExecutions
                .Where(s => s.State == StepExecutionState.Completed)
                .Max(s => s.StepOrder);

            stepsToExecute = steps.Where(s => s.StepOrder > lastCompletedOrder).ToList();

            // Restore context from snapshot
            if (!string.IsNullOrEmpty(execution.ContextSnapshot) && execution.ContextSnapshot != "{}")
            {
                var savedContext = JsonSerializer.Deserialize<Dictionary<string, object>>(execution.ContextSnapshot);
                if (savedContext != null)
                    context = JobContext.Restore(savedContext);
            }

            _logger.LogInformation("Resuming execution {ExecutionId} from step order {StartFrom}", executionId, lastCompletedOrder + 1);
        }

        await _audit.LogAsync(AuditableEntityType.JobExecution, executionId, isResumed ? "ExecutionResumed" : "ExecutionStarted", details: new { jobId = execution.JobId }, ct: cancellationToken);

        try
        {
            foreach (var step in stepsToExecute)
            {
                // Check for control signals before each step
                var signal = await CheckControlSignalAsync(execution.Id, cancellationToken);

                if (signal == "paused")
                {
                    execution.State = JobExecutionState.Paused;
                    execution.PausedAt = DateTime.UtcNow;
                    execution.RequestedState = null;
                    execution.ContextSnapshot = JsonSerializer.Serialize(context.Snapshot());
                    await _db.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Execution {ExecutionId} paused before step {StepName}", executionId, step.Name);
                    await _audit.LogAsync(AuditableEntityType.JobExecution, executionId, "ExecutionPaused", details: new { beforeStep = step.Name }, ct: cancellationToken);
                    return;
                }

                if (signal == "cancelled")
                {
                    execution.State = JobExecutionState.Cancelled;
                    execution.CancelledAt = DateTime.UtcNow;
                    execution.CompletedAt = DateTime.UtcNow;
                    execution.RequestedState = null;
                    await _db.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Execution {ExecutionId} cancelled before step {StepName}", executionId, step.Name);
                    await _audit.LogAsync(AuditableEntityType.JobExecution, executionId, "ExecutionCancelled", details: new { beforeStep = step.Name }, ct: cancellationToken);
                    await DispatchJobNotificationAsync(execution, "job_cancelled", cancellationToken);
                    return;
                }

                var stepExecution = new StepExecution
                {
                    Id = Guid.NewGuid(),
                    JobExecutionId = execution.Id,
                    JobStepId = step.Id,
                    StepOrder = step.StepOrder,
                    State = StepExecutionState.Running,
                    StartedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                };
                _db.StepExecutions.Add(stepExecution);
                await _db.SaveChangesAsync(cancellationToken);

                var sw = Stopwatch.StartNew();
                StepResult result;

                try
                {
                    var handler = _registry.Resolve(step.TypeKey);
                    var config = new StepConfiguration(step.Configuration);

                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(step.TimeoutSeconds));

                    result = await handler.ExecuteAsync(config, context, timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    result = StepResult.Fail($"Step '{step.Name}' timed out after {step.TimeoutSeconds}s");
                }
                catch (Exception ex)
                {
                    result = StepResult.Fail(ex.Message, ex.StackTrace);
                }

                sw.Stop();
                stepExecution.DurationMs = sw.ElapsedMilliseconds;

                if (result.Success)
                {
                    stepExecution.State = StepExecutionState.Completed;
                    stepExecution.CompletedAt = DateTime.UtcNow;
                    stepExecution.BytesProcessed = result.BytesProcessed;

                    if (result.Outputs is not null)
                    {
                        stepExecution.OutputData = JsonSerializer.Serialize(result.Outputs);
                        foreach (var kvp in result.Outputs)
                            context.Set($"{step.StepOrder}.{kvp.Key}", kvp.Value);
                    }

                    _logger.LogInformation("Step {StepName} completed in {DurationMs}ms", step.Name, sw.ElapsedMilliseconds);

                    await _audit.LogAsync(AuditableEntityType.StepExecution, stepExecution.Id, "StepCompleted", details: new { stepType = step.TypeKey, durationMs = sw.ElapsedMilliseconds }, ct: cancellationToken);
                }
                else
                {
                    stepExecution.State = StepExecutionState.Failed;
                    stepExecution.CompletedAt = DateTime.UtcNow;
                    stepExecution.ErrorMessage = result.ErrorMessage;
                    stepExecution.ErrorStackTrace = result.ErrorStackTrace;

                    _logger.LogWarning("Step {StepName} failed: {Error}", step.Name, result.ErrorMessage);

                    await _audit.LogAsync(AuditableEntityType.StepExecution, stepExecution.Id, "StepFailed", details: new { error = result.ErrorMessage, stepType = step.TypeKey }, ct: cancellationToken);

                    if (failurePolicy.Type != FailurePolicyType.SkipAndContinue)
                        await DispatchJobNotificationAsync(execution, "step_failed", cancellationToken, new Dictionary<string, object> { ["stepName"] = step.Name, ["error"] = result.ErrorMessage ?? "" });

                    if (failurePolicy.Type == FailurePolicyType.SkipAndContinue)
                    {
                        _logger.LogInformation("Failure policy is SkipAndContinue. Continuing to next step.");
                        await _db.SaveChangesAsync(cancellationToken);
                        continue;
                    }

                    allSucceeded = false;
                    await _db.SaveChangesAsync(cancellationToken);
                    break;
                }

                execution.ContextSnapshot = JsonSerializer.Serialize(context.Snapshot());
                await _db.SaveChangesAsync(cancellationToken);
            }

            execution.CompletedAt = DateTime.UtcNow;
            execution.State = allSucceeded ? JobExecutionState.Completed : JobExecutionState.Failed;
            await _db.SaveChangesAsync(cancellationToken);

            var executionOp = allSucceeded ? "ExecutionCompleted" : "ExecutionFailed";
            await _audit.LogAsync(AuditableEntityType.JobExecution, executionId, executionOp, details: new { jobId = execution.JobId, state = execution.State.ToString().ToLowerInvariant() }, ct: cancellationToken);

            var notifEventType = allSucceeded ? "job_completed" : "job_failed";
            await DispatchJobNotificationAsync(execution, notifEventType, cancellationToken);
        }
        finally
        {
            await _connectionRegistry.DisposeAsync();
        }

        _logger.LogInformation("JobExecution {ExecutionId} finished with state {State}", executionId, execution.State);
    }

    private async Task DispatchJobNotificationAsync(JobExecution execution, string eventType, CancellationToken ct, Dictionary<string, object>? extraContext = null)
    {
        try
        {
            var context = new Dictionary<string, object>
            {
                ["executionId"] = execution.Id,
                ["state"] = execution.State.ToString().ToLowerInvariant(),
            };

            if (extraContext is not null)
            {
                foreach (var kvp in extraContext)
                    context[kvp.Key] = kvp.Value;
            }

            var notificationEvent = new NotificationEvent
            {
                EventType = eventType,
                EntityType = "job",
                EntityId = execution.JobId,
                EntityName = execution.Job?.Name,
                Context = context,
            };

            await _dispatcher.DispatchAsync(notificationEvent, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch notification for execution {ExecutionId}", execution.Id);
        }
    }

    private async Task<string?> CheckControlSignalAsync(Guid executionId, CancellationToken ct)
    {
        // Read fresh from DB to pick up API-written signals
        var requestedState = await _db.JobExecutions
            .Where(e => e.Id == executionId)
            .Select(e => e.RequestedState)
            .FirstOrDefaultAsync(ct);

        return requestedState;
    }

    private static FailurePolicy ParseFailurePolicy(string json)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<FailurePolicy>(json, options) ?? new FailurePolicy();
        }
        catch
        {
            return new FailurePolicy();
        }
    }
}

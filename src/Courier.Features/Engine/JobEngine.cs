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

    private enum FlowSignal { Continue, Abort, Halt }

    private sealed class ExecutionRun
    {
        public required JobExecution Execution { get; init; }
        public required List<JobStep> Steps { get; init; }
        public required JobContext Context { get; init; }
        public required FailurePolicy FailurePolicy { get; init; }
        public required HashSet<int> CompletedStepOrders { get; init; }
        public bool AllSucceeded { get; set; } = true;
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

        // Load existing step executions for resume detection
        var existingStepExecutions = await _db.StepExecutions
            .Where(se => se.JobExecutionId == execution.Id)
            .ToListAsync(cancellationToken);

        var isResumed = existingStepExecutions.Any(s => s.State == StepExecutionState.Completed);

        // Completed root-level step orders (non-iteration steps only)
        var completedStepOrders = existingStepExecutions
            .Where(s => s.State == StepExecutionState.Completed && s.IterationIndex == null)
            .Select(s => s.StepOrder)
            .ToHashSet();

        if (isResumed)
        {
            // Restore context from snapshot
            if (!string.IsNullOrEmpty(execution.ContextSnapshot) && execution.ContextSnapshot != "{}")
            {
                var savedContext = JsonSerializer.Deserialize<Dictionary<string, object>>(execution.ContextSnapshot);
                if (savedContext != null)
                    context = JobContext.Restore(savedContext);
            }

            _logger.LogInformation("Resuming execution {ExecutionId} with {CompletedCount} completed steps",
                executionId, completedStepOrders.Count);
        }

        // Parse steps into execution tree
        List<ExecutionNode> executionPlan;
        try
        {
            executionPlan = ExecutionPlanParser.Parse(steps);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to parse execution plan for job {JobId}", execution.JobId);
            execution.State = JobExecutionState.Failed;
            execution.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var run = new ExecutionRun
        {
            Execution = execution,
            Steps = steps,
            Context = context,
            FailurePolicy = failurePolicy,
            CompletedStepOrders = completedStepOrders,
        };

        await _audit.LogAsync(AuditableEntityType.JobExecution, executionId,
            isResumed ? "ExecutionResumed" : "ExecutionStarted",
            details: new { jobId = execution.JobId }, ct: cancellationToken);

        try
        {
            var signal = await ExecuteNodesAsync(executionPlan, run, skipCompleted: isResumed, cancellationToken);

            if (signal != FlowSignal.Halt)
            {
                execution.CompletedAt = DateTime.UtcNow;
                execution.State = run.AllSucceeded ? JobExecutionState.Completed : JobExecutionState.Failed;
                await _db.SaveChangesAsync(cancellationToken);

                var executionOp = run.AllSucceeded ? "ExecutionCompleted" : "ExecutionFailed";
                await _audit.LogAsync(AuditableEntityType.JobExecution, executionId, executionOp,
                    details: new { jobId = execution.JobId, state = execution.State.ToString().ToLowerInvariant() },
                    ct: cancellationToken);

                var notifEventType = run.AllSucceeded ? "job_completed" : "job_failed";
                await DispatchJobNotificationAsync(execution, notifEventType, cancellationToken);
            }
        }
        finally
        {
            await _connectionRegistry.DisposeAsync();
        }

        _logger.LogInformation("JobExecution {ExecutionId} finished with state {State}", executionId, execution.State);
    }

    private async Task<FlowSignal> ExecuteNodesAsync(
        List<ExecutionNode> nodes, ExecutionRun run, bool skipCompleted, CancellationToken ct)
    {
        var skipping = skipCompleted;

        foreach (var node in nodes)
        {
            // When resuming, skip contiguous prefix of completed root-level steps
            if (skipping && node is StepNode sn &&
                run.CompletedStepOrders.Contains(run.Steps[sn.StepIndex].StepOrder))
            {
                _logger.LogInformation("Skipping completed step '{StepName}' (order {Order})",
                    run.Steps[sn.StepIndex].Name, run.Steps[sn.StepIndex].StepOrder);
                continue;
            }

            skipping = false;

            var signal = node switch
            {
                StepNode stepNode => await ExecuteStepAsync(stepNode, run, ct),
                ForEachNode foreachNode => await ExecuteForEachAsync(foreachNode, run, ct),
                IfElseNode ifElseNode => await ExecuteIfElseAsync(ifElseNode, run, ct),
                _ => throw new InvalidOperationException($"Unknown execution node type: {node.GetType().Name}")
            };

            if (signal != FlowSignal.Continue)
                return signal;
        }

        return FlowSignal.Continue;
    }

    private async Task<FlowSignal> ExecuteStepAsync(StepNode node, ExecutionRun run, CancellationToken ct)
    {
        var step = run.Steps[node.StepIndex];

        // Check for control signals before each step
        var signal = await CheckControlSignalAsync(run.Execution.Id, ct);

        if (signal == "paused")
        {
            run.Execution.State = JobExecutionState.Paused;
            run.Execution.PausedAt = DateTime.UtcNow;
            run.Execution.RequestedState = null;
            run.Execution.ContextSnapshot = JsonSerializer.Serialize(run.Context.Snapshot());
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Execution {ExecutionId} paused before step {StepName}", run.Execution.Id, step.Name);
            await _audit.LogAsync(AuditableEntityType.JobExecution, run.Execution.Id, "ExecutionPaused",
                details: new { beforeStep = step.Name }, ct: ct);
            return FlowSignal.Halt;
        }

        if (signal == "cancelled")
        {
            run.Execution.State = JobExecutionState.Cancelled;
            run.Execution.CancelledAt = DateTime.UtcNow;
            run.Execution.CompletedAt = DateTime.UtcNow;
            run.Execution.RequestedState = null;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Execution {ExecutionId} cancelled before step {StepName}", run.Execution.Id, step.Name);
            await _audit.LogAsync(AuditableEntityType.JobExecution, run.Execution.Id, "ExecutionCancelled",
                details: new { beforeStep = step.Name }, ct: ct);
            await DispatchJobNotificationAsync(run.Execution, "job_cancelled", ct);
            return FlowSignal.Halt;
        }

        var stepExecution = new StepExecution
        {
            Id = Guid.NewGuid(),
            JobExecutionId = run.Execution.Id,
            JobStepId = step.Id,
            StepOrder = step.StepOrder,
            State = StepExecutionState.Running,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IterationIndex = run.Context.CurrentIterationIndex,
        };
        _db.StepExecutions.Add(stepExecution);
        await _db.SaveChangesAsync(ct);

        var sw = Stopwatch.StartNew();
        StepResult result;

        try
        {
            var handler = _registry.Resolve(step.TypeKey);
            var config = new StepConfiguration(step.Configuration);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(step.TimeoutSeconds));

            result = await handler.ExecuteAsync(config, run.Context, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
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
                    run.Context.Set($"{step.StepOrder}.{kvp.Key}", kvp.Value);
            }

            _logger.LogInformation("Step {StepName} completed in {DurationMs}ms", step.Name, sw.ElapsedMilliseconds);

            await _audit.LogAsync(AuditableEntityType.StepExecution, stepExecution.Id, "StepCompleted",
                details: new { stepType = step.TypeKey, durationMs = sw.ElapsedMilliseconds }, ct: ct);

            run.Execution.ContextSnapshot = JsonSerializer.Serialize(run.Context.Snapshot());
            await _db.SaveChangesAsync(ct);
            return FlowSignal.Continue;
        }

        // Step failed
        stepExecution.State = StepExecutionState.Failed;
        stepExecution.CompletedAt = DateTime.UtcNow;
        stepExecution.ErrorMessage = result.ErrorMessage;
        stepExecution.ErrorStackTrace = result.ErrorStackTrace;

        _logger.LogWarning("Step {StepName} failed: {Error}", step.Name, result.ErrorMessage);

        await _audit.LogAsync(AuditableEntityType.StepExecution, stepExecution.Id, "StepFailed",
            details: new { error = result.ErrorMessage, stepType = step.TypeKey }, ct: ct);

        if (run.FailurePolicy.Type != FailurePolicyType.SkipAndContinue)
            await DispatchJobNotificationAsync(run.Execution, "step_failed", ct,
                new Dictionary<string, object> { ["stepName"] = step.Name, ["error"] = result.ErrorMessage ?? "" });

        if (run.FailurePolicy.Type == FailurePolicyType.SkipAndContinue)
        {
            _logger.LogInformation("Failure policy is SkipAndContinue. Continuing to next step.");
            await _db.SaveChangesAsync(ct);
            return FlowSignal.Continue;
        }

        run.AllSucceeded = false;
        await _db.SaveChangesAsync(ct);
        return FlowSignal.Abort;
    }

    private async Task<FlowSignal> ExecuteForEachAsync(ForEachNode node, ExecutionRun run, CancellationToken ct)
    {
        var step = run.Steps[node.StepIndex];
        var config = new StepConfiguration(step.Configuration);

        List<JsonElement> items;
        try
        {
            items = ResolveForEachSource(config, run.Context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ForEach step '{StepName}' failed to resolve source", step.Name);
            run.AllSucceeded = false;
            return FlowSignal.Abort;
        }

        if (items.Count == 0)
        {
            _logger.LogInformation("ForEach step '{StepName}' has empty collection, skipping body", step.Name);
            return FlowSignal.Continue;
        }

        _logger.LogInformation("ForEach step '{StepName}' iterating over {Count} items", step.Name, items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            run.Context.PushLoopScope(items[i], i, items.Count);

            try
            {
                var signal = await ExecuteNodesAsync(node.Body, run, skipCompleted: false, ct);
                if (signal != FlowSignal.Continue)
                    return signal;
            }
            finally
            {
                run.Context.PopLoopScope();
            }
        }

        _logger.LogInformation("ForEach step '{StepName}' completed {Count} iterations", step.Name, items.Count);
        return FlowSignal.Continue;
    }

    private async Task<FlowSignal> ExecuteIfElseAsync(IfElseNode node, ExecutionRun run, CancellationToken ct)
    {
        var step = run.Steps[node.StepIndex];
        var config = new StepConfiguration(step.Configuration);

        var left = ContextResolver.Resolve(config.GetString("left"), run.Context);
        var op = config.GetString("operator");
        var right = config.Has("right")
            ? ContextResolver.Resolve(config.GetString("right"), run.Context)
            : null;

        var conditionResult = ConditionEvaluator.Evaluate(left, op, right);

        _logger.LogInformation("Condition '{Left}' {Operator} '{Right}' evaluated to {Result}",
            left, op, right ?? "(none)", conditionResult);

        var branch = conditionResult ? node.ThenBranch : node.ElseBranch;
        if (branch is null || branch.Count == 0)
            return FlowSignal.Continue;

        return await ExecuteNodesAsync(branch, run, skipCompleted: false, ct);
    }

    private static List<JsonElement> ResolveForEachSource(StepConfiguration config, JobContext context)
    {
        var sourceRef = config.GetString("source");

        if (sourceRef.StartsWith("context:"))
        {
            var key = sourceRef["context:".Length..];

            // Try as JsonElement array
            if (context.TryGet<JsonElement>(key, out var jsonEl) && jsonEl.ValueKind == JsonValueKind.Array)
                return [.. jsonEl.EnumerateArray()];

            // Try as string containing JSON array
            if (context.TryGet<string>(key, out var strVal) && strVal is not null)
            {
                using var doc = JsonDocument.Parse(strVal);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    return [.. doc.RootElement.Clone().EnumerateArray()];
            }

            // Try as object and serialize to JSON
            if (context.TryGet<object>(key, out var objVal) && objVal is not null)
            {
                if (objVal is JsonElement objJsonEl && objJsonEl.ValueKind == JsonValueKind.Array)
                    return [.. objJsonEl.EnumerateArray()];

                var json = JsonSerializer.Serialize(objVal);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    return [.. doc.RootElement.Clone().EnumerateArray()];
            }

            throw new InvalidOperationException($"ForEach source context reference '{key}' not found or not an array");
        }

        // Literal JSON array
        using var literal = JsonDocument.Parse(sourceRef);
        if (literal.RootElement.ValueKind == JsonValueKind.Array)
            return [.. literal.RootElement.Clone().EnumerateArray()];

        throw new InvalidOperationException("ForEach source must be a JSON array");
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

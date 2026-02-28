using System.Diagnostics;
using System.Text.Json;
using Courier.Domain.Engine;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.Engine.Protocols;
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

    public JobEngine(CourierDbContext db, StepTypeRegistry registry, JobConnectionRegistry connectionRegistry, ILogger<JobEngine> logger)
    {
        _db = db;
        _registry = registry;
        _connectionRegistry = connectionRegistry;
        _logger = logger;
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

        try
        {
            foreach (var step in steps)
            {
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
                }
                else
                {
                    stepExecution.State = StepExecutionState.Failed;
                    stepExecution.CompletedAt = DateTime.UtcNow;
                    stepExecution.ErrorMessage = result.ErrorMessage;
                    stepExecution.ErrorStackTrace = result.ErrorStackTrace;

                    _logger.LogWarning("Step {StepName} failed: {Error}", step.Name, result.ErrorMessage);

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
        }
        finally
        {
            await _connectionRegistry.DisposeAsync();
        }

        _logger.LogInformation("JobExecution {ExecutionId} finished with state {State}", executionId, execution.State);
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

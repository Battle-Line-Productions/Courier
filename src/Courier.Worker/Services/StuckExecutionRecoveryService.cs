using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Worker.Services;

public class StuckExecutionRecoveryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StuckExecutionRecoveryService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _stuckThreshold = TimeSpan.FromHours(2);

    public StuckExecutionRecoveryService(IServiceScopeFactory scopeFactory, ILogger<StuckExecutionRecoveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StuckExecutionRecoveryService started");

        // Run immediately on startup
        await RecoverStuckExecutionsAsync(stoppingToken);

        // Then run periodically
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_checkInterval, stoppingToken);

            try
            {
                await RecoverStuckExecutionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stuck execution recovery");
            }
        }
    }

    private async Task RecoverStuckExecutionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourierDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<AuditService>();

        var threshold = DateTime.UtcNow - _stuckThreshold;

        // Find executions stuck in Running state
        var stuckExecutions = await db.JobExecutions
            .Where(e => e.State == JobExecutionState.Running && e.StartedAt < threshold)
            .ToListAsync(ct);

        if (stuckExecutions.Count == 0)
            return;

        _logger.LogWarning("Found {Count} stuck execution(s) to recover", stuckExecutions.Count);

        foreach (var execution in stuckExecutions)
        {
            execution.State = JobExecutionState.Failed;
            execution.CompletedAt = DateTime.UtcNow;

            _logger.LogWarning(
                "Recovered stuck execution {ExecutionId} for job {JobId} — started at {StartedAt}",
                execution.Id, execution.JobId, execution.StartedAt);

            await audit.LogAsync(AuditableEntityType.JobExecution, execution.Id, "ExecutionRecovered",
                details: new { reason = "Worker crashed during execution", startedAt = execution.StartedAt }, ct: ct);

            // Also fail any running step executions
            var runningSteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == execution.Id && se.State == StepExecutionState.Running)
                .ToListAsync(ct);

            foreach (var step in runningSteps)
            {
                step.State = StepExecutionState.Failed;
                step.CompletedAt = DateTime.UtcNow;
                step.ErrorMessage = "Worker crashed during execution";
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Recovered {Count} stuck execution(s)", stuckExecutions.Count);
    }
}

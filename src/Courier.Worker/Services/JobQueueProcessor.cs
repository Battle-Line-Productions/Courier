using Courier.Domain.Enums;
using Courier.Features.Engine;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Courier.Worker.Services;

public class JobQueueProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobQueueProcessor> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public JobQueueProcessor(IServiceScopeFactory scopeFactory, ILogger<JobQueueProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobQueueProcessor started. Polling every {Interval}s.", _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in queue processor loop");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("JobQueueProcessor stopping.");
    }

    private async Task ProcessNextAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourierDbContext>();

        var execution = await db.JobExecutions
            .Where(e => e.State == JobExecutionState.Queued)
            .OrderBy(e => e.QueuedAt)
            .FirstOrDefaultAsync(ct);

        if (execution is null)
            return;

        _logger.LogInformation("Dequeued execution {ExecutionId} for job {JobId}", execution.Id, execution.JobId);

        execution.State = JobExecutionState.Running;
        execution.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var engine = scope.ServiceProvider.GetRequiredService<JobEngine>();
        await engine.ExecuteAsync(execution.Id, ct);
    }
}

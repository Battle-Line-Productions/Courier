using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Courier.Features.Chains;

[DisallowConcurrentExecution]
public class QuartzChainAdapter : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuartzChainAdapter> _logger;

    public QuartzChainAdapter(IServiceScopeFactory scopeFactory, ILogger<QuartzChainAdapter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var chainIdStr = context.MergedJobDataMap.GetString("chainId");
        if (!Guid.TryParse(chainIdStr, out var chainId))
        {
            _logger.LogWarning("QuartzChainAdapter: invalid or missing chainId in data map");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourierDbContext>();
        var executionService = scope.ServiceProvider.GetRequiredService<ChainExecutionService>();

        var chain = await db.JobChains
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == chainId);

        if (chain is null)
        {
            _logger.LogWarning("QuartzChainAdapter: chain {ChainId} not found, skipping", chainId);
            return;
        }

        if (!chain.IsEnabled)
        {
            _logger.LogInformation("QuartzChainAdapter: chain {ChainId} is disabled, skipping", chainId);
            return;
        }

        if (chain.Members.Count == 0)
        {
            _logger.LogWarning("QuartzChainAdapter: chain {ChainId} has no members, skipping", chainId);
            return;
        }

        // Update schedule metadata
        var scheduleId = Guid.Parse(context.JobDetail.Key.Name);
        var schedule = await db.ChainSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId);
        if (schedule is not null)
        {
            schedule.LastFiredAt = DateTimeOffset.UtcNow;
            schedule.NextFireAt = context.NextFireTimeUtc;

            if (schedule.ScheduleType == "one_shot")
                schedule.IsEnabled = false;

            await db.SaveChangesAsync();
        }

        var result = await executionService.TriggerAsync(chainId, "schedule");

        if (result.Success)
        {
            _logger.LogInformation(
                "QuartzChainAdapter: created chain execution {ExecutionId} for chain {ChainId} (triggered by schedule {ScheduleId})",
                result.Data!.Id, chainId, scheduleId);
        }
        else
        {
            _logger.LogWarning(
                "QuartzChainAdapter: failed to trigger chain {ChainId}: {Error}",
                chainId, result.Error?.Message);
        }
    }
}

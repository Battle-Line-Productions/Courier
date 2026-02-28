using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Courier.Worker.Services;

public class ScheduleStartupSync : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduleStartupSync> _logger;

    public ScheduleStartupSync(IServiceScopeFactory scopeFactory, ILogger<ScheduleStartupSync> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourierDbContext>();
        var scheduleService = scope.ServiceProvider.GetRequiredService<JobScheduleService>();

        var enabledSchedules = await db.JobSchedules
            .Where(s => s.IsEnabled)
            .ToListAsync(cancellationToken);

        var synced = 0;
        foreach (var schedule in enabledSchedules)
        {
            try
            {
                await scheduleService.UnregisterFromQuartzAsync(schedule.Id);
                await scheduleService.RegisterWithQuartzAsync(schedule);
                synced++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync schedule {ScheduleId} for job {JobId}", schedule.Id, schedule.JobId);
            }
        }

        _logger.LogInformation("ScheduleStartupSync: synced {Count}/{Total} enabled schedules", synced, enabledSchedules.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

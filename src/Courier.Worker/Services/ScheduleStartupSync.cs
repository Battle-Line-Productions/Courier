using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Courier.Worker.Services;

public class ScheduleStartupSync : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduleStartupSync> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromSeconds(30);

    public ScheduleStartupSync(IServiceScopeFactory scopeFactory, ILogger<ScheduleStartupSync> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial sync on startup
        await SyncAsync(stoppingToken);

        // Periodic re-sync to pick up API-created schedules
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_syncInterval, stoppingToken);

            try
            {
                await SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during schedule sync");
            }
        }
    }

    private async Task SyncAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var jobManager = scope.ServiceProvider.GetRequiredService<QuartzScheduleManager>();
        var chainManager = scope.ServiceProvider.GetRequiredService<ChainScheduleManager>();

        try
        {
            await jobManager.SyncAllAsync(ct);
            await chainManager.SyncAllAsync(ct);
            _logger.LogDebug("Schedule sync completed");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Schedule sync failed");
        }
    }
}

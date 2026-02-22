using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Courier.Worker;

public class WorkerHeartbeat : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkerHeartbeat> _logger;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    public WorkerHeartbeat(IServiceScopeFactory scopeFactory, ILogger<WorkerHeartbeat> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Validate DB connectivity
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CourierDbContext>();
            var canConnect = await db.Database.CanConnectAsync(stoppingToken);
            if (!canConnect)
            {
                _logger.LogCritical("Worker cannot connect to the database.");
                throw new InvalidOperationException("Database connection failed on Worker startup.");
            }
        }

        _logger.LogInformation("Worker started. Heartbeat interval: {Interval}s", HeartbeatInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Worker heartbeat at {Time}", DateTimeOffset.UtcNow);
            await Task.Delay(HeartbeatInterval, stoppingToken);
        }

        _logger.LogInformation("Worker stopping.");
    }
}

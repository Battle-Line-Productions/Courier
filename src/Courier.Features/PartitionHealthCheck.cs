using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Courier.Features;

public class PartitionHealthCheck : IHealthCheck
{
    private readonly CourierDbContext _db;

    public PartitionHealthCheck(CourierDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var nextMonth = now.AddMonths(1);
        var currentSuffix = now.ToString("yyyy_MM");
        var nextSuffix = nextMonth.ToString("yyyy_MM");

        var requiredPartitions = new[]
        {
            $"audit_log_entries_{currentSuffix}",
            $"audit_log_entries_{nextSuffix}",
            $"job_executions_{currentSuffix}",
            $"job_executions_{nextSuffix}",
            $"step_executions_{currentSuffix}",
            $"step_executions_{nextSuffix}",
        };

        var missing = new List<string>();

        var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        foreach (var partition in requiredPartitions)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pg_tables WHERE schemaname = 'public' AND tablename = @name";
            var param = cmd.CreateParameter();
            param.ParameterName = "name";
            param.Value = partition;
            cmd.Parameters.Add(param);

            var count = (long)(await cmd.ExecuteScalarAsync(cancellationToken))!;
            if (count == 0)
                missing.Add(partition);
        }

        if (missing.Count == 0)
            return HealthCheckResult.Healthy("All required partitions exist");

        return HealthCheckResult.Unhealthy(
            $"Missing partitions: {string.Join(", ", missing)}");
    }
}

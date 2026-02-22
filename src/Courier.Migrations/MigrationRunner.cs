using DbUp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Courier.Migrations;

/// <summary>
/// Runs DbUp migrations on API startup only. Acquires a PostgreSQL advisory lock
/// to prevent concurrent migration runs across replicas.
/// </summary>
public class MigrationRunner : IHostedService
{
    private readonly string _connectionString;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(IConfiguration configuration, ILogger<MigrationRunner> logger)
    {
        _connectionString = configuration.GetConnectionString("CourierDb")
            ?? throw new InvalidOperationException("ConnectionStrings:CourierDb is not configured.");
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting database migration...");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        // Advisory lock prevents concurrent migration runs across replicas
        await using var lockCmd = new NpgsqlCommand("SELECT pg_advisory_lock(12345)", conn);
        await lockCmd.ExecuteNonQueryAsync(cancellationToken);

        try
        {
            var upgrader = DeployChanges.To
                .PostgresqlDatabase(_connectionString)
                .WithScriptsEmbeddedInAssembly(typeof(MigrationMarker).Assembly)
                .WithTransactionPerScript()
                .LogToConsole()
                .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                _logger.LogError(result.Error, "Database migration failed.");
                throw new InvalidOperationException($"Migration failed: {result.Error.Message}", result.Error);
            }

            _logger.LogInformation("Database migration completed successfully. {ScriptCount} scripts applied.",
                result.Scripts.Count());
        }
        finally
        {
            await using var unlockCmd = new NpgsqlCommand("SELECT pg_advisory_unlock(12345)", conn);
            await unlockCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

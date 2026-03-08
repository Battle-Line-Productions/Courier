using Courier.Infrastructure.Data;
using DbUp;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Courier.Tests.JobEngine.Fixtures;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public CourierDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new CourierDbContext(options);
    }

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(ConnectionString)
            .WithScriptsEmbeddedInAssembly(typeof(Courier.Migrations.MigrationMarker).Assembly)
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
            throw new InvalidOperationException($"Test migration failed: {result.Error.Message}", result.Error);

        // Mark setup as completed so any middleware checks pass
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE system_settings SET value = 'true' WHERE key = 'auth.setup_completed'";
        await cmd.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}

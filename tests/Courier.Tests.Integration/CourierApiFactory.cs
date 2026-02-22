using Courier.Infrastructure.Data;
using Courier.Migrations;
using DbUp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Courier.Tests.Integration;

public class CourierApiFactory : WebApplicationFactory<Courier.Api.Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        // Run DbUp migrations against the test container
        var connectionString = _postgres.GetConnectionString();
        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(MigrationMarker).Assembly)
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
            throw new InvalidOperationException($"Test migration failed: {result.Error.Message}", result.Error);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove the MigrationRunner so it doesn't run again
            services.RemoveAll<IHostedService>();

            // Remove all Aspire-registered EF Core services (pool, options, context)
            var descriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("CourierDbContext") == true)
                .ToList();
            foreach (var d in descriptors) services.Remove(d);

            services.AddDbContext<CourierDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}

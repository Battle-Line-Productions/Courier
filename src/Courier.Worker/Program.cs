using Courier.Features;
using Courier.Infrastructure.Data;
using Courier.Migrations;
using Courier.Worker;
using Courier.Worker.Services;
using Quartz;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Aspire ServiceDefaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Serilog
builder.Services.AddSerilog((_, config) =>
{
    config.ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();

    var seqUrl = builder.Configuration["Seq:ServerUrl"];
    if (!string.IsNullOrWhiteSpace(seqUrl))
        config.WriteTo.Seq(seqUrl);
});

// EF Core via Aspire integration
builder.AddNpgsqlDbContext<CourierDbContext>("CourierDb");

// Worker does NOT run migrations — it validates schema version
builder.Services.AddHostedService<SchemaVersionValidator>();

// Worker heartbeat
builder.Services.AddHostedService<WorkerHeartbeat>();

// Features (engine, step registry, step handlers, services)
builder.Services.AddCourierFeatures(builder.Configuration);

// Job queue processor
builder.Services.AddHostedService<JobQueueProcessor>();

// Monitor polling service
builder.Services.AddHostedService<MonitorPollingService>();

// Key expiry checker (daily — transitions active → expiring → retired)
builder.Services.AddHostedService<KeyExpiryService>();

// Partition maintenance (weekly, pre-creates monthly partitions)
builder.Services.AddHostedService<PartitionMaintenanceService>();

// Stuck execution recovery (runs on startup + every 5 minutes)
builder.Services.AddHostedService<StuckExecutionRecoveryService>();

// Quartz.NET persistent store
builder.Services.AddQuartz(q =>
{
    q.SchedulerId = "CourierScheduler";
    q.UsePersistentStore(store =>
    {
        store.UsePostgres(builder.Configuration.GetConnectionString("CourierDb")!);
        store.UseNewtonsoftJsonSerializer();
    });
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Schedule sync — Quartz registration manager + periodic sync from DB
builder.Services.AddScoped<QuartzScheduleManager>();
builder.Services.AddScoped<ChainScheduleManager>();
builder.Services.AddHostedService<ScheduleStartupSync>();

var host = builder.Build();
host.Run();

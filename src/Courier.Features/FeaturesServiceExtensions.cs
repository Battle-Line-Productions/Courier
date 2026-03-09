using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Features.Connections;
using Courier.Features.Dashboard;
using Courier.Features.Engine;
using Courier.Features.Engine.Crypto;
using Courier.Features.Engine.Protocols;
using Courier.Features.Engine.Steps;
using Courier.Features.Engine.Steps.Crypto;
using Courier.Features.Engine.Steps.Azure;
using Courier.Features.Engine.Steps.FileOps;
using Courier.Features.Engine.Steps.Flow;
using Courier.Features.Engine.Steps.Transfer;
using Courier.Features.Engine.Compression;
using Courier.Features.Auth;
using Courier.Features.AuditLog;
using Courier.Features.AzureFunctions;
using Courier.Features.Filesystem;
using Courier.Features.Jobs;
using Courier.Features.Monitors;
using Courier.Features.PgpKeys;
using Courier.Features.SshKeys;
using Courier.Features.Chains;
using Courier.Features.Tags;
using Courier.Features.Notifications;
using Courier.Features.Notifications.Channels;
using Courier.Features.Events;
using Courier.Features.Keys;
using Courier.Features.Security;
using Courier.Features.Settings;
using Courier.Features.Setup;
using Courier.Features.Feedback;
using Courier.Features.Users;
using Courier.Infrastructure.Encryption;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Courier.Features;

public static class FeaturesServiceExtensions
{
    public static IServiceCollection AddCourierFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<JobService>();
        services.AddScoped<JobStepService>();
        services.AddScoped<ExecutionService>();
        services.AddScoped<JobScheduleService>();
        services.AddScoped<FilesystemService>();
        services.AddScoped<JobEngine>();
        services.AddScoped<IJobStep, FileCopyStep>();
        services.AddScoped<IJobStep, FileMoveStep>();
        services.AddScoped<StepTypeRegistry>();

        // Workspace
        services.Configure<WorkspaceSettings>(configuration.GetSection("Workspace"));
        services.AddScoped<JobWorkspace>();

        // Protocol services
        services.AddScoped<ITransferClientFactory, TransferClientFactory>();
        services.AddScoped<JobConnectionRegistry>();

        // Crypto services
        services.AddScoped<ICryptoProvider, PgpCryptoProvider>();

        // Compression services
        services.AddScoped<ICompressionProvider, ZipCompressionProvider>();
        services.AddScoped<ICompressionProvider, TarCompressionProvider>();
        services.AddScoped<ICompressionProvider, GzipCompressionProvider>();
        services.AddScoped<ICompressionProvider, TarGzCompressionProvider>();
        services.AddScoped<CompressionProviderRegistry>();

        // File operation step handlers (3)
        services.AddScoped<IJobStep, FileZipStep>();
        services.AddScoped<IJobStep, FileUnzipStep>();
        services.AddScoped<IJobStep, FileDeleteStep>();

        // Transfer step handlers (15)
        services.AddScoped<IJobStep, SftpUploadStep>();
        services.AddScoped<IJobStep, SftpDownloadStep>();
        services.AddScoped<IJobStep, SftpMkdirStep>();
        services.AddScoped<IJobStep, SftpRmdirStep>();
        services.AddScoped<IJobStep, SftpListStep>();
        services.AddScoped<IJobStep, FtpUploadStep>();
        services.AddScoped<IJobStep, FtpDownloadStep>();
        services.AddScoped<IJobStep, FtpMkdirStep>();
        services.AddScoped<IJobStep, FtpRmdirStep>();
        services.AddScoped<IJobStep, FtpListStep>();
        services.AddScoped<IJobStep, FtpsUploadStep>();
        services.AddScoped<IJobStep, FtpsDownloadStep>();
        services.AddScoped<IJobStep, FtpsMkdirStep>();
        services.AddScoped<IJobStep, FtpsRmdirStep>();
        services.AddScoped<IJobStep, FtpsListStep>();

        // Flow control step handlers (4)
        services.AddScoped<IJobStep, FlowForEachStep>();
        services.AddScoped<IJobStep, FlowIfStep>();
        services.AddScoped<IJobStep, FlowElseStep>();
        services.AddScoped<IJobStep, FlowEndStep>();

        // Crypto step handlers (4)
        services.AddScoped<IJobStep, PgpEncryptStep>();
        services.AddScoped<IJobStep, PgpDecryptStep>();
        services.AddScoped<IJobStep, PgpSignStep>();
        services.AddScoped<IJobStep, PgpVerifyStep>();

        // Azure Function step handler
        services.AddHttpClient("AzureFunctions");
        services.AddHttpClient("LogAnalytics");
        services.AddScoped<AzureFunctionClient>();
        services.AddScoped<AppInsightsQueryService>();
        services.AddScoped<IJobStep, AzureFunctionExecuteStep>();

        services.AddValidatorsFromAssemblyContaining<CreateJobValidator>();

        // Connections
        services.AddScoped<ConnectionService>();
        services.AddScoped<KnownHostService>();

        // Keys
        services.AddScoped<PgpKeyService>();
        services.AddScoped<SshKeyService>();
        services.AddScoped<KeyShareService>();

        // Monitors
        services.AddScoped<MonitorService>();

        // Dashboard
        services.AddScoped<DashboardService>();

        // Audit
        services.AddScoped<AuditService>();

        // Auth
        services.AddScoped<AuthService>();
        services.AddScoped<JwtTokenService>();

        // Settings
        services.AddScoped<SettingsService>();

        // Setup
        services.AddScoped<SetupService>();

        // Users
        services.AddScoped<UserService>();

        // Tags
        services.AddScoped<TagService>();

        // Chains & Dependencies
        services.AddScoped<ChainService>();
        services.AddScoped<ChainExecutionService>();
        services.AddScoped<ChainOrchestrator>();
        services.AddScoped<ChainScheduleService>();
        services.AddScoped<JobDependencyService>();

        // Notifications
        services.AddScoped<NotificationRuleService>();
        services.AddScoped<NotificationLogService>();
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<INotificationChannel, WebhookNotificationChannel>();
        services.AddScoped<INotificationChannel, EmailNotificationChannel>();
        services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));
        services.AddHttpClient("Webhooks");

        // Domain Events
        services.AddScoped<DomainEventService>();

        // Security
        services.AddSingleton<FipsEnforcer>();

        // Encryption
        services.Configure<EncryptionSettings>(configuration.GetSection("Encryption"));
        services.AddSingleton<ICredentialEncryptor, AesGcmCredentialEncryptor>();

        // Feedback / GitHub
        services.Configure<GitHubSettings>(configuration.GetSection("GitHub"));
        services.AddHttpClient("GitHub", client =>
        {
            client.BaseAddress = new Uri("https://api.github.com");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("User-Agent", "Courier-Feedback");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        });
        services.AddScoped<GitHubAuthService>();
        services.AddScoped<FeedbackService>();
        services.AddMemoryCache();

        // Health checks
        services.AddHealthChecks()
            .AddCheck<PartitionHealthCheck>("partitions", tags: ["partitions"]);

        return services;
    }
}

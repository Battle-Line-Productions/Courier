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
using Courier.Features.Engine.Steps.Transfer;
using Courier.Features.Engine.Compression;
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

        // Protocol services
        services.AddScoped<ITransferClientFactory, TransferClientFactory>();
        services.AddScoped<JobConnectionRegistry>();

        // Crypto services
        services.AddScoped<ICryptoProvider, PgpCryptoProvider>();

        // Compression services
        services.AddScoped<ICompressionProvider, ZipCompressionProvider>();
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

        // Keys
        services.AddScoped<PgpKeyService>();
        services.AddScoped<SshKeyService>();

        // Monitors
        services.AddScoped<MonitorService>();

        // Dashboard
        services.AddScoped<DashboardService>();

        // Audit
        services.AddScoped<AuditService>();

        // Tags
        services.AddScoped<TagService>();

        // Chains & Dependencies
        services.AddScoped<ChainService>();
        services.AddScoped<ChainExecutionService>();
        services.AddScoped<ChainOrchestrator>();
        services.AddScoped<JobDependencyService>();

        // Notifications
        services.AddScoped<NotificationRuleService>();
        services.AddScoped<NotificationLogService>();
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<INotificationChannel, WebhookNotificationChannel>();
        services.AddScoped<INotificationChannel, EmailNotificationChannel>();
        services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));
        services.AddHttpClient("Webhooks");

        // Encryption
        services.Configure<EncryptionSettings>(configuration.GetSection("Encryption"));
        services.AddSingleton<ICredentialEncryptor, AesGcmCredentialEncryptor>();

        return services;
    }
}

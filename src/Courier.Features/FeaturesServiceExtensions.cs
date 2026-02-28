using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Features.Connections;
using Courier.Features.Engine;
using Courier.Features.Engine.Crypto;
using Courier.Features.Engine.Protocols;
using Courier.Features.Engine.Steps;
using Courier.Features.Engine.Steps.Crypto;
using Courier.Features.Engine.Steps.Transfer;
using Courier.Features.Filesystem;
using Courier.Features.Jobs;
using Courier.Features.PgpKeys;
using Courier.Features.SshKeys;
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
        services.AddValidatorsFromAssemblyContaining<CreateJobValidator>();

        // Connections
        services.AddScoped<ConnectionService>();

        // Keys
        services.AddScoped<PgpKeyService>();
        services.AddScoped<SshKeyService>();

        // Encryption
        services.Configure<EncryptionSettings>(configuration.GetSection("Encryption"));
        services.AddSingleton<ICredentialEncryptor, AesGcmCredentialEncryptor>();

        return services;
    }
}

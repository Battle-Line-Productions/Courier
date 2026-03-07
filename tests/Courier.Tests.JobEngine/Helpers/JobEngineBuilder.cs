using Courier.Domain.Engine;
using Courier.Domain.Encryption;
using Courier.Features.AuditLog;
using Courier.Features.AzureFunctions;
using Courier.Features.Engine;
using Courier.Features.Engine.Compression;
using Courier.Features.Engine.Crypto;
using Courier.Features.Engine.Protocols;
using Courier.Features.Engine.Steps;
using Courier.Features.Engine.Steps.Azure;
using Courier.Features.Engine.Steps.Crypto;
using Courier.Features.Engine.Steps.FileOps;
using Courier.Features.Engine.Steps.Flow;
using Courier.Features.Engine.Steps.Transfer;
using Courier.Features.Notifications;
using Courier.Infrastructure.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Courier.Tests.JobEngine.Helpers;

public class JobEngineBuilder
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private readonly List<IJobStep> _additionalSteps = [];
    private string _baseDirectory = Path.GetTempPath();
    private bool _cleanupOnCompletion;

    public JobEngineBuilder(CourierDbContext db, ICredentialEncryptor encryptor)
    {
        _db = db;
        _encryptor = encryptor;
    }

    public JobEngineBuilder WithBaseDirectory(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        return this;
    }

    public JobEngineBuilder WithCleanupOnCompletion(bool cleanup = true)
    {
        _cleanupOnCompletion = cleanup;
        return this;
    }

    public JobEngineBuilder WithAdditionalSteps(params IJobStep[] additionalSteps)
    {
        _additionalSteps.AddRange(additionalSteps);
        return this;
    }

    public Features.Engine.JobEngine Build()
    {
        // Transfer infrastructure
        var transferFactory = new TransferClientFactory();
        var connectionRegistry = new JobConnectionRegistry(transferFactory);

        // Crypto infrastructure
        var cryptoProvider = new PgpCryptoProvider(_db, _encryptor);

        // Compression infrastructure
        var zipProvider = new ZipCompressionProvider();
        var compressionRegistry = new CompressionProviderRegistry(new ICompressionProvider[] { zipProvider });

        // Azure Function step (mock the HTTP clients since we won't test Azure)
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var functionClient = new AzureFunctionClient(httpClientFactory, NullLogger<AzureFunctionClient>.Instance);
        var appInsights = new AppInsightsQueryService(httpClientFactory, NullLogger<AppInsightsQueryService>.Instance);

        // Build all step handlers
        var steps = new List<IJobStep>
        {
            // File ops (no deps)
            new FileCopyStep(),
            new FileMoveStep(),
            new FileDeleteStep(),

            // Compression
            new FileZipStep(compressionRegistry),
            new FileUnzipStep(compressionRegistry),

            // SFTP transfers
            new SftpUploadStep(_db, _encryptor, connectionRegistry),
            new SftpDownloadStep(_db, _encryptor, connectionRegistry),
            new SftpMkdirStep(_db, _encryptor, connectionRegistry),
            new SftpRmdirStep(_db, _encryptor, connectionRegistry),
            new SftpListStep(_db, _encryptor, connectionRegistry),

            // FTP transfers
            new FtpUploadStep(_db, _encryptor, connectionRegistry),
            new FtpDownloadStep(_db, _encryptor, connectionRegistry),
            new FtpMkdirStep(_db, _encryptor, connectionRegistry),
            new FtpRmdirStep(_db, _encryptor, connectionRegistry),
            new FtpListStep(_db, _encryptor, connectionRegistry),

            // FTPS transfers
            new FtpsUploadStep(_db, _encryptor, connectionRegistry),
            new FtpsDownloadStep(_db, _encryptor, connectionRegistry),
            new FtpsMkdirStep(_db, _encryptor, connectionRegistry),
            new FtpsRmdirStep(_db, _encryptor, connectionRegistry),
            new FtpsListStep(_db, _encryptor, connectionRegistry),

            // Crypto
            new PgpEncryptStep(cryptoProvider),
            new PgpDecryptStep(cryptoProvider),
            new PgpSignStep(cryptoProvider),
            new PgpVerifyStep(cryptoProvider),

            // Flow control
            new FlowForEachStep(),
            new FlowIfStep(),
            new FlowElseStep(),
            new FlowEndStep(),

            // Azure Function (mocked)
            new AzureFunctionExecuteStep(_db, _encryptor, functionClient, appInsights,
                NullLogger<AzureFunctionExecuteStep>.Instance),
        };

        steps.AddRange(_additionalSteps);

        var stepRegistry = new StepTypeRegistry(steps);
        var workspace = new JobWorkspace(NullLogger<JobWorkspace>.Instance);
        var workspaceSettings = Options.Create(new WorkspaceSettings
        {
            BaseDirectory = _baseDirectory,
            CleanupOnCompletion = _cleanupOnCompletion,
        });

        var audit = new AuditService(_db);
        var dispatcher = new NotificationDispatcher(
            _db,
            Array.Empty<INotificationChannel>(),
            NullLogger<NotificationDispatcher>.Instance);

        return new Features.Engine.JobEngine(
            _db,
            stepRegistry,
            connectionRegistry,
            workspace,
            workspaceSettings,
            NullLogger<Features.Engine.JobEngine>.Instance,
            audit,
            dispatcher);
    }
}

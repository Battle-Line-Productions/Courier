using Courier.Domain.Engine;
using Courier.Domain.Encryption;
using Courier.Features.AuditLog;
using Courier.Features.Callbacks;
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
using Courier.Features.Events;
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

        // Azure Function step (mock deps since we won't test Azure)
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var callbackService = new StepCallbackService(_db);
        var configuration = Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>();

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
            new SftpUploadStep(_db, _encryptor, connectionRegistry, NullLogger<SftpUploadStep>.Instance),
            new SftpDownloadStep(_db, _encryptor, connectionRegistry, NullLogger<SftpDownloadStep>.Instance),
            new SftpMkdirStep(_db, _encryptor, connectionRegistry, NullLogger<SftpMkdirStep>.Instance),
            new SftpRmdirStep(_db, _encryptor, connectionRegistry, NullLogger<SftpRmdirStep>.Instance),
            new SftpListStep(_db, _encryptor, connectionRegistry, NullLogger<SftpListStep>.Instance),

            // FTP transfers
            new FtpUploadStep(_db, _encryptor, connectionRegistry, NullLogger<FtpUploadStep>.Instance),
            new FtpDownloadStep(_db, _encryptor, connectionRegistry, NullLogger<FtpDownloadStep>.Instance),
            new FtpMkdirStep(_db, _encryptor, connectionRegistry, NullLogger<FtpMkdirStep>.Instance),
            new FtpRmdirStep(_db, _encryptor, connectionRegistry, NullLogger<FtpRmdirStep>.Instance),
            new FtpListStep(_db, _encryptor, connectionRegistry, NullLogger<FtpListStep>.Instance),

            // FTPS transfers
            new FtpsUploadStep(_db, _encryptor, connectionRegistry, NullLogger<FtpsUploadStep>.Instance),
            new FtpsDownloadStep(_db, _encryptor, connectionRegistry, NullLogger<FtpsDownloadStep>.Instance),
            new FtpsMkdirStep(_db, _encryptor, connectionRegistry, NullLogger<FtpsMkdirStep>.Instance),
            new FtpsRmdirStep(_db, _encryptor, connectionRegistry, NullLogger<FtpsRmdirStep>.Instance),
            new FtpsListStep(_db, _encryptor, connectionRegistry, NullLogger<FtpsListStep>.Instance),

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
            new AzureFunctionExecuteStep(_db, _encryptor, callbackService, httpClientFactory,
                configuration, NullLogger<AzureFunctionExecuteStep>.Instance),
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
        var events = new DomainEventService(_db);

        return new Features.Engine.JobEngine(
            _db,
            stepRegistry,
            connectionRegistry,
            workspace,
            workspaceSettings,
            NullLogger<Features.Engine.JobEngine>.Instance,
            audit,
            dispatcher,
            events);
    }
}

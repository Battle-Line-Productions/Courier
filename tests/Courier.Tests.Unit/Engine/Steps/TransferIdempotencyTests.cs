using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using Courier.Features.Engine.Protocols;
using Courier.Features.Engine.Steps.Transfer;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Steps;

public class TransferIdempotencyTests : IDisposable
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private readonly ITransferClientFactory _factory;
    private readonly JobConnectionRegistry _registry;
    private readonly ITransferClient _mockClient;
    private readonly string _tempDir;

    public TransferIdempotencyTests()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CourierDbContext(options);
        _encryptor = Substitute.For<ICredentialEncryptor>();
        _factory = Substitute.For<ITransferClientFactory>();
        _mockClient = Substitute.For<ITransferClient>();
        _mockClient.IsConnected.Returns(true);

        _factory.Create(Arg.Any<Connection>(), Arg.Any<byte[]?>(), Arg.Any<byte[]?>())
            .Returns(_mockClient);

        _registry = new JobConnectionRegistry(_factory);

        _tempDir = Path.Combine(Path.GetTempPath(), $"courier_transfer_idem_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private async Task<Guid> SeedSftpConnection()
    {
        var connection = new Connection
        {
            Id = Guid.NewGuid(),
            Name = "Test SFTP",
            Protocol = "sftp",
            Host = "sftp.example.com",
            Port = 22,
            AuthMethod = "password",
            Username = "user",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Connections.Add(connection);
        await _db.SaveChangesAsync();
        return connection.Id;
    }

    private async Task<Guid> SeedFtpsConnection()
    {
        var connection = new Connection
        {
            Id = Guid.NewGuid(),
            Name = "Test FTPS",
            Protocol = "ftps",
            Host = "ftps.example.com",
            Port = 990,
            AuthMethod = "password",
            Username = "user",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Connections.Add(connection);
        await _db.SaveChangesAsync();
        return connection.Id;
    }

    private static StepConfiguration MakeUploadConfig(Guid connectionId, string localPath, string remotePath, string? idempotency = null)
    {
        var idem = idempotency is not null ? $""", "idempotency": "{idempotency}" """ : "";
        var json = $$"""{"connection_id": "{{connectionId}}", "local_path": "{{localPath.Replace("\\", "\\\\")}}", "remote_path": "{{remotePath}}"{{idem}}}""";
        return new StepConfiguration(json);
    }

    private static StepConfiguration MakeDownloadConfig(Guid connectionId, string remotePath, string localPath, string? idempotency = null)
    {
        var idem = idempotency is not null ? $""", "idempotency": "{idempotency}" """ : "";
        var json = $$"""{"connection_id": "{{connectionId}}", "remote_path": "{{remotePath}}", "local_path": "{{localPath.Replace("\\", "\\\\")}}"{{idem}}}""";
        return new StepConfiguration(json);
    }

    [Fact]
    public async Task SftpUpload_SkipIfExists_SkipsWhenRemoteFileExists()
    {
        // Arrange
        var connId = await SeedSftpConnection();
        var localPath = Path.Combine(_tempDir, "upload.txt");
        File.WriteAllText(localPath, "data");

        _mockClient.ListDirectoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<RemoteFileInfo>
            {
                new("upload.txt", "/remote/upload.txt", 100, DateTime.UtcNow, false)
            });

        var step = new SftpUploadStep(_db, _encryptor, _registry, NullLogger<SftpUploadStep>.Instance);
        var config = MakeUploadConfig(connId, localPath, "/remote/upload.txt", "skip_if_exists");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Outputs!["skipped"].ShouldBe("true");
        result.Outputs["reason"].ShouldBe("file_exists");
        result.BytesProcessed.ShouldBe(0);
        await _mockClient.DidNotReceive().UploadAsync(Arg.Any<UploadRequest>(), Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SftpUpload_SkipIfExists_ProceedsWhenRemoteFileMissing()
    {
        // Arrange
        var connId = await SeedSftpConnection();
        var localPath = Path.Combine(_tempDir, "upload.txt");
        File.WriteAllText(localPath, "data");

        _mockClient.ListDirectoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<RemoteFileInfo>()); // empty directory

        var step = new SftpUploadStep(_db, _encryptor, _registry, NullLogger<SftpUploadStep>.Instance);
        var config = MakeUploadConfig(connId, localPath, "/remote/upload.txt", "skip_if_exists");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Outputs!.ContainsKey("skipped").ShouldBeFalse();
        await _mockClient.Received(1).UploadAsync(Arg.Any<UploadRequest>(), Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SftpUpload_Overwrite_UploadsRegardlessOfRemoteFile()
    {
        // Arrange
        var connId = await SeedSftpConnection();
        var localPath = Path.Combine(_tempDir, "upload.txt");
        File.WriteAllText(localPath, "data");

        var step = new SftpUploadStep(_db, _encryptor, _registry, NullLogger<SftpUploadStep>.Instance);
        var config = MakeUploadConfig(connId, localPath, "/remote/upload.txt", "overwrite");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        await _mockClient.Received(1).UploadAsync(Arg.Any<UploadRequest>(), Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SftpDownload_SkipIfExists_SkipsWhenLocalFileExists()
    {
        // Arrange
        var connId = await SeedSftpConnection();
        var localPath = Path.Combine(_tempDir, "downloaded.txt");
        File.WriteAllText(localPath, "already here");

        var step = new SftpDownloadStep(_db, _encryptor, _registry, NullLogger<SftpDownloadStep>.Instance);
        var config = MakeDownloadConfig(connId, "/remote/downloaded.txt", localPath, "skip_if_exists");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Outputs!["skipped"].ShouldBe("true");
        result.Outputs["reason"].ShouldBe("file_exists");
        await _mockClient.DidNotReceive().DownloadAsync(Arg.Any<DownloadRequest>(), Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SftpDownload_SkipIfExists_ProceedsWhenLocalFileMissing()
    {
        // Arrange
        var connId = await SeedSftpConnection();
        var localPath = Path.Combine(_tempDir, "not_yet.txt"); // does not exist

        var step = new SftpDownloadStep(_db, _encryptor, _registry, NullLogger<SftpDownloadStep>.Instance);
        var config = MakeDownloadConfig(connId, "/remote/not_yet.txt", localPath, "skip_if_exists");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Outputs!.ContainsKey("skipped").ShouldBeFalse();
        await _mockClient.Received(1).DownloadAsync(Arg.Any<DownloadRequest>(), Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SftpDownload_Overwrite_DownloadsRegardlessOfLocalFile()
    {
        // Arrange
        var connId = await SeedSftpConnection();
        var localPath = Path.Combine(_tempDir, "existing.txt");
        File.WriteAllText(localPath, "old data");

        var step = new SftpDownloadStep(_db, _encryptor, _registry, NullLogger<SftpDownloadStep>.Instance);
        var config = MakeDownloadConfig(connId, "/remote/existing.txt", localPath, "overwrite");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        await _mockClient.Received(1).DownloadAsync(Arg.Any<DownloadRequest>(), Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FtpsUpload_SkipIfExists_SkipsWhenRemoteFileExists()
    {
        // Arrange
        var connId = await SeedFtpsConnection();
        var localPath = Path.Combine(_tempDir, "upload.txt");
        File.WriteAllText(localPath, "ftps data");

        _mockClient.ListDirectoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<RemoteFileInfo>
            {
                new("upload.txt", "/remote/upload.txt", 50, DateTime.UtcNow, false)
            });

        var step = new FtpsUploadStep(_db, _encryptor, _registry, NullLogger<FtpsUploadStep>.Instance);
        var config = MakeUploadConfig(connId, localPath, "/remote/upload.txt", "skip_if_exists");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Outputs!["skipped"].ShouldBe("true");
        result.Outputs["reason"].ShouldBe("file_exists");
    }

    [Fact]
    public async Task FtpsDownload_SkipIfExists_SkipsWhenLocalFileExists()
    {
        // Arrange
        var connId = await SeedFtpsConnection();
        var localPath = Path.Combine(_tempDir, "local.txt");
        File.WriteAllText(localPath, "existing");

        var step = new FtpsDownloadStep(_db, _encryptor, _registry, NullLogger<FtpsDownloadStep>.Instance);
        var config = MakeDownloadConfig(connId, "/remote/local.txt", localPath, "skip_if_exists");
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Outputs!["skipped"].ShouldBe("true");
        result.Outputs["reason"].ShouldBe("file_exists");
        await _mockClient.DidNotReceive().DownloadAsync(Arg.Any<DownloadRequest>(), Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SftpUpload_DefaultIdempotency_UploadsWithoutCheckingRemote()
    {
        // Arrange
        var connId = await SeedSftpConnection();
        var localPath = Path.Combine(_tempDir, "upload.txt");
        File.WriteAllText(localPath, "data");

        var step = new SftpUploadStep(_db, _encryptor, _registry, NullLogger<SftpUploadStep>.Instance);
        var config = MakeUploadConfig(connId, localPath, "/remote/upload.txt"); // no idempotency
        var context = new JobContext();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        await _mockClient.DidNotReceive().ListDirectoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mockClient.Received(1).UploadAsync(Arg.Any<UploadRequest>(), Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<CancellationToken>());
    }
}

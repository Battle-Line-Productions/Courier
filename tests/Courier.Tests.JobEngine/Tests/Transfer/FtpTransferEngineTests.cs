using System.Text.Json;
using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.Transfer;

[Collection("EngineTests")]
[Trait("Category", "Transfer")]
public class FtpTransferEngineTests
{
    private readonly DatabaseFixture _database;
    private readonly FtpServerFixture _ftp;

    public FtpTransferEngineTests(DatabaseFixture database, FtpServerFixture ftp)
    {
        _database = database;
        _ftp = ftp;
    }

    [Fact]
    public async Task FtpUpload_FileExistsOnServer()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var originalContent = $"ftp-upload-{Guid.NewGuid()}";
            var localFile = Path.Combine(tempDir, "upload.txt");
            await File.WriteAllTextAsync(localFile, originalContent);

            var connId = await TestDataSeeder.SeedFtpConnection(db, encryptor,
                _ftp.Host, _ftp.ControlPort, _ftp.Username, _ftp.Password);

            var remoteFileName = $"test-{Guid.NewGuid():N}.txt";
            var remotePath = $"/{remoteFileName}";
            var downloadFile = Path.Combine(tempDir, "verify-download.txt");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "ftp.upload", "Upload", new
            {
                connection_id = connId.ToString(),
                local_path = localFile,
                remote_path = remotePath,
            });
            // Verify by downloading back (FTP container paths are not directly accessible via exec)
            await TestDataSeeder.AddStep(db, jobId, 1, "ftp.download", "Download verify", new
            {
                connection_id = connId.ToString(),
                remote_path = remotePath,
                local_path = downloadFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(downloadFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(downloadFile)).ShouldBe(originalContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FtpDownload_FileDownloadedLocally()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Upload a file first, then download it in a separate execution
            var originalContent = $"ftp-download-{Guid.NewGuid()}";
            var uploadFile = Path.Combine(tempDir, "to-upload.txt");
            await File.WriteAllTextAsync(uploadFile, originalContent);

            var connId = await TestDataSeeder.SeedFtpConnection(db, encryptor,
                _ftp.Host, _ftp.ControlPort, _ftp.Username, _ftp.Password);

            var remoteFileName = $"test-{Guid.NewGuid():N}.txt";
            var remotePath = $"/{remoteFileName}";

            // First job: upload
            var (uploadJobId, uploadExecId) = await TestDataSeeder.SeedJob(db, "upload-setup");
            await TestDataSeeder.AddStep(db, uploadJobId, 0, "ftp.upload", "Upload setup", new
            {
                connection_id = connId.ToString(),
                local_path = uploadFile,
                remote_path = remotePath,
            });
            await engine.ExecuteAsync(uploadExecId, CancellationToken.None);

            var uploadExec = await db.JobExecutions.FirstAsync(e => e.Id == uploadExecId);
            uploadExec.State.ShouldBe(JobExecutionState.Completed);

            // Second job: download
            var downloadFile = Path.Combine(tempDir, "downloaded.txt");
            var (downloadJobId, downloadExecId) = await TestDataSeeder.SeedJob(db, "download-test");
            await TestDataSeeder.AddStep(db, downloadJobId, 0, "ftp.download", "Download", new
            {
                connection_id = connId.ToString(),
                remote_path = remotePath,
                local_path = downloadFile,
            });
            await engine.ExecuteAsync(downloadExecId, CancellationToken.None);

            var downloadExec = await db.JobExecutions.FirstAsync(e => e.Id == downloadExecId);
            downloadExec.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(downloadFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(downloadFile)).ShouldBe(originalContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FtpMkdir_DirectoryCreatedOnServer()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var connId = await TestDataSeeder.SeedFtpConnection(db, encryptor,
                _ftp.Host, _ftp.ControlPort, _ftp.Username, _ftp.Password);

            var dirName = $"testdir-{Guid.NewGuid():N}";
            var remotePath = $"/{dirName}";

            // Create directory, then upload a file into it to verify it exists
            var verifyFile = Path.Combine(tempDir, "verify.txt");
            await File.WriteAllTextAsync(verifyFile, "mkdir-verify");
            var downloadFile = Path.Combine(tempDir, "verify-download.txt");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "ftp.mkdir", "Mkdir", new
            {
                connection_id = connId.ToString(),
                remote_path = remotePath,
            });
            // Upload into the new directory to prove it exists
            await TestDataSeeder.AddStep(db, jobId, 1, "ftp.upload", "Upload into dir", new
            {
                connection_id = connId.ToString(),
                local_path = verifyFile,
                remote_path = $"{remotePath}/verify.txt",
            });
            await TestDataSeeder.AddStep(db, jobId, 2, "ftp.download", "Download from dir", new
            {
                connection_id = connId.ToString(),
                remote_path = $"{remotePath}/verify.txt",
                local_path = downloadFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(downloadFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(downloadFile)).ShouldBe("mkdir-verify");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FtpList_ReturnsFileNames()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var connId = await TestDataSeeder.SeedFtpConnection(db, encryptor,
                _ftp.Host, _ftp.ControlPort, _ftp.Username, _ftp.Password);

            var listDir = $"/listdir-{Guid.NewGuid():N}";

            // Create dir, upload two files, then list
            var fileA = Path.Combine(tempDir, "fileA.txt");
            var fileB = Path.Combine(tempDir, "fileB.txt");
            await File.WriteAllTextAsync(fileA, "a");
            await File.WriteAllTextAsync(fileB, "b");

            var (setupJobId, setupExecId) = await TestDataSeeder.SeedJob(db, "list-setup");
            await TestDataSeeder.AddStep(db, setupJobId, 0, "ftp.mkdir", "Mkdir", new
            {
                connection_id = connId.ToString(),
                remote_path = listDir,
            });
            await TestDataSeeder.AddStep(db, setupJobId, 1, "ftp.upload", "Upload A", new
            {
                connection_id = connId.ToString(),
                local_path = fileA,
                remote_path = $"{listDir}/fileA.txt",
            });
            await TestDataSeeder.AddStep(db, setupJobId, 2, "ftp.upload", "Upload B", new
            {
                connection_id = connId.ToString(),
                local_path = fileB,
                remote_path = $"{listDir}/fileB.txt",
            });
            await engine.ExecuteAsync(setupExecId, CancellationToken.None);

            var setupExec = await db.JobExecutions.FirstAsync(e => e.Id == setupExecId);
            setupExec.State.ShouldBe(JobExecutionState.Completed);

            // Now list
            var (listJobId, listExecId) = await TestDataSeeder.SeedJob(db, "list-test");
            await TestDataSeeder.AddStep(db, listJobId, 0, "ftp.list", "List", new
            {
                connection_id = connId.ToString(),
                remote_path = listDir,
            });
            await engine.ExecuteAsync(listExecId, CancellationToken.None);

            var listExec = await db.JobExecutions.FirstAsync(e => e.Id == listExecId);
            listExec.State.ShouldBe(JobExecutionState.Completed);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == listExecId && se.StepOrder == 0);
            stepExec.OutputData.ShouldNotBeNull();

            var output = JsonDocument.Parse(stepExec.OutputData);
            var fileListJson = output.RootElement.GetProperty("file_list").GetString();
            fileListJson.ShouldNotBeNull();

            var fileList = JsonDocument.Parse(fileListJson);
            fileList.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);

            var fileNames = fileList.RootElement.EnumerateArray()
                .Select(f => f.GetProperty("Name").GetString())
                .ToList();
            fileNames.ShouldContain("fileA.txt");
            fileNames.ShouldContain("fileB.txt");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FtpUploadThenDownload_RoundtripContentIdentical()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var originalContent = $"ftp-roundtrip-{Guid.NewGuid()}";
            var localUploadFile = Path.Combine(tempDir, "roundtrip-upload.txt");
            await File.WriteAllTextAsync(localUploadFile, originalContent);

            var connId = await TestDataSeeder.SeedFtpConnection(db, encryptor,
                _ftp.Host, _ftp.ControlPort, _ftp.Username, _ftp.Password);

            var remotePath = $"/roundtrip-{Guid.NewGuid():N}.txt";
            var localDownloadFile = Path.Combine(tempDir, "roundtrip-download.txt");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "ftp.upload", "Upload", new
            {
                connection_id = connId.ToString(),
                local_path = localUploadFile,
                remote_path = remotePath,
            });
            await TestDataSeeder.AddStep(db, jobId, 1, "ftp.download", "Download", new
            {
                connection_id = connId.ToString(),
                remote_path = remotePath,
                local_path = localDownloadFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(localDownloadFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(localDownloadFile)).ShouldBe(originalContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task FtpUpload_ProtocolMismatch_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var localFile = Path.Combine(tempDir, "upload.txt");
            await File.WriteAllTextAsync(localFile, "protocol mismatch test");

            // Seed a connection with protocol "sftp" instead of "ftp"
            var sftpConnId = Guid.CreateVersion7();
            var conn = new Domain.Entities.Connection
            {
                Id = sftpConnId,
                Name = $"test-sftp-mismatch-{sftpConnId:N}",
                Protocol = "sftp",
                Host = _ftp.Host,
                Port = _ftp.ControlPort,
                AuthMethod = "password",
                Username = _ftp.Username,
                PasswordEncrypted = encryptor.Encrypt(_ftp.Password),
                HostKeyPolicy = "always_trust",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Connections.Add(conn);
            await db.SaveChangesAsync();

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "ftp.upload", "Upload", new
            {
                connection_id = sftpConnId.ToString(),
                local_path = localFile,
                remote_path = $"/mismatch-{Guid.NewGuid():N}.txt",
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            var step = await db.StepExecutions
                .FirstAsync(s => s.JobExecutionId == executionId && s.StepOrder == 0);
            step.State.ShouldBe(StepExecutionState.Failed);
            step.ErrorMessage.ShouldNotBeNullOrEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task FtpDownload_NonexistentRemoteFile_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var localFile = Path.Combine(tempDir, "downloaded.txt");

            var connId = await TestDataSeeder.SeedFtpConnection(db, encryptor,
                _ftp.Host, _ftp.ControlPort, _ftp.Username, _ftp.Password);

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "ftp.download", "Download", new
            {
                connection_id = connId.ToString(),
                remote_path = "/nonexistent-file-xxx.txt",
                local_path = localFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

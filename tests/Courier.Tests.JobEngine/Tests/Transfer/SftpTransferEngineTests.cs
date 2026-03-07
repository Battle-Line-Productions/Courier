using System.Text.Json;
using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.Transfer;

[Collection("EngineTests")]
[Trait("Category", "Transfer")]
public class SftpTransferEngineTests
{
    private readonly DatabaseFixture _database;
    private readonly SftpServerFixture _sftp;

    public SftpTransferEngineTests(DatabaseFixture database, SftpServerFixture sftp)
    {
        _database = database;
        _sftp = sftp;
    }

    [Fact]
    public async Task SftpUpload_FileExistsOnServer()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var localFile = Path.Combine(tempDir, "upload.txt");
            await File.WriteAllTextAsync(localFile, "upload content");

            var connId = await TestDataSeeder.SeedSftpConnection(db, encryptor,
                _sftp.Host, _sftp.Port, _sftp.Username, _sftp.Password);

            var remotePath = $"/home/testuser/upload/test-{Guid.NewGuid():N}.txt";
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "sftp.upload", "Upload", new
            {
                connection_id = connId.ToString(),
                local_path = localFile,
                remote_path = remotePath,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var content = await ContainerFileHelper.ReadFileFromContainer(_sftp.Container, remotePath);
            content.ShouldBe("upload content");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SftpDownload_FileDownloadedLocally()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var remoteFileName = $"test-{Guid.NewGuid():N}.txt";
            var remotePath = $"/home/testuser/download/{remoteFileName}";
            await ContainerFileHelper.WriteFileToContainer(_sftp.Container, remotePath, "download content");

            var localFile = Path.Combine(tempDir, "downloaded.txt");
            var connId = await TestDataSeeder.SeedSftpConnection(db, encryptor,
                _sftp.Host, _sftp.Port, _sftp.Username, _sftp.Password);

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "sftp.download", "Download", new
            {
                connection_id = connId.ToString(),
                remote_path = remotePath,
                local_path = localFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(localFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(localFile)).ShouldBe("download content");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SftpMkdir_DirectoryCreatedOnServer()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var connId = await TestDataSeeder.SeedSftpConnection(db, encryptor,
            _sftp.Host, _sftp.Port, _sftp.Username, _sftp.Password);

        var dirName = $"testdir-{Guid.NewGuid():N}";
        var remotePath = $"/home/testuser/upload/{dirName}";

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
        await TestDataSeeder.AddStep(db, jobId, 0, "sftp.mkdir", "Mkdir", new
        {
            connection_id = connId.ToString(),
            remote_path = remotePath,
        });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        // Verify directory exists via container exec
        var result = await _sftp.Container.ExecAsync(["test", "-d", remotePath]);
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task SftpRmdir_DirectoryRemovedFromServer()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var connId = await TestDataSeeder.SeedSftpConnection(db, encryptor,
            _sftp.Host, _sftp.Port, _sftp.Username, _sftp.Password);

        var dirName = $"testdir-{Guid.NewGuid():N}";
        var remotePath = $"/home/testuser/upload/{dirName}";

        // Create the directory first
        await _sftp.Container.ExecAsync(["mkdir", "-p", remotePath]);
        await _sftp.Container.ExecAsync(["chown", "testuser:users", remotePath]);

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
        await TestDataSeeder.AddStep(db, jobId, 0, "sftp.rmdir", "Rmdir", new
        {
            connection_id = connId.ToString(),
            remote_path = remotePath,
        });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        // Verify directory no longer exists
        var result = await _sftp.Container.ExecAsync(["test", "-d", remotePath]);
        result.ExitCode.ShouldNotBe(0);
    }

    [Fact]
    public async Task SftpList_ReturnsFileNames()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var listDir = $"/home/testuser/upload/listdir-{Guid.NewGuid():N}";
            await _sftp.Container.ExecAsync(["mkdir", "-p", listDir]);
            await _sftp.Container.ExecAsync(["chown", "testuser:users", listDir]);

            // Create two files in the directory
            await ContainerFileHelper.WriteFileToContainer(_sftp.Container, $"{listDir}/fileA.txt", "a");
            await ContainerFileHelper.WriteFileToContainer(_sftp.Container, $"{listDir}/fileB.txt", "b");

            var connId = await TestDataSeeder.SeedSftpConnection(db, encryptor,
                _sftp.Host, _sftp.Port, _sftp.Username, _sftp.Password);

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "sftp.list", "List", new
            {
                connection_id = connId.ToString(),
                remote_path = listDir,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
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
    public async Task SftpUploadThenDownload_RoundtripContentIdentical()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var originalContent = $"roundtrip-test-{Guid.NewGuid()}";
            var localUploadFile = Path.Combine(tempDir, "roundtrip-upload.txt");
            await File.WriteAllTextAsync(localUploadFile, originalContent);

            var connId = await TestDataSeeder.SeedSftpConnection(db, encryptor,
                _sftp.Host, _sftp.Port, _sftp.Username, _sftp.Password);

            var remotePath = $"/home/testuser/upload/roundtrip-{Guid.NewGuid():N}.txt";
            var localDownloadFile = Path.Combine(tempDir, "roundtrip-download.txt");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "sftp.upload", "Upload", new
            {
                connection_id = connId.ToString(),
                local_path = localUploadFile,
                remote_path = remotePath,
            });
            await TestDataSeeder.AddStep(db, jobId, 1, "sftp.download", "Download", new
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
    public async Task SftpUpload_InvalidConnectionId_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var localFile = Path.Combine(tempDir, "upload.txt");
            await File.WriteAllTextAsync(localFile, "should not be uploaded");

            var fakeConnId = Guid.CreateVersion7();
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "sftp.upload", "Upload", new
            {
                connection_id = fakeConnId.ToString(),
                local_path = localFile,
                remote_path = "/home/testuser/upload/should-not-exist.txt",
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

    [Fact]
    public async Task SftpUpload_WithContextRef_UsesOutputFromPriorStep()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var copiedFile = Path.Combine(tempDir, "copied.txt");
            await File.WriteAllTextAsync(sourceFile, "context ref content");

            var connId = await TestDataSeeder.SeedSftpConnection(db, encryptor,
                _sftp.Host, _sftp.Port, _sftp.Username, _sftp.Password);

            var remotePath = $"/home/testuser/upload/context-ref-{Guid.NewGuid():N}.txt";

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: file.copy — produces "copied_file" output
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy", new
            {
                source_path = sourceFile,
                destination_path = copiedFile,
            });

            // Step 1: sftp.upload — uses context:0.copied_file as local_path
            await TestDataSeeder.AddStep(db, jobId, 1, "sftp.upload", "Upload via context", new
            {
                connection_id = connId.ToString(),
                local_path = "context:0.copied_file",
                remote_path = remotePath,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var content = await ContainerFileHelper.ReadFileFromContainer(_sftp.Container, remotePath);
            content.ShouldBe("context ref content");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task SftpUpload_NonexistentLocalFile_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var nonexistentFile = Path.Combine(tempDir, "does-not-exist.txt");

            var connId = await TestDataSeeder.SeedSftpConnection(db, encryptor,
                _sftp.Host, _sftp.Port, _sftp.Username, _sftp.Password);

            var remotePath = $"/home/testuser/upload/should-not-exist-{Guid.NewGuid():N}.txt";
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "sftp.upload", "Upload", new
            {
                connection_id = connId.ToString(),
                local_path = nonexistentFile,
                remote_path = remotePath,
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
    public async Task SftpDownload_NonexistentRemoteFile_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var localFile = Path.Combine(tempDir, "downloaded.txt");

            var connId = await TestDataSeeder.SeedSftpConnection(db, encryptor,
                _sftp.Host, _sftp.Port, _sftp.Username, _sftp.Password);

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "sftp.download", "Download", new
            {
                connection_id = connId.ToString(),
                remote_path = "/home/testuser/upload/nonexistent-file-xxx.txt",
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

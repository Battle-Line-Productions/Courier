using System.IO.Compression;
using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.FileOps;

[Collection("EngineTests")]
[Trait("Category", "FileOps")]
public class FileUnzipEngineTests
{
    private readonly DatabaseFixture _database;
    public FileUnzipEngineTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task UnzipArchive_ExtractsContents()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a zip archive with known content
            var archivePath = Path.Combine(tempDir, "test.zip");
            var extractDir = Path.Combine(tempDir, "extracted");
            var stagingDir = Path.Combine(tempDir, "staging");
            Directory.CreateDirectory(stagingDir);
            await File.WriteAllTextAsync(Path.Combine(stagingDir, "inner.txt"), "extracted content");
            ZipFile.CreateFromDirectory(stagingDir, archivePath);

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.unzip", "Unzip Archive", new
            {
                archive_path = archivePath,
                output_directory = extractDir
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);
            Directory.Exists(extractDir).ShouldBeTrue();
            File.Exists(Path.Combine(extractDir, "inner.txt")).ShouldBeTrue();
            (await File.ReadAllTextAsync(Path.Combine(extractDir, "inner.txt"))).ShouldBe("extracted content");

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Completed);
            stepExec.OutputData.ShouldNotBeNull();
            stepExec.OutputData.ShouldContain("extracted_directory");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UnzipWithPassword_ExtractsContents()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // First, create a password-protected zip using the engine's zip step
            var sourceFile = Path.Combine(tempDir, "secret.txt");
            var archivePath = Path.Combine(tempDir, "encrypted.zip");
            var extractDir = Path.Combine(tempDir, "decrypted");
            await File.WriteAllTextAsync(sourceFile, "password protected");

            // Step 0: zip with password, Step 1: unzip with password
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.zip", "Zip With Password", new
            {
                source_path = sourceFile,
                output_path = archivePath,
                password = "secret123"
            });
            await TestDataSeeder.AddStep(db, jobId, 1, "file.unzip", "Unzip With Password", new
            {
                archive_path = archivePath,
                output_directory = extractDir,
                password = "secret123"
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);
            Directory.Exists(extractDir).ShouldBeTrue();

            var extractedFile = Path.Combine(extractDir, "secret.txt");
            File.Exists(extractedFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(extractedFile)).ShouldBe("password protected");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UnzipArchive_MultipleFiles_AllExtracted()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create archive with multiple files
            var stagingDir = Path.Combine(tempDir, "staging");
            Directory.CreateDirectory(stagingDir);
            await File.WriteAllTextAsync(Path.Combine(stagingDir, "a.txt"), "aaa");
            await File.WriteAllTextAsync(Path.Combine(stagingDir, "b.txt"), "bbb");
            await File.WriteAllTextAsync(Path.Combine(stagingDir, "c.txt"), "ccc");

            var archivePath = Path.Combine(tempDir, "multi.zip");
            ZipFile.CreateFromDirectory(stagingDir, archivePath);

            var extractDir = Path.Combine(tempDir, "extracted");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.unzip", "Unzip Multi", new
            {
                archive_path = archivePath,
                output_directory = extractDir
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(Path.Combine(extractDir, "a.txt")).ShouldBeTrue();
            File.Exists(Path.Combine(extractDir, "b.txt")).ShouldBeTrue();
            File.Exists(Path.Combine(extractDir, "c.txt")).ShouldBeTrue();
            (await File.ReadAllTextAsync(Path.Combine(extractDir, "a.txt"))).ShouldBe("aaa");
            (await File.ReadAllTextAsync(Path.Combine(extractDir, "b.txt"))).ShouldBe("bbb");
            (await File.ReadAllTextAsync(Path.Combine(extractDir, "c.txt"))).ShouldBe("ccc");

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Completed);
            stepExec.OutputData.ShouldNotBeNull();
            stepExec.OutputData.ShouldContain("extracted_directory");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task UnzipArchive_CorruptedFile_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var archivePath = Path.Combine(tempDir, "corrupt.zip");
            var extractDir = Path.Combine(tempDir, "extracted");
            await File.WriteAllBytesAsync(archivePath, new byte[] { 0x50, 0x4B, 0x00, 0x00, 0xFF, 0xFF, 0xFF });

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.unzip", "Unzip Corrupt", new
            {
                archive_path = archivePath,
                output_directory = extractDir
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Failed);
            stepExec.ErrorMessage.ShouldNotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task UnzipArchive_WrongPassword_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // First create a password-protected zip using the engine
            var sourceFile = Path.Combine(tempDir, "secret.txt");
            var archivePath = Path.Combine(tempDir, "encrypted.zip");
            var extractDir = Path.Combine(tempDir, "decrypted");
            await File.WriteAllTextAsync(sourceFile, "secret data");

            var (setupJobId, setupExecutionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, setupJobId, 0, "file.zip", "Zip With Password", new
            {
                source_path = sourceFile,
                output_path = archivePath,
                password = "correct-password"
            });

            await engine.ExecuteAsync(setupExecutionId, CancellationToken.None);

            var setupExecution = await db.JobExecutions.FirstAsync(e => e.Id == setupExecutionId);
            setupExecution.State.ShouldBe(JobExecutionState.Completed);
            File.Exists(archivePath).ShouldBeTrue();

            // Now try to unzip with wrong password
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.unzip", "Unzip Wrong Password", new
            {
                archive_path = archivePath,
                output_directory = extractDir,
                password = "wrong-password"
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Failed);
            stepExec.ErrorMessage.ShouldNotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

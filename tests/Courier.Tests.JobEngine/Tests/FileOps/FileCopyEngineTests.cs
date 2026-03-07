using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.FileOps;

[Collection("EngineTests")]
[Trait("Category", "FileOps")]
public class FileCopyEngineTests
{
    private readonly DatabaseFixture _database;
    public FileCopyEngineTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task CopyFile_DestinationCreated_WithCorrectContent()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "hello world");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);
            File.Exists(destFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(destFile)).ShouldBe("hello world");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CopyFile_StepExecution_ContainsCopiedFileOutput()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "copied.txt");
            await File.WriteAllTextAsync(sourceFile, "output test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Completed);
            stepExec.OutputData.ShouldNotBeNull();
            stepExec.OutputData.ShouldContain("copied_file");
            stepExec.OutputData.ShouldContain(destFile.Replace("\\", "\\\\"));
            stepExec.BytesProcessed.ShouldNotBeNull();
            stepExec.BytesProcessed.Value.ShouldBeGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CopyFile_SourceNotFound_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var missingSource = Path.Combine(tempDir, "does-not-exist.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Missing", new
            {
                source_path = missingSource,
                destination_path = destFile
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Failed);
            stepExec.ErrorMessage.ShouldNotBeNull();
            stepExec.ErrorMessage.ShouldContain("not found");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CopyFile_CreatesDestinationDirectory_WhenMissing()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "subdir", "nested", "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "nested copy");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Nested", new
            {
                source_path = sourceFile,
                destination_path = destFile
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);
            File.Exists(destFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(destFile)).ShouldBe("nested copy");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task CopyFile_EmptyFile_SucceedsWithZeroBytes()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "empty.txt");
            var destFile = Path.Combine(tempDir, "empty-copy.txt");
            File.Create(sourceFile).Dispose();

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Empty", new
            {
                source_path = sourceFile,
                destination_path = destFile
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);
            File.Exists(destFile).ShouldBeTrue();
            new FileInfo(destFile).Length.ShouldBe(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task CopyFile_OverwriteExistingDestination_Succeeds()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "new content");
            await File.WriteAllTextAsync(destFile, "old content");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Overwrite", new
            {
                source_path = sourceFile,
                destination_path = destFile
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);
            (await File.ReadAllTextAsync(destFile)).ShouldBe("new content");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

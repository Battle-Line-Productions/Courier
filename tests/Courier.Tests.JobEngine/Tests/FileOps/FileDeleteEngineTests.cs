using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.FileOps;

[Collection("EngineTests")]
[Trait("Category", "FileOps")]
public class FileDeleteEngineTests
{
    private readonly DatabaseFixture _database;
    public FileDeleteEngineTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task DeleteFile_FileRemoved_ExecutionCompletes()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var targetFile = Path.Combine(tempDir, "to-delete.txt");
            await File.WriteAllTextAsync(targetFile, "delete me");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.delete", "Delete File", new
            {
                path = targetFile
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);
            File.Exists(targetFile).ShouldBeFalse();

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Completed);
            stepExec.OutputData.ShouldNotBeNull();
            stepExec.OutputData.ShouldContain("deleted_file");
            stepExec.BytesProcessed.ShouldNotBeNull();
            stepExec.BytesProcessed.Value.ShouldBeGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DeleteFile_NotFound_DefaultBehavior_Succeeds()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var missingFile = Path.Combine(tempDir, "nonexistent.txt");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.delete", "Delete Missing", new
            {
                path = missingFile
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DeleteFile_NotFound_FailIfNotFoundTrue_Fails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var missingFile = Path.Combine(tempDir, "nonexistent.txt");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.delete", "Delete Missing Strict", new
            {
                path = missingFile,
                fail_if_not_found = true
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
    [Trait("Category", "EdgeCase")]
    public async Task DeleteFile_DirectoryPath_WithFailIfNotFound_Fails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a subdirectory (not a file)
            var subDir = Path.Combine(tempDir, "subdir");
            Directory.CreateDirectory(subDir);

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.delete", "Delete Dir", new
            {
                path = subDir,
                fail_if_not_found = true
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
    public async Task DeleteFile_NotFound_FailIfNotFoundFalse_Succeeds()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var missingFile = Path.Combine(tempDir, "nonexistent.txt");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);
            await TestDataSeeder.AddStep(db, jobId, 0, "file.delete", "Delete Missing Lenient", new
            {
                path = missingFile,
                fail_if_not_found = false
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

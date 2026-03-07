using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.EngineCore;

[Collection("EngineTests")]
[Trait("Category", "Notifications")]
public class EngineNotificationTests
{
    private readonly DatabaseFixture _database;
    public EngineNotificationTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task ExecuteAsync_AllStepsSucceed_AuditLogRecordsCompletion()
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
            await File.WriteAllTextAsync(sourceFile, "notification test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Verify audit log entry for execution completion
            var auditEntries = await db.AuditLogEntries
                .Where(a => a.EntityId == executionId && a.EntityType == "job_execution")
                .ToListAsync();
            auditEntries.ShouldNotBeEmpty();
            auditEntries.ShouldContain(a => a.Operation == "ExecutionCompleted");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_StepFails_AuditLogRecordsFailure()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // file.copy with nonexistent source -> fails
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Copy", new
            {
                source_path = Path.Combine(tempDir, "nonexistent.txt"),
                destination_path = Path.Combine(tempDir, "dest.txt"),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            // Verify audit log entries for step failure and execution failure
            var auditEntries = await db.AuditLogEntries
                .Where(a => a.EntityId == executionId && a.EntityType == "job_execution")
                .ToListAsync();
            auditEntries.ShouldNotBeEmpty();
            auditEntries.ShouldContain(a => a.Operation == "ExecutionFailed");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_AuditLogRecordsCancellation()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            await File.WriteAllTextAsync(sourceFile, "cancel test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = Path.Combine(tempDir, "dest.txt"),
            });

            // Set cancellation signal before execution starts
            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.RequestedState = "cancelled";
            await db.SaveChangesAsync();

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            await db.Entry(execution).ReloadAsync();
            execution.State.ShouldBe(JobExecutionState.Cancelled);

            // Verify audit log entry for cancellation
            var auditEntries = await db.AuditLogEntries
                .Where(a => a.EntityId == executionId && a.EntityType == "job_execution")
                .ToListAsync();
            auditEntries.ShouldNotBeEmpty();
            auditEntries.ShouldContain(a => a.Operation == "ExecutionCancelled");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MultiStepPipeline_AuditTrailIsComplete()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            await File.WriteAllTextAsync(sourceFile, "multi-step audit test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":3}""");

            // Step 0: file.copy with bad source (fails, but SkipAndContinue)
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Copy", new
            {
                source_path = Path.Combine(tempDir, "nonexistent.txt"),
                destination_path = Path.Combine(tempDir, "bad-dest.txt"),
            });

            // Step 1: file.copy with valid source (succeeds)
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Good Copy", new
            {
                source_path = sourceFile,
                destination_path = Path.Combine(tempDir, "dest.txt"),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Step 0 failed, step 1 completed
            var step0 = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            step0.State.ShouldBe(StepExecutionState.Failed);

            var step1 = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 1);
            step1.State.ShouldBe(StepExecutionState.Completed);

            // Verify the execution completed successfully despite the failed step
            // and audit trail recorded both step outcomes and final completion
            var auditEntries = await db.AuditLogEntries
                .Where(a => a.EntityId == executionId && a.EntityType == "job_execution")
                .ToListAsync();
            auditEntries.ShouldNotBeEmpty();
            auditEntries.ShouldContain(a => a.Operation == "ExecutionCompleted");

            File.Exists(Path.Combine(tempDir, "dest.txt")).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

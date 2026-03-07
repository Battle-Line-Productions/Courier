using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.FailurePolicy;

[Collection("EngineTests")]
[Trait("Category", "FailurePolicy")]
public class StepTimeoutTests
{
    private readonly DatabaseFixture _database;
    public StepTimeoutTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task Step_CompletesWithinTimeout_Succeeds()
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
            await File.WriteAllTextAsync(sourceFile, "timeout test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step with generous timeout — should complete well within limit
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File",
                new
                {
                    source_path = sourceFile,
                    destination_path = destFile,
                },
                timeoutSeconds: 60);

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Completed);
            stepExec.DurationMs.ShouldNotBeNull();
            stepExec.DurationMs.Value.ShouldBeGreaterThanOrEqualTo(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Step_DurationMs_IsRecorded()
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
            await File.WriteAllTextAsync(sourceFile, "duration tracking test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Completed);
            stepExec.StartedAt.ShouldNotBeNull();
            stepExec.CompletedAt.ShouldNotBeNull();
            stepExec.DurationMs.ShouldNotBeNull();
            stepExec.CompletedAt.Value.ShouldBeGreaterThanOrEqualTo(stepExec.StartedAt.Value);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Step_ExceedsTimeout_MarkedAsFailed_WithTimeoutMessage()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new SlowTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // SlowTestStep with 5s delay but only 1s timeout
        await TestDataSeeder.AddStep(db, jobId, 0, "test.slow", "Slow Step",
            new { delay_ms = 5000 },
            timeoutSeconds: 1);

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Failed);

        var stepExec = await db.StepExecutions
            .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
        stepExec.State.ShouldBe(StepExecutionState.Failed);
        stepExec.ErrorMessage.ShouldNotBeNull();
        stepExec.ErrorMessage.ShouldContain("timed out");
    }

    [Fact]
    public async Task Step_ExceedsTimeout_SkipAndContinue_NextStepRuns()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new SlowTestStep())
            .Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "timeout skip test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":3}""");

            // Step 0: SlowTestStep with timeout that will be exceeded
            await TestDataSeeder.AddStep(db, jobId, 0, "test.slow", "Slow Step",
                new { delay_ms = 5000 },
                timeoutSeconds: 1);

            // Step 1: file.copy with valid source — should still run
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Good Copy", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Step 0 should be failed (timeout)
            var step0Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            step0Exec.State.ShouldBe(StepExecutionState.Failed);
            step0Exec.ErrorMessage.ShouldNotBeNull();
            step0Exec.ErrorMessage.ShouldContain("timed out");

            // Step 1 should have completed successfully
            var step1Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 1);
            step1Exec.State.ShouldBe(StepExecutionState.Completed);

            File.Exists(destFile).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Step_ExceedsTimeout_StopPolicy_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new SlowTestStep())
            .Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "timeout stop test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":"stop"}""");

            // Step 0: SlowTestStep with timeout that will be exceeded
            await TestDataSeeder.AddStep(db, jobId, 0, "test.slow", "Slow Step",
                new { delay_ms = 5000 },
                timeoutSeconds: 1);

            // Step 1: file.copy — should never run
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Good Copy", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            // Step 0 should be failed (timeout)
            var step0Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            step0Exec.State.ShouldBe(StepExecutionState.Failed);
            step0Exec.ErrorMessage.ShouldNotBeNull();
            step0Exec.ErrorMessage.ShouldContain("timed out");

            // Step 1 should not have been executed
            var step1Execs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .ToListAsync();
            step1Execs.Count.ShouldBe(0);

            File.Exists(destFile).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

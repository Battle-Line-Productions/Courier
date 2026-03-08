using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.EngineCore;

[Collection("EngineTests")]
[Trait("Category", "RetryStep")]
public class RetryStepTests
{
    private readonly DatabaseFixture _database;
    public RetryStepTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task RetryStep_FailsOnceThenSucceeds_ExecutionCompleted()
    {
        // Arrange
        CountdownTestStep.ResetCounters();
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new CountdownTestStep())
            .Build();

        var stepId = Guid.NewGuid().ToString("N");
        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":1,"max_retries":3,"backoff_base_seconds":0}""");

        await TestDataSeeder.AddStep(db, jobId, 0, "test.countdown", "Countdown Step", new
        {
            id = stepId,
            fail_count = 1,
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert
        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);
    }

    [Fact]
    public async Task RetryStep_ExhaustsAllRetries_ExecutionFailed()
    {
        // Arrange
        CountdownTestStep.ResetCounters();
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new CountdownTestStep())
            .Build();

        var stepId = Guid.NewGuid().ToString("N");
        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":1,"max_retries":2,"backoff_base_seconds":0}""");

        // Will fail 10 times total, but only 2 retries allowed (3 attempts total)
        await TestDataSeeder.AddStep(db, jobId, 0, "test.countdown", "Always Fail Step", new
        {
            id = stepId,
            fail_count = 10,
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert
        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Failed);
    }

    [Fact]
    public async Task RetryStep_CreatesNewStepExecutionPerAttempt()
    {
        // Arrange
        CountdownTestStep.ResetCounters();
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new CountdownTestStep())
            .Build();

        var stepId = Guid.NewGuid().ToString("N");
        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":1,"max_retries":3,"backoff_base_seconds":0}""");

        // Fails twice, succeeds on 3rd attempt (initial + 2 retries)
        await TestDataSeeder.AddStep(db, jobId, 0, "test.countdown", "Retry Step", new
        {
            id = stepId,
            fail_count = 2,
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert - should have 3 step executions: initial failure + retry failure + retry success
        var stepExecs = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 0)
            .OrderBy(se => se.RetryAttempt)
            .ToListAsync();

        stepExecs.Count.ShouldBe(3);
        stepExecs[0].RetryAttempt.ShouldBe(0);
        stepExecs[0].State.ShouldBe(StepExecutionState.Failed);
        stepExecs[1].RetryAttempt.ShouldBe(1);
        stepExecs[1].State.ShouldBe(StepExecutionState.Failed);
        stepExecs[2].RetryAttempt.ShouldBe(2);
        stepExecs[2].State.ShouldBe(StepExecutionState.Completed);
    }

    [Fact]
    public async Task RetryStep_MaxRetriesZero_FailsImmediately()
    {
        // Arrange
        CountdownTestStep.ResetCounters();
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new CountdownTestStep())
            .Build();

        var stepId = Guid.NewGuid().ToString("N");
        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":1,"max_retries":0,"backoff_base_seconds":0}""");

        await TestDataSeeder.AddStep(db, jobId, 0, "test.countdown", "No Retry Step", new
        {
            id = stepId,
            fail_count = 1,
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert
        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Failed);

        // Only 1 step execution (the initial failure), no retries
        var stepExecs = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 0)
            .ToListAsync();
        stepExecs.Count.ShouldBe(1);
        stepExecs[0].RetryAttempt.ShouldBe(0);
        stepExecs[0].State.ShouldBe(StepExecutionState.Failed);
    }

    [Fact]
    public async Task RetryStep_SubsequentStepsExecuteAfterSuccessfulRetry()
    {
        // Arrange
        CountdownTestStep.ResetCounters();
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "retry then continue");

            var engine = new JobEngineBuilder(db, encryptor)
                .WithAdditionalSteps(new CountdownTestStep())
                .Build();

            var stepId = Guid.NewGuid().ToString("N");
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":1,"max_retries":3,"backoff_base_seconds":0}""");

            // Step 0: fails once, succeeds on retry
            await TestDataSeeder.AddStep(db, jobId, 0, "test.countdown", "Retry Step", new
            {
                id = stepId,
                fail_count = 1,
            });

            // Step 1: file.copy should run after step 0 succeeds
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy Step", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Act
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Assert
            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(destFile).ShouldBeTrue();

            var step1Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 1);
            step1Exec.State.ShouldBe(StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RetryStep_RetryAttemptStoredOnStepExecution()
    {
        // Arrange
        CountdownTestStep.ResetCounters();
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new CountdownTestStep())
            .Build();

        var stepId = Guid.NewGuid().ToString("N");
        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":1,"max_retries":3,"backoff_base_seconds":0}""");

        // Fails 3 times, succeeds on 4th (3 retries = attempt indices 1, 2, 3)
        await TestDataSeeder.AddStep(db, jobId, 0, "test.countdown", "Retry Step", new
        {
            id = stepId,
            fail_count = 3,
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert
        var stepExecs = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 0)
            .OrderBy(se => se.RetryAttempt)
            .ToListAsync();

        stepExecs.Count.ShouldBe(4);
        stepExecs[0].RetryAttempt.ShouldBe(0);
        stepExecs[1].RetryAttempt.ShouldBe(1);
        stepExecs[2].RetryAttempt.ShouldBe(2);
        stepExecs[3].RetryAttempt.ShouldBe(3);
    }

    [Fact]
    public async Task RetryStep_OnFirstStepOfMultiStepJob_CompletesSuccessfully()
    {
        // Arrange
        CountdownTestStep.ResetCounters();
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile1 = Path.Combine(tempDir, "dest1.txt");
            var destFile2 = Path.Combine(tempDir, "dest2.txt");
            await File.WriteAllTextAsync(sourceFile, "multi-step retry test");

            var engine = new JobEngineBuilder(db, encryptor)
                .WithAdditionalSteps(new CountdownTestStep())
                .Build();

            var stepId = Guid.NewGuid().ToString("N");
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":1,"max_retries":2,"backoff_base_seconds":0}""");

            // Step 0: countdown (fails once, succeeds on retry)
            await TestDataSeeder.AddStep(db, jobId, 0, "test.countdown", "Retry First", new
            {
                id = stepId,
                fail_count = 1,
            });

            // Step 1: file.copy
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy 1", new
            {
                source_path = sourceFile,
                destination_path = destFile1,
            });

            // Step 2: file.copy
            await TestDataSeeder.AddStep(db, jobId, 2, "file.copy", "Copy 2", new
            {
                source_path = sourceFile,
                destination_path = destFile2,
            });

            // Act
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Assert
            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(destFile1).ShouldBeTrue();
            File.Exists(destFile2).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RetryStep_OnLastStepOfMultiStepJob_CompletesSuccessfully()
    {
        // Arrange
        CountdownTestStep.ResetCounters();
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "last step retry test");

            var engine = new JobEngineBuilder(db, encryptor)
                .WithAdditionalSteps(new CountdownTestStep())
                .Build();

            var stepId = Guid.NewGuid().ToString("N");
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":1,"max_retries":2,"backoff_base_seconds":0}""");

            // Step 0: file.copy (succeeds)
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Step", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Step 1: countdown (fails once, succeeds on retry) — last step
            await TestDataSeeder.AddStep(db, jobId, 1, "test.countdown", "Retry Last", new
            {
                id = stepId,
                fail_count = 1,
            });

            // Act
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Assert
            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(destFile).ShouldBeTrue();

            // Step 1 should have 2 executions: 1 failed + 1 succeeded
            var step1Execs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .OrderBy(se => se.RetryAttempt)
                .ToListAsync();
            step1Execs.Count.ShouldBe(2);
            step1Execs[0].State.ShouldBe(StepExecutionState.Failed);
            step1Execs[1].State.ShouldBe(StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RetryStep_OutputFromSuccessfulRetryIsUsed()
    {
        // Arrange
        CountdownTestStep.ResetCounters();
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new CountdownTestStep())
            .Build();

        var stepId = Guid.NewGuid().ToString("N");
        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":1,"max_retries":3,"backoff_base_seconds":0}""");

        // Fails once, succeeds on retry
        await TestDataSeeder.AddStep(db, jobId, 0, "test.countdown", "Retry Step", new
        {
            id = stepId,
            fail_count = 1,
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert — the successful retry step execution should have output data
        var successExec = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 0 && se.State == StepExecutionState.Completed)
            .FirstAsync();

        successExec.OutputData.ShouldNotBeNull();
        successExec.OutputData.ShouldContain("success-after-1-failures");
    }

    [Fact]
    public async Task RetryStep_ExhaustsRetries_AllAttemptStepExecutionsRecorded()
    {
        // Arrange
        CountdownTestStep.ResetCounters();
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new CountdownTestStep())
            .Build();

        var stepId = Guid.NewGuid().ToString("N");
        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":1,"max_retries":2,"backoff_base_seconds":0}""");

        // Always fails (fail_count > max attempts)
        await TestDataSeeder.AddStep(db, jobId, 0, "test.countdown", "Always Fails", new
        {
            id = stepId,
            fail_count = 100,
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert — 3 total step executions: 1 initial + 2 retries, all failed
        var stepExecs = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 0)
            .OrderBy(se => se.RetryAttempt)
            .ToListAsync();

        stepExecs.Count.ShouldBe(3);
        stepExecs.ShouldAllBe(se => se.State == StepExecutionState.Failed);
        stepExecs.ShouldAllBe(se => se.ErrorMessage != null);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Failed);
    }

    [Fact]
    public async Task RetryStep_FailedStepBlocksSubsequentSteps()
    {
        // Arrange
        CountdownTestStep.ResetCounters();
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "should not copy");

            var engine = new JobEngineBuilder(db, encryptor)
                .WithAdditionalSteps(new CountdownTestStep())
                .Build();

            var stepId = Guid.NewGuid().ToString("N");
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":1,"max_retries":1,"backoff_base_seconds":0}""");

            // Step 0: always fails (exhaust retries)
            await TestDataSeeder.AddStep(db, jobId, 0, "test.countdown", "Always Fail", new
            {
                id = stepId,
                fail_count = 100,
            });

            // Step 1: should never execute
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy Step", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Act
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Assert
            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

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

    [Fact]
    public async Task RetryStep_FailsTwiceThenSucceeds_CorrectDurationTracked()
    {
        // Arrange
        CountdownTestStep.ResetCounters();
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new CountdownTestStep())
            .Build();

        var stepId = Guid.NewGuid().ToString("N");
        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":1,"max_retries":3,"backoff_base_seconds":0}""");

        await TestDataSeeder.AddStep(db, jobId, 0, "test.countdown", "Duration Step", new
        {
            id = stepId,
            fail_count = 2,
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert — all step executions should have non-null duration
        var stepExecs = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 0)
            .ToListAsync();

        stepExecs.Count.ShouldBe(3);
        stepExecs.ShouldAllBe(se => se.DurationMs != null && se.DurationMs >= 0);
        stepExecs.ShouldAllBe(se => se.CompletedAt != null);
    }
}

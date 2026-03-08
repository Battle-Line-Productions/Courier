using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.EngineCore;

[Collection("EngineTests")]
[Trait("Category", "RetryJob")]
public class RetryJobTests
{
    private readonly DatabaseFixture _database;
    public RetryJobTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task RetryJob_StepFails_NewQueuedExecutionCreated()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":2,"max_retries":3}""");

        await TestDataSeeder.AddStep(db, jobId, 0, "test.fail", "Failing Step", new
        {
            message = "trigger retry job",
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert — original execution marked as failed
        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Failed);

        // A new queued execution should exist for the same job
        var retryExecution = await db.JobExecutions
            .FirstOrDefaultAsync(e => e.JobId == jobId && e.Id != executionId);
        retryExecution.ShouldNotBeNull();
        retryExecution!.State.ShouldBe(JobExecutionState.Queued);
        retryExecution.RetryAttempt.ShouldBe(1);
    }

    [Fact]
    public async Task RetryJob_RetriesUpToMaxThenStaysFailed()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":2,"max_retries":2}""");

        await TestDataSeeder.AddStep(db, jobId, 0, "test.fail", "Failing Step", new
        {
            message = "exhaust retries",
        });

        // Act — Run the initial execution
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // The engine creates a retry execution (attempt 1). Run it.
        var attempt1 = await db.JobExecutions
            .FirstAsync(e => e.JobId == jobId && e.RetryAttempt == 1);
        attempt1.State = JobExecutionState.Running;
        attempt1.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var engine2 = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();
        await engine2.ExecuteAsync(attempt1.Id, CancellationToken.None);

        // The engine creates a retry execution (attempt 2). Run it.
        var attempt2 = await db.JobExecutions
            .FirstAsync(e => e.JobId == jobId && e.RetryAttempt == 2);
        attempt2.State = JobExecutionState.Running;
        attempt2.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var engine3 = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();
        await engine3.ExecuteAsync(attempt2.Id, CancellationToken.None);

        // Assert — attempt 2 is at the max, so no further retry should be created
        await db.Entry(attempt2).ReloadAsync();
        attempt2.State.ShouldBe(JobExecutionState.Failed);

        var furtherRetries = await db.JobExecutions
            .Where(e => e.JobId == jobId && e.RetryAttempt > 2)
            .ToListAsync();
        furtherRetries.Count.ShouldBe(0);
    }

    [Fact]
    public async Task RetryJob_NewExecutionHasCorrectJobId()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":2,"max_retries":1}""");

        await TestDataSeeder.AddStep(db, jobId, 0, "test.fail", "Failing Step", new
        {
            message = "retry job id check",
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert
        var retryExecution = await db.JobExecutions
            .FirstAsync(e => e.JobId == jobId && e.Id != executionId);
        retryExecution.JobId.ShouldBe(jobId);
    }

    [Fact]
    public async Task RetryJob_NewExecutionStartsFromStepOne()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "retry from start");

            var engine = new JobEngineBuilder(db, encryptor)
                .WithAdditionalSteps(new FailingTestStep())
                .Build();

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":2,"max_retries":1}""");

            // Step 0: file.copy succeeds
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Step", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Step 1: always fails
            await TestDataSeeder.AddStep(db, jobId, 1, "test.fail", "Fail Step", new
            {
                message = "force retry",
            });

            // Act — Run original execution
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Get the retry execution
            var retryExec = await db.JobExecutions
                .FirstAsync(e => e.JobId == jobId && e.RetryAttempt == 1);

            // Clean up dest to verify step 0 re-runs
            if (File.Exists(destFile)) File.Delete(destFile);

            retryExec.State = JobExecutionState.Running;
            retryExec.StartedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var engine2 = new JobEngineBuilder(db, encryptor)
                .WithAdditionalSteps(new FailingTestStep())
                .Build();
            await engine2.ExecuteAsync(retryExec.Id, CancellationToken.None);

            // Assert — step 0 ran again in the retry execution (file was re-created)
            File.Exists(destFile).ShouldBeTrue();

            // Retry execution should have step 0 completed (re-ran from start)
            var retryStep0 = await db.StepExecutions
                .FirstOrDefaultAsync(se => se.JobExecutionId == retryExec.Id && se.StepOrder == 0);
            retryStep0.ShouldNotBeNull();
            retryStep0!.State.ShouldBe(StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RetryJob_MaxRetriesOne_AllowsExactlyOneRetry()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":2,"max_retries":1}""");

        await TestDataSeeder.AddStep(db, jobId, 0, "test.fail", "Failing Step", new
        {
            message = "max retries 1",
        });

        // Act — Run initial execution
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Get the retry execution (attempt 1)
        var retryExec = await db.JobExecutions
            .FirstAsync(e => e.JobId == jobId && e.RetryAttempt == 1);
        retryExec.State = JobExecutionState.Running;
        retryExec.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var engine2 = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();
        await engine2.ExecuteAsync(retryExec.Id, CancellationToken.None);

        // Assert — retry attempt 1 has reached max, no more retries
        await db.Entry(retryExec).ReloadAsync();
        retryExec.State.ShouldBe(JobExecutionState.Failed);

        var allExecutions = await db.JobExecutions
            .Where(e => e.JobId == jobId)
            .OrderBy(e => e.RetryAttempt)
            .ToListAsync();

        // Exactly 2 executions: original (attempt 0/null) + retry (attempt 1)
        allExecutions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RetryJob_OriginalFailedExecutionPreservedInHistory()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":2,"max_retries":1}""");

        await TestDataSeeder.AddStep(db, jobId, 0, "test.fail", "Failing Step", new
        {
            message = "preserve history",
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert — original execution still exists and is marked as failed
        var originalExec = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        originalExec.State.ShouldBe(JobExecutionState.Failed);
        originalExec.CompletedAt.ShouldNotBeNull();

        // Original step execution is preserved
        var originalStepExecs = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId)
            .ToListAsync();
        originalStepExecs.Count.ShouldBeGreaterThan(0);
        originalStepExecs[0].State.ShouldBe(StepExecutionState.Failed);
    }

    [Fact]
    public async Task RetryJob_RetryAttemptIncrementedCorrectly()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":2,"max_retries":3}""");

        await TestDataSeeder.AddStep(db, jobId, 0, "test.fail", "Failing Step", new
        {
            message = "retry increment test",
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert
        var retryExec = await db.JobExecutions
            .FirstAsync(e => e.JobId == jobId && e.Id != executionId);
        retryExec.RetryAttempt.ShouldBe(1);
        retryExec.TriggeredBy.ShouldStartWith("retry:");
        retryExec.TriggeredBy.ShouldContain(executionId.ToString());
    }

    [Fact]
    public async Task RetryJob_TriggeredByContainsOriginalExecutionId()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":2,"max_retries":1}""");

        await TestDataSeeder.AddStep(db, jobId, 0, "test.fail", "Failing Step", new
        {
            message = "triggered by check",
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert
        var retryExec = await db.JobExecutions
            .FirstAsync(e => e.JobId == jobId && e.RetryAttempt == 1);
        retryExec.TriggeredBy.ShouldBe($"retry:{executionId}");
    }

    [Fact]
    public async Task RetryJob_MixedWithRetryStep_StepRetriesFirst()
    {
        // Arrange — a job with RetryStep policy where step always fails.
        // When step retries exhaust, the overall failure behavior (stop + abort) applies,
        // NOT RetryJob. This tests that RetryStep and RetryJob are separate policies.
        CountdownTestStep.ResetCounters();
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new CountdownTestStep())
            .Build();

        var stepId = Guid.NewGuid().ToString("N");
        // Using RetryStep policy (type=1)
        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":1,"max_retries":2,"backoff_base_seconds":0}""");

        // Fails 1 time, succeeds on 2nd attempt (first retry)
        await TestDataSeeder.AddStep(db, jobId, 0, "test.countdown", "Retry Step", new
        {
            id = stepId,
            fail_count = 1,
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert — step retry succeeded, so job completes. No RetryJob queued.
        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        var retryExecutions = await db.JobExecutions
            .Where(e => e.JobId == jobId && e.Id != executionId)
            .ToListAsync();
        retryExecutions.Count.ShouldBe(0);
    }
}

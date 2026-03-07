using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.FailurePolicy;

[Collection("EngineTests")]
[Trait("Category", "FailurePolicy")]
public class FailurePolicyTests
{
    private readonly DatabaseFixture _database;
    public FailurePolicyTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task StopOnFirstFailure_AbortsPipeline_ExecutionFailed()
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
            await File.WriteAllTextAsync(sourceFile, "failure test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":"stop","max_retries":0}""");

            // Step 0: file.copy with nonexistent source → fails
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Copy", new
            {
                source_path = Path.Combine(tempDir, "nonexistent.txt"),
                destination_path = destFile,
            });

            // Step 1: file.copy with valid source → should never run
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Good Copy", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            // Step 0 should be failed
            var step0Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            step0Exec.State.ShouldBe(StepExecutionState.Failed);
            step0Exec.ErrorMessage.ShouldNotBeNull();

            // Step 1 should not have been executed at all
            var step1Execs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .ToListAsync();
            step1Execs.Count.ShouldBe(0);

            // Destination file was not created
            File.Exists(destFile).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SkipAndContinue_StepFails_NextStepStillRuns()
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
            await File.WriteAllTextAsync(sourceFile, "skip test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":3}""");

            // Step 0: file.copy with nonexistent source → fails
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Copy", new
            {
                source_path = Path.Combine(tempDir, "nonexistent.txt"),
                destination_path = Path.Combine(tempDir, "bad-dest.txt"),
            });

            // Step 1: file.copy with valid source → should still run
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Good Copy", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            // With SkipAndContinue, execution continues and final state is Completed
            // because AllSucceeded stays true (the abort path that sets it false is skipped)
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Step 0 should be failed
            var step0Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            step0Exec.State.ShouldBe(StepExecutionState.Failed);
            step0Exec.ErrorMessage.ShouldNotBeNull();
            step0Exec.ErrorMessage.ShouldContain("not found");

            // Step 1 should have completed successfully
            var step1Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 1);
            step1Exec.State.ShouldBe(StepExecutionState.Completed);

            // Destination file was created by step 1
            File.Exists(destFile).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task StopPolicy_DefaultWhenNoPolicy_StopsOnFailure()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            await File.WriteAllTextAsync(sourceFile, "default policy test");

            // Default failure policy (no explicit policy — defaults to Stop)
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: fails
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Copy", new
            {
                source_path = Path.Combine(tempDir, "missing.txt"),
                destination_path = Path.Combine(tempDir, "out.txt"),
            });

            // Step 1: should not run
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Good Copy", new
            {
                source_path = sourceFile,
                destination_path = Path.Combine(tempDir, "out2.txt"),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            var step1Execs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .ToListAsync();
            step1Execs.Count.ShouldBe(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task SkipAndContinue_MultipleStepsFail_AllRecorded()
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
            await File.WriteAllTextAsync(sourceFile, "multi-fail test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":3}""");

            // Step 0: file.copy with missing source → fails
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Copy 1", new
            {
                source_path = Path.Combine(tempDir, "missing1.txt"),
                destination_path = Path.Combine(tempDir, "bad-dest1.txt"),
            });

            // Step 1: file.copy with missing source → fails
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Bad Copy 2", new
            {
                source_path = Path.Combine(tempDir, "missing2.txt"),
                destination_path = Path.Combine(tempDir, "bad-dest2.txt"),
            });

            // Step 2: file.copy with valid source → succeeds
            await TestDataSeeder.AddStep(db, jobId, 2, "file.copy", "Good Copy", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var step0Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            step0Exec.State.ShouldBe(StepExecutionState.Failed);

            var step1Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 1);
            step1Exec.State.ShouldBe(StepExecutionState.Failed);

            var step2Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 2);
            step2Exec.State.ShouldBe(StepExecutionState.Completed);

            File.Exists(destFile).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task SkipAndContinue_AllStepsFail_ExecutionStillCompletes()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":3}""");

            // Step 0: file.copy with missing source → fails
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Copy 1", new
            {
                source_path = Path.Combine(tempDir, "missing1.txt"),
                destination_path = Path.Combine(tempDir, "bad-dest1.txt"),
            });

            // Step 1: file.copy with missing source → fails
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Bad Copy 2", new
            {
                source_path = Path.Combine(tempDir, "missing2.txt"),
                destination_path = Path.Combine(tempDir, "bad-dest2.txt"),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var step0Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            step0Exec.State.ShouldBe(StepExecutionState.Failed);

            var step1Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 1);
            step1Exec.State.ShouldBe(StepExecutionState.Failed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RetryStepPolicy_BehavesAsStop_ExecutionAborts()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            await File.WriteAllTextAsync(sourceFile, "retry step test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":1}""");

            // Step 0: file.copy with nonexistent source -> fails
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Copy", new
            {
                source_path = Path.Combine(tempDir, "nonexistent.txt"),
                destination_path = Path.Combine(tempDir, "dest0.txt"),
            });

            // Step 1: file.copy with valid source -> should not run
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Good Copy", new
            {
                source_path = sourceFile,
                destination_path = Path.Combine(tempDir, "dest1.txt"),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            var step0Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            step0Exec.State.ShouldBe(StepExecutionState.Failed);

            var step1Execs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .ToListAsync();
            step1Execs.Count.ShouldBe(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RetryJobPolicy_BehavesAsStop_ExecutionAborts()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            await File.WriteAllTextAsync(sourceFile, "retry job test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":2}""");

            // Step 0: file.copy with nonexistent source -> fails
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Copy", new
            {
                source_path = Path.Combine(tempDir, "nonexistent.txt"),
                destination_path = Path.Combine(tempDir, "dest0.txt"),
            });

            // Step 1: file.copy with valid source -> should not run
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Good Copy", new
            {
                source_path = sourceFile,
                destination_path = Path.Combine(tempDir, "dest1.txt"),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            var step0Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            step0Exec.State.ShouldBe(StepExecutionState.Failed);

            var step1Execs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .ToListAsync();
            step1Execs.Count.ShouldBe(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SkipAndContinue_InsideForEach_FailedIterationContinuesLoop()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":3}""");

        // Step 0: forEach over 3 items
        await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach Items", new
        {
            source = """["a","b","c"]""",
        });

        // Step 1: body — always fails
        await TestDataSeeder.AddStep(db, jobId, 1, "test.fail", "Fail Step", new
        {
            message = "iteration failure",
        });

        // Step 2: end forEach
        await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        // All 3 body step iterations should exist and be failed
        var bodySteps = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
            .OrderBy(se => se.IterationIndex)
            .ToListAsync();
        bodySteps.Count.ShouldBe(3);
        bodySteps.ShouldAllBe(se => se.State == StepExecutionState.Failed);
        bodySteps[0].IterationIndex.ShouldBe(0);
        bodySteps[1].IterationIndex.ShouldBe(1);
        bodySteps[2].IterationIndex.ShouldBe(2);
    }
}

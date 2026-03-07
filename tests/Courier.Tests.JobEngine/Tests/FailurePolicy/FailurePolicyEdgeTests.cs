using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.FailurePolicy;

[Collection("EngineTests")]
[Trait("Category", "FailurePolicy")]
public class FailurePolicyEdgeTests
{
    private readonly DatabaseFixture _database;
    public FailurePolicyEdgeTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task StopPolicy_FailureInForEachBody_AbortsEntireExecution()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep(), new OutputTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":"stop","max_retries":0}""");

        // Step 0: forEach over 3 items
        await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach", new
        {
            source = """["a","b","c"]""",
        });

        // Step 1: body — always fails
        await TestDataSeeder.AddStep(db, jobId, 1, "test.fail", "Fail In Loop", new
        {
            message = "loop body failure",
        });

        // Step 2: end forEach
        await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

        // Step 3: should NOT run after forEach aborts
        await TestDataSeeder.AddStep(db, jobId, 3, "test.output", "After Loop", new
        {
            outputs = """{"result":"after_loop_ran"}""",
        });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Failed);

        // Only first iteration body ran (Stop policy aborts after first failure)
        var bodySteps = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
            .ToListAsync();
        bodySteps.Count.ShouldBe(1);
        bodySteps[0].State.ShouldBe(StepExecutionState.Failed);

        // Step 3 should not have run
        var afterLoopSteps = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 3)
            .ToListAsync();
        afterLoopSteps.Count.ShouldBe(0);
    }

    [Fact]
    public async Task SkipAndContinue_FailureInNestedIf_ContinuesToNextNode()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep(), new OutputTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":3}""");

        // Step 0: if true
        await TestDataSeeder.AddStep(db, jobId, 0, "flow.if", "If True", new
        {
            left = "a",
            @operator = "equals",
            right = "a",
        });

        // Step 1: then body — fails
        await TestDataSeeder.AddStep(db, jobId, 1, "test.fail", "Fail In If", new
        {
            message = "if body failure",
        });

        // Step 2: end if
        await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End If", new { });

        // Step 3: should still run (SkipAndContinue)
        await TestDataSeeder.AddStep(db, jobId, 3, "test.output", "After If", new
        {
            outputs = """{"result":"after_if_ran"}""",
        });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        // Step 1 failed
        var step1 = await db.StepExecutions
            .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 1);
        step1.State.ShouldBe(StepExecutionState.Failed);

        // Step 3 ran successfully
        var step3 = await db.StepExecutions
            .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 3);
        step3.State.ShouldBe(StepExecutionState.Completed);
    }

    [Fact]
    public async Task FailurePolicyEmptyObject_DefaultsToStop()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            await File.WriteAllTextAsync(sourceFile, "empty policy test");

            // Empty JSON object → deserializes to FailurePolicy with defaults → Stop
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db, failurePolicy: "{}");

            // Step 0: fails
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Copy", new
            {
                source_path = Path.Combine(tempDir, "nonexistent.txt"),
                destination_path = Path.Combine(tempDir, "dest.txt"),
            });

            // Step 1: should NOT run (defaults to Stop)
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Good Copy", new
            {
                source_path = sourceFile,
                destination_path = Path.Combine(tempDir, "dest2.txt"),
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
    public async Task FailurePolicyUnknownType_DefaultsToStop()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            await File.WriteAllTextAsync(sourceFile, "unknown type policy test");

            // Valid JSON but unknown type value → deserializes with default Type=Stop
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":99,"max_retries":0}""");

            // Step 0: fails
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Copy", new
            {
                source_path = Path.Combine(tempDir, "nonexistent.txt"),
                destination_path = Path.Combine(tempDir, "dest.txt"),
            });

            // Step 1: should NOT run
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Good Copy", new
            {
                source_path = sourceFile,
                destination_path = Path.Combine(tempDir, "dest2.txt"),
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
    public async Task SkipAndContinue_AllStepsFail_UsingTestFailStep_Completes()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
            failurePolicy: """{"type":3}""");

        // 3 steps all using test.fail
        await TestDataSeeder.AddStep(db, jobId, 0, "test.fail", "Fail 1", new
        {
            message = "failure 1",
        });
        await TestDataSeeder.AddStep(db, jobId, 1, "test.fail", "Fail 2", new
        {
            message = "failure 2",
        });
        await TestDataSeeder.AddStep(db, jobId, 2, "test.fail", "Fail 3", new
        {
            message = "failure 3",
        });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        // All 3 steps should have run and failed
        var allSteps = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId)
            .OrderBy(se => se.StepOrder)
            .ToListAsync();
        allSteps.Count.ShouldBe(3);
        allSteps.ShouldAllBe(se => se.State == StepExecutionState.Failed);
        allSteps[0].ErrorMessage.ShouldBe("failure 1");
        allSteps[1].ErrorMessage.ShouldBe("failure 2");
        allSteps[2].ErrorMessage.ShouldBe("failure 3");
    }
}

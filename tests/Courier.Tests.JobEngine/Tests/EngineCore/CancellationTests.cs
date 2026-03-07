using System.Text.Json;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.EngineCore;

[Collection("EngineTests")]
[Trait("Category", "Cancellation")]
public class CancellationTests
{
    private readonly DatabaseFixture _database;
    public CancellationTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task Cancel_AfterFirstStepCompletes_SecondStepNotExecuted()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // Step 0: succeeds (output step)
        await TestDataSeeder.AddStep(db, jobId, 0, "test.output", "Output Step", new
        {
            outputs = """{"result":"step0_done"}""",
        });

        // Step 1: sets cancellation signal on the execution
        await TestDataSeeder.AddStep(db, jobId, 1, "test.set_signal", "Set Cancel", new
        {
            signal = "cancelled",
            execution_id = executionId.ToString(),
        });

        // Step 2: should NOT run because signal is checked before each step
        await TestDataSeeder.AddStep(db, jobId, 2, "test.output", "Should Not Run", new
        {
            outputs = """{"result":"step2_ran"}""",
        });

        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new OutputTestStep(), new SignalSettingTestStep(db))
            .Build();

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Cancelled);
        execution.CancelledAt.ShouldNotBeNull();

        // Step 0 and 1 completed, step 2 never created
        var step0 = await db.StepExecutions
            .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
        step0.State.ShouldBe(StepExecutionState.Completed);

        var step1 = await db.StepExecutions
            .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 1);
        step1.State.ShouldBe(StepExecutionState.Completed);

        var step2Execs = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 2)
            .ToListAsync();
        step2Execs.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Cancel_InsideForEachLoop_StopsIteration()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // Step 0: forEach over 3 items
        await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach", new
        {
            source = """["a","b","c"]""",
        });

        // Step 1: body — sets cancel signal (runs on first iteration, then signal is detected before second)
        await TestDataSeeder.AddStep(db, jobId, 1, "test.set_signal", "Set Cancel In Loop", new
        {
            signal = "cancelled",
            execution_id = executionId.ToString(),
        });

        // Step 2: end forEach
        await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End ForEach", new { });

        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new SignalSettingTestStep(db))
            .Build();

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Cancelled);

        // Body step should have executed only once (first iteration) or twice at most
        // (signal set during iteration 0, checked before iteration 1 body)
        var bodySteps = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
            .ToListAsync();
        bodySteps.Count.ShouldBeGreaterThan(0);
        bodySteps.Count.ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Pause_AfterFirstStep_ContextSnapshotContainsStepOutput()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // Step 0: produces output
        await TestDataSeeder.AddStep(db, jobId, 0, "test.output", "Output Step", new
        {
            outputs = """{"my_key":"my_value"}""",
        });

        // Step 1: sets pause signal
        await TestDataSeeder.AddStep(db, jobId, 1, "test.set_signal", "Set Pause", new
        {
            signal = "paused",
            execution_id = executionId.ToString(),
        });

        // Step 2: should NOT run
        await TestDataSeeder.AddStep(db, jobId, 2, "test.output", "Should Not Run", new
        {
            outputs = """{"result":"step2_ran"}""",
        });

        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new OutputTestStep(), new SignalSettingTestStep(db))
            .Build();

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Paused);
        execution.PausedAt.ShouldNotBeNull();
        execution.ContextSnapshot.ShouldNotBeNull();

        // Context snapshot should contain step 0's output
        execution.ContextSnapshot.ShouldContain("0.my_key");
        execution.ContextSnapshot.ShouldContain("my_value");

        // Step 2 should not have executed
        var step2Execs = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 2)
            .ToListAsync();
        step2Execs.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Pause_InsideForEach_HaltsAndSavesContext()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // Step 0: forEach over 3 items
        await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach", new
        {
            source = """["x","y","z"]""",
        });

        // Step 1: body — sets pause signal on first iteration
        await TestDataSeeder.AddStep(db, jobId, 1, "test.set_signal", "Set Pause In Loop", new
        {
            signal = "paused",
            execution_id = executionId.ToString(),
        });

        // Step 2: end forEach
        await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End ForEach", new { });

        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new SignalSettingTestStep(db))
            .Build();

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Paused);
        execution.PausedAt.ShouldNotBeNull();
        execution.ContextSnapshot.ShouldNotBeNull();
    }

    [Fact]
    public async Task CancellationToken_AlreadyCancelled_ThrowsOrHandlesGracefully()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        await TestDataSeeder.AddStep(db, jobId, 0, "test.output", "Output Step", new
        {
            outputs = """{"result":"should_not_run"}""",
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // An already-cancelled token should cause OperationCanceledException
        await Should.ThrowAsync<OperationCanceledException>(
            () => engine.ExecuteAsync(executionId, cts.Token));
    }

    [Fact]
    public async Task Resume_AfterPause_CompletesRemainingSteps()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile1 = Path.Combine(tempDir, "dest1.txt");
            var destFile2 = Path.Combine(tempDir, "dest2.txt");
            await File.WriteAllTextAsync(sourceFile, "resume test content");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            var step0Id = await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy 1", new
            {
                source_path = sourceFile,
                destination_path = destFile1,
            });

            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy 2", new
            {
                source_path = sourceFile,
                destination_path = destFile2,
            });

            // Simulate step 0 already completed (as if paused after step 0)
            var completedStepExec = new StepExecution
            {
                Id = Guid.CreateVersion7(),
                JobExecutionId = executionId,
                JobStepId = step0Id,
                StepOrder = 0,
                State = StepExecutionState.Completed,
                StartedAt = DateTime.UtcNow.AddSeconds(-5),
                CompletedAt = DateTime.UtcNow.AddSeconds(-3),
                CreatedAt = DateTime.UtcNow.AddSeconds(-5),
            };
            db.StepExecutions.Add(completedStepExec);

            // Set context snapshot with workspace
            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.ContextSnapshot = JsonSerializer.Serialize(
                new Dictionary<string, object>
                {
                    ["workspace"] = tempDir,
                    ["0.copied_file"] = destFile1,
                });
            await db.SaveChangesAsync();

            var engine = new JobEngineBuilder(db, encryptor).Build();
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            await db.Entry(execution).ReloadAsync();
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Step 1 ran (file2 created)
            File.Exists(destFile2).ShouldBeTrue();

            // Both step executions exist
            var allSteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .OrderBy(se => se.StepOrder)
                .ToListAsync();
            allSteps.ShouldContain(se => se.StepOrder == 0 && se.State == StepExecutionState.Completed);
            allSteps.ShouldContain(se => se.StepOrder == 1 && se.State == StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

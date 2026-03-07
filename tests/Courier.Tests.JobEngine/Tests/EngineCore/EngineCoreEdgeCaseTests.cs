using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.EngineCore;

[Collection("EngineTests")]
[Trait("Category", "EdgeCase")]
public class EngineCoreEdgeCaseTests
{
    private readonly DatabaseFixture _database;
    public EngineCoreEdgeCaseTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task ZeroSteps_ExecutionCompletesImmediately()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        var stepExecs = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId)
            .ToListAsync();
        stepExecs.Count.ShouldBe(0);
    }

    [Fact]
    public async Task UnknownStepType_ExecutionFailsWithError()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        await TestDataSeeder.AddStep(db, jobId, 0, "bogus.step", "Bogus Step", new { });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Failed);

        var stepExec = await db.StepExecutions
            .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
        stepExec.State.ShouldBe(StepExecutionState.Failed);
        stepExec.ErrorMessage.ShouldNotBeNull();
        stepExec.ErrorMessage.ShouldContain("No step handler registered");
    }

    [Fact]
    public async Task ExecutionNotFound_ReturnsGracefully()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var countBefore = await db.JobExecutions.CountAsync();

        await engine.ExecuteAsync(Guid.CreateVersion7(), CancellationToken.None);

        var countAfter = await db.JobExecutions.CountAsync();
        countAfter.ShouldBe(countBefore);
    }

    [Fact]
    public async Task MissingRequiredConfig_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // Valid JSON but missing required fields for file.copy (source_path, destination_path)
        await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Config", new { });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Failed);

        var stepExec = await db.StepExecutions
            .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
        stepExec.State.ShouldBe(StepExecutionState.Failed);
        stepExec.ErrorMessage.ShouldNotBeNull();
    }

    [Fact]
    public async Task MalformedFailurePolicy_DefaultsToStop()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            await File.WriteAllTextAsync(sourceFile, "policy test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":"not_a_type"}""");

            // Step 0: file.copy with nonexistent source → fails
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Copy", new
            {
                source_path = Path.Combine(tempDir, "nonexistent.txt"),
                destination_path = Path.Combine(tempDir, "dest0.txt"),
            });

            // Step 1: file.copy with valid source → should not run (Stop policy)
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Good Copy", new
            {
                source_path = sourceFile,
                destination_path = Path.Combine(tempDir, "dest1.txt"),
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
    public async Task Cancellation_StopsExecutionBeforeFirstStep()
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
            await File.WriteAllTextAsync(sourceFile, "should not be copied");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy 1", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy 2", new
            {
                source_path = sourceFile,
                destination_path = Path.Combine(tempDir, "dest2.txt"),
            });

            // Set cancellation signal before execution starts
            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.RequestedState = "cancelled";
            await db.SaveChangesAsync();

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            await db.Entry(execution).ReloadAsync();
            execution.State.ShouldBe(JobExecutionState.Cancelled);
            execution.CancelledAt.ShouldNotBeNull();
            execution.CompletedAt.ShouldNotBeNull();

            // No steps should have been created
            var stepExecs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .ToListAsync();
            stepExecs.Count.ShouldBe(0);

            File.Exists(destFile).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Pause_StopsExecutionAndSavesContext()
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
            await File.WriteAllTextAsync(sourceFile, "should not be copied");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy 1", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy 2", new
            {
                source_path = sourceFile,
                destination_path = Path.Combine(tempDir, "dest2.txt"),
            });

            // Set pause signal before execution starts
            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.RequestedState = "paused";
            await db.SaveChangesAsync();

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            await db.Entry(execution).ReloadAsync();
            execution.State.ShouldBe(JobExecutionState.Paused);
            execution.PausedAt.ShouldNotBeNull();
            execution.RequestedState.ShouldBeNull();
            execution.ContextSnapshot.ShouldNotBeNullOrEmpty();

            // No steps should have been created
            var stepExecs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .ToListAsync();
            stepExecs.Count.ShouldBe(0);

            File.Exists(destFile).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Resume_SkipsCompletedStepsAndFinishes()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceA = Path.Combine(tempDir, "a.txt");
            var destB = Path.Combine(tempDir, "b.txt");
            var sourceC = Path.Combine(tempDir, "c.txt");
            var destD = Path.Combine(tempDir, "d.txt");

            // Create source files
            await File.WriteAllTextAsync(sourceA, "content A");
            await File.WriteAllTextAsync(sourceC, "content C");
            // Simulate step 0 already ran: B exists
            await File.WriteAllTextAsync(destB, "content A");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            var step0Id = await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy A→B", new
            {
                source_path = sourceA,
                destination_path = destB,
            });

            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy C→D", new
            {
                source_path = sourceC,
                destination_path = destD,
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

            // Set context snapshot with workspace + step 0 output
            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.ContextSnapshot = System.Text.Json.JsonSerializer.Serialize(
                new Dictionary<string, object>
                {
                    ["workspace"] = tempDir,
                    ["0.copied_file"] = destB,
                });
            await db.SaveChangesAsync();

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            await db.Entry(execution).ReloadAsync();
            execution.State.ShouldBe(JobExecutionState.Completed);

            // File D should exist (step 1 ran)
            File.Exists(destD).ShouldBeTrue();

            // Should have the original completed step + 1 new step execution
            var allStepExecs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .OrderBy(se => se.StepOrder)
                .ToListAsync();

            // Step 0: pre-existing completed step
            allStepExecs.ShouldContain(se => se.StepOrder == 0 && se.State == StepExecutionState.Completed);
            // Step 1: newly created
            allStepExecs.ShouldContain(se => se.StepOrder == 1 && se.State == StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UnterminatedFlowBlock_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "Loop", new
        {
            source = """["a","b"]""",
        });

        await TestDataSeeder.AddStep(db, jobId, 1, "file.delete", "Delete Item", new
        {
            path = "context:loop.current_item",
        });

        // No flow.end step — unterminated block

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Failed);
    }
}

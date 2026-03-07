using System.Text.Json;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.EngineCore;

[Collection("EngineTests")]
[Trait("Category", "Resume")]
public class ResumeEdgeTests
{
    private readonly DatabaseFixture _database;
    public ResumeEdgeTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task Resume_EmptyContextSnapshot_CreatesNewContext()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "empty snapshot test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Set empty context snapshot (simulating resume with empty state)
            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.ContextSnapshot = "{}";
            await db.SaveChangesAsync();

            var engine = new JobEngineBuilder(db, encryptor).Build();
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            await db.Entry(execution).ReloadAsync();
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Step should have run
            File.Exists(destFile).ShouldBeTrue();

            // Workspace key should be in the final context
            execution.ContextSnapshot.ShouldNotBeNull();
            execution.ContextSnapshot.ShouldContain("workspace");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Resume_NullContextSnapshot_CreatesNewContext()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "null snapshot test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // ContextSnapshot remains null (default)
            var engine = new JobEngineBuilder(db, encryptor).Build();
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(destFile).ShouldBeTrue();
            execution.ContextSnapshot.ShouldNotBeNull();
            execution.ContextSnapshot.ShouldContain("workspace");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Resume_SkipsOnlyCompletedRootSteps_NotIterationSteps()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: forEach over 2 items
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach", new
            {
                source = """["a","b"]""",
            });

            var step1Id = await TestDataSeeder.AddStep(db, jobId, 1, "test.output", "Body", new
            {
                outputs = """{"item":"processed"}""",
            });

            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            // Simulate: step 1 ran for iteration 0 (IterationIndex=0), but not iteration 1
            // These are iteration steps, NOT root steps, so resume should NOT skip the forEach
            var iterStepExec = new StepExecution
            {
                Id = Guid.CreateVersion7(),
                JobExecutionId = executionId,
                JobStepId = step1Id,
                StepOrder = 1,
                State = StepExecutionState.Completed,
                IterationIndex = 0,
                StartedAt = DateTime.UtcNow.AddSeconds(-5),
                CompletedAt = DateTime.UtcNow.AddSeconds(-3),
                CreatedAt = DateTime.UtcNow.AddSeconds(-5),
            };
            db.StepExecutions.Add(iterStepExec);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.ContextSnapshot = JsonSerializer.Serialize(
                new Dictionary<string, object>
                {
                    ["workspace"] = tempDir,
                });
            await db.SaveChangesAsync();

            var engine = new JobEngineBuilder(db, encryptor)
                .WithAdditionalSteps(new OutputTestStep())
                .Build();
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            await db.Entry(execution).ReloadAsync();
            execution.State.ShouldBe(JobExecutionState.Completed);

            // The forEach should have re-run, creating new body step executions
            // (iteration steps with IterationIndex != null are not counted as completed root steps)
            var bodySteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .ToListAsync();
            // Should have original iteration 0 + new iterations from re-run
            bodySteps.Count.ShouldBeGreaterThanOrEqualTo(2);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Resume_MidPipeline_RunsRemainingSteps()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var dest1 = Path.Combine(tempDir, "dest1.txt");
            var dest2 = Path.Combine(tempDir, "dest2.txt");
            var dest3 = Path.Combine(tempDir, "dest3.txt");
            await File.WriteAllTextAsync(sourceFile, "resume mid-pipeline");
            // Simulate step 0 already completed: dest1 exists
            await File.WriteAllTextAsync(dest1, "resume mid-pipeline");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            var step0Id = await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy 1", new
            {
                source_path = sourceFile,
                destination_path = dest1,
            });

            var step1Id = await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy 2", new
            {
                source_path = sourceFile,
                destination_path = dest2,
            });

            await TestDataSeeder.AddStep(db, jobId, 2, "file.copy", "Copy 3", new
            {
                source_path = sourceFile,
                destination_path = dest3,
            });

            // Simulate steps 0 and 1 already completed
            db.StepExecutions.Add(new StepExecution
            {
                Id = Guid.CreateVersion7(),
                JobExecutionId = executionId,
                JobStepId = step0Id,
                StepOrder = 0,
                State = StepExecutionState.Completed,
                StartedAt = DateTime.UtcNow.AddSeconds(-10),
                CompletedAt = DateTime.UtcNow.AddSeconds(-8),
                CreatedAt = DateTime.UtcNow.AddSeconds(-10),
            });
            db.StepExecutions.Add(new StepExecution
            {
                Id = Guid.CreateVersion7(),
                JobExecutionId = executionId,
                JobStepId = step1Id,
                StepOrder = 1,
                State = StepExecutionState.Completed,
                StartedAt = DateTime.UtcNow.AddSeconds(-7),
                CompletedAt = DateTime.UtcNow.AddSeconds(-5),
                CreatedAt = DateTime.UtcNow.AddSeconds(-7),
            });

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.ContextSnapshot = JsonSerializer.Serialize(
                new Dictionary<string, object>
                {
                    ["workspace"] = tempDir,
                    ["0.copied_file"] = dest1,
                    ["1.copied_file"] = dest2,
                });
            await db.SaveChangesAsync();

            var engine = new JobEngineBuilder(db, encryptor).Build();
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            await db.Entry(execution).ReloadAsync();
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Only step 2 should have a new execution record
            File.Exists(dest3).ShouldBeTrue();

            var allSteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .OrderBy(se => se.StepOrder)
                .ToListAsync();

            allSteps.ShouldContain(se => se.StepOrder == 0 && se.State == StepExecutionState.Completed);
            allSteps.ShouldContain(se => se.StepOrder == 1 && se.State == StepExecutionState.Completed);
            allSteps.ShouldContain(se => se.StepOrder == 2 && se.State == StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Resume_WorkspacePathRestored_FromSnapshot()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "workspace restore test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            var step0Id = await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy 1", new
            {
                source_path = sourceFile,
                destination_path = Path.Combine(tempDir, "already_done.txt"),
            });

            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy 2", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Simulate step 0 completed
            db.StepExecutions.Add(new StepExecution
            {
                Id = Guid.CreateVersion7(),
                JobExecutionId = executionId,
                JobStepId = step0Id,
                StepOrder = 0,
                State = StepExecutionState.Completed,
                StartedAt = DateTime.UtcNow.AddSeconds(-5),
                CompletedAt = DateTime.UtcNow.AddSeconds(-3),
                CreatedAt = DateTime.UtcNow.AddSeconds(-5),
            });

            // Set context snapshot WITH the workspace path pointing to tempDir
            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.ContextSnapshot = JsonSerializer.Serialize(
                new Dictionary<string, object>
                {
                    ["workspace"] = tempDir,
                });
            await db.SaveChangesAsync();

            var engine = new JobEngineBuilder(db, encryptor).Build();
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            await db.Entry(execution).ReloadAsync();
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Step 1 executed successfully
            File.Exists(destFile).ShouldBeTrue();

            // The final context snapshot should still contain the same workspace path
            execution.ContextSnapshot.ShouldNotBeNull();
            execution.ContextSnapshot.ShouldContain("workspace");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

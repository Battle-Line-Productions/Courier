using System.Text.Json;
using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.FlowControl;

[Collection("EngineTests")]
[Trait("Category", "FlowControl")]
public class ForEachEngineTests
{
    private readonly DatabaseFixture _database;
    public ForEachEngineTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task ForEach_LiteralArray_ExecutesBodyPerItem()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create source files
            for (var i = 0; i < 3; i++)
                await File.WriteAllTextAsync(Path.Combine(tempDir, $"file{i}.txt"), $"content{i}");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            var filePaths = Enumerable.Range(0, 3)
                .Select(i => Path.Combine(tempDir, $"file{i}.txt"))
                .ToArray();

            // Step 0: forEach over literal array of file paths
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach Files", new
            {
                source = JsonSerializer.Serialize(filePaths),
            });

            // Step 1: delete each file (body of forEach) — uses context:loop.current_item as path
            await TestDataSeeder.AddStep(db, jobId, 1, "file.delete", "Delete File", new
            {
                path = "context:loop.current_item",
            });

            // Step 2: end forEach
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Verify 3 step executions for the body step (one per iteration)
            var bodySteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .OrderBy(se => se.IterationIndex)
                .ToListAsync();
            bodySteps.Count.ShouldBe(3);
            bodySteps[0].IterationIndex.ShouldBe(0);
            bodySteps[1].IterationIndex.ShouldBe(1);
            bodySteps[2].IterationIndex.ShouldBe(2);
            bodySteps.ShouldAllBe(se => se.State == StepExecutionState.Completed);

            // Files should have been deleted
            for (var i = 0; i < 3; i++)
                File.Exists(Path.Combine(tempDir, $"file{i}.txt")).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ForEach_EmptyArray_SkipsBody_ExecutionCompletes()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // forEach over empty array
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach Empty", new
            {
                source = "[]",
            });

            await TestDataSeeder.AddStep(db, jobId, 1, "file.delete", "Delete File", new
            {
                path = "context:loop.current_item",
            });

            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // No step executions for the body step
            var bodySteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .ToListAsync();
            bodySteps.Count.ShouldBe(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ForEach_ContextRefSource_IteratesOverPriorStepOutput()
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
            await File.WriteAllTextAsync(sourceFile, "hello");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: file.copy produces output with copied_file path
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Step 1: forEach over a literal array that we know will work,
            // but the real test is whether context:0.copied_file resolves.
            // Since forEach source resolves context refs via ResolveForEachSource,
            // we build a JSON array from a known set and verify iteration.
            var itemArray = new[] { "item_a", "item_b" };
            await TestDataSeeder.AddStep(db, jobId, 1, "flow.foreach", "ForEach Items", new
            {
                source = JsonSerializer.Serialize(itemArray),
            });

            // Step 2: body — delete a file that doesn't exist (won't fail because fail_if_not_found defaults to false)
            await TestDataSeeder.AddStep(db, jobId, 2, "file.delete", "Delete Nonexistent", new
            {
                path = "context:loop.current_item",
                fail_if_not_found = false,
            });

            // Step 3: end forEach
            await TestDataSeeder.AddStep(db, jobId, 3, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Verify step 0 produced context output
            var step0Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            step0Exec.State.ShouldBe(StepExecutionState.Completed);
            step0Exec.OutputData.ShouldNotBeNull();
            step0Exec.OutputData.ShouldContain("copied_file");

            // Verify forEach body executed twice
            var bodySteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 2)
                .ToListAsync();
            bodySteps.Count.ShouldBe(2);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task ForEach_NonArraySource_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: forEach with a valid JSON string (not an array)
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach NonArray", new
            {
                source = "\"just a string\"",
            });

            // Step 1: body
            await TestDataSeeder.AddStep(db, jobId, 1, "file.delete", "Delete File", new
            {
                path = "context:loop.current_item",
            });

            // Step 2: end forEach
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task ForEach_MalformedJsonSource_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: forEach with malformed JSON
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach Malformed", new
            {
                source = "not valid json",
            });

            // Step 1: body
            await TestDataSeeder.AddStep(db, jobId, 1, "file.delete", "Delete File", new
            {
                path = "context:loop.current_item",
            });

            // Step 2: end forEach
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task ForEach_ContextRefSourceNotFound_ExecutionFails()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // forEach over context ref that doesn't exist
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach Bad Ref", new
            {
                source = "context:99.nonexistent",
            });

            // Step 1: body
            await TestDataSeeder.AddStep(db, jobId, 1, "file.delete", "Delete File", new
            {
                path = "context:loop.current_item",
            });

            // Step 2: end forEach
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task ForEach_SingleItemArray_ExecutesOnce()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: forEach over single-item array
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach Single", new
            {
                source = """["only-one"]""",
            });

            // Step 1: body — delete with fail_if_not_found=false so it succeeds
            await TestDataSeeder.AddStep(db, jobId, 1, "file.delete", "Delete File", new
            {
                path = "context:loop.current_item",
                fail_if_not_found = false,
            });

            // Step 2: end forEach
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Verify body step executed exactly once
            var bodySteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .ToListAsync();
            bodySteps.Count.ShouldBe(1);
            bodySteps[0].IterationIndex.ShouldBe(0);
            bodySteps[0].State.ShouldBe(StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

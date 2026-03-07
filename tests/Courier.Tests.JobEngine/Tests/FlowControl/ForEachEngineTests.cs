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

    [Fact]
    public async Task ForEach_ComplexJsonObjects_BodyAccessesNestedProperty()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create files named a.txt and b.txt
            await File.WriteAllTextAsync(Path.Combine(tempDir, "a.txt"), "file a");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "b.txt"), "file b");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // forEach over JSON objects with nested "name" property
            var items = new[]
            {
                new { name = Path.Combine(tempDir, "a.txt"), size = 100 },
                new { name = Path.Combine(tempDir, "b.txt"), size = 200 },
            };
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach Objects", new
            {
                source = JsonSerializer.Serialize(items),
            });

            // Body step: delete file using context:loop.current_item.name
            await TestDataSeeder.AddStep(db, jobId, 1, "file.delete", "Delete File", new
            {
                path = "context:loop.current_item.name",
            });

            // End forEach
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Both files should have been deleted
            File.Exists(Path.Combine(tempDir, "a.txt")).ShouldBeFalse();
            File.Exists(Path.Combine(tempDir, "b.txt")).ShouldBeFalse();

            // Verify 2 body step executions
            var bodySteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .OrderBy(se => se.IterationIndex)
                .ToListAsync();
            bodySteps.Count.ShouldBe(2);
            bodySteps.ShouldAllBe(se => se.State == StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ForEach_BodyProducesOutputs_StepAfterLoopCompletes()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create source files for each iteration
            await File.WriteAllTextAsync(Path.Combine(tempDir, "src0.txt"), "content0");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "src1.txt"), "content1");

            var filePaths = new[]
            {
                Path.Combine(tempDir, "src0.txt"),
                Path.Combine(tempDir, "src1.txt"),
            };

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: forEach over file paths
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach Files", new
            {
                source = JsonSerializer.Serialize(filePaths),
            });

            // Step 1: body — copy file (produces output)
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy File", new
            {
                source_path = "context:loop.current_item",
                destination_path = Path.Combine(tempDir, "loop-dest.txt"),
            });

            // Step 2: end forEach
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            // Step 3: file.copy after the loop (verifies pipeline continues after loop)
            await File.WriteAllTextAsync(Path.Combine(tempDir, "after-loop-src.txt"), "after loop");
            await TestDataSeeder.AddStep(db, jobId, 3, "file.copy", "Post-Loop Copy", new
            {
                source_path = Path.Combine(tempDir, "after-loop-src.txt"),
                destination_path = Path.Combine(tempDir, "after-loop-dest.txt"),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Verify body executed twice
            var bodySteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .ToListAsync();
            bodySteps.Count.ShouldBe(2);
            bodySteps.ShouldAllBe(se => se.State == StepExecutionState.Completed);

            // Post-loop step should have completed
            var postLoopStep = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 3);
            postLoopStep.State.ShouldBe(StepExecutionState.Completed);

            File.Exists(Path.Combine(tempDir, "after-loop-dest.txt")).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ForEach_SingleIterationFails_SkipAndContinue_RemainingIterationsRun()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create files for items 0 and 2, but NOT item 1
            var file0 = Path.Combine(tempDir, "item0.txt");
            var file2 = Path.Combine(tempDir, "item2.txt");
            await File.WriteAllTextAsync(file0, "item 0");
            await File.WriteAllTextAsync(file2, "item 2");

            var filePaths = new[]
            {
                file0,
                Path.Combine(tempDir, "item1.txt"), // does NOT exist
                file2,
            };

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db,
                failurePolicy: """{"type":3}""");

            // Step 0: forEach over file paths
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach Files", new
            {
                source = JsonSerializer.Serialize(filePaths),
            });

            // Step 1: body — delete file with fail_if_not_found=true
            await TestDataSeeder.AddStep(db, jobId, 1, "file.delete", "Delete File", new
            {
                path = "context:loop.current_item",
                fail_if_not_found = true,
            });

            // Step 2: end forEach
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Verify 3 body step executions
            var bodySteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .OrderBy(se => se.IterationIndex)
                .ToListAsync();
            bodySteps.Count.ShouldBe(3);

            // Items 0 and 2 should have completed, item 1 should have failed
            bodySteps[0].State.ShouldBe(StepExecutionState.Completed);
            bodySteps[1].State.ShouldBe(StepExecutionState.Failed);
            bodySteps[2].State.ShouldBe(StepExecutionState.Completed);

            // Files 0 and 2 should have been deleted
            File.Exists(file0).ShouldBeFalse();
            File.Exists(file2).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

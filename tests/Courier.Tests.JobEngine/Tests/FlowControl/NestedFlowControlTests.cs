using System.Text.Json;
using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.FlowControl;

[Collection("EngineTests")]
[Trait("Category", "FlowControl")]
public class NestedFlowControlTests
{
    private readonly DatabaseFixture _database;
    public NestedFlowControlTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task ForEach_WithIfElseInBody_ExecutesCorrectBranch()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create files: file_yes.txt and file_no.txt
            var yesFile = Path.Combine(tempDir, "file_yes.txt");
            var noFile = Path.Combine(tempDir, "file_no.txt");
            await File.WriteAllTextAsync(yesFile, "yes content");
            await File.WriteAllTextAsync(noFile, "no content");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: forEach over ["yes", "no"]
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach", new
            {
                source = """["yes","no"]""",
            });

            // Step 1: if loop.current_item equals "yes"
            await TestDataSeeder.AddStep(db, jobId, 1, "flow.if", "If Yes", new
            {
                left = "context:loop.current_item",
                @operator = "equals",
                right = "yes",
            });

            // Step 2: then branch — delete the yes file
            await TestDataSeeder.AddStep(db, jobId, 2, "file.delete", "Delete Yes", new
            {
                path = yesFile,
            });

            // Step 3: else
            await TestDataSeeder.AddStep(db, jobId, 3, "flow.else", "Else", new { });

            // Step 4: else branch — delete the no file
            await TestDataSeeder.AddStep(db, jobId, 4, "file.delete", "Delete No", new
            {
                path = noFile,
            });

            // Step 5: end if
            await TestDataSeeder.AddStep(db, jobId, 5, "flow.end", "End If", new { });

            // Step 6: end forEach
            await TestDataSeeder.AddStep(db, jobId, 6, "flow.end", "End ForEach", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Both files should be deleted (yes in iteration 0 then branch, no in iteration 1 else branch)
            File.Exists(yesFile).ShouldBeFalse();
            File.Exists(noFile).ShouldBeFalse();

            // Verify correct step executions:
            // Step 2 (Delete Yes) should have 1 execution (only for "yes" iteration)
            var step2Execs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 2)
                .ToListAsync();
            step2Execs.Count.ShouldBe(1);
            step2Execs[0].IterationIndex.ShouldBe(0);

            // Step 4 (Delete No) should have 1 execution (only for "no" iteration)
            var step4Execs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 4)
                .ToListAsync();
            step4Execs.Count.ShouldBe(1);
            step4Execs[0].IterationIndex.ShouldBe(1);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task NestedForEach_InnerLoopExecutesPerOuterItem()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create 4 files (2 outer x 2 inner combinations)
            for (var o = 0; o < 2; o++)
            for (var i = 0; i < 2; i++)
            {
                var filePath = Path.Combine(tempDir, $"file_{o}_{i}.txt");
                await File.WriteAllTextAsync(filePath, $"content_{o}_{i}");
            }

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            var outerItems = JsonSerializer.Serialize(new[] { "0", "1" });
            var innerItems = JsonSerializer.Serialize(new[] { "0", "1" });

            // Step 0: outer forEach
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "Outer ForEach", new
            {
                source = outerItems,
            });

            // Step 1: inner forEach
            await TestDataSeeder.AddStep(db, jobId, 1, "flow.foreach", "Inner ForEach", new
            {
                source = innerItems,
            });

            // Step 2: delete file (body of inner forEach)
            // Since loop.current_item reflects the innermost loop, we use a known filename pattern
            // We can't directly construct the path from two loop vars easily,
            // so we use file.delete with fail_if_not_found=false to test iteration count
            await TestDataSeeder.AddStep(db, jobId, 2, "file.delete", "Delete File", new
            {
                path = "context:loop.current_item",
                fail_if_not_found = false,
            });

            // Step 3: end inner forEach
            await TestDataSeeder.AddStep(db, jobId, 3, "flow.end", "End Inner", new { });

            // Step 4: end outer forEach
            await TestDataSeeder.AddStep(db, jobId, 4, "flow.end", "End Outer", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Step 2 should have 4 executions (2 outer * 2 inner)
            var bodySteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 2)
                .ToListAsync();
            bodySteps.Count.ShouldBe(4);
            bodySteps.ShouldAllBe(se => se.State == StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task TriplyNestedIfBlocks_AllBranchesEvaluateCorrectly()
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
            await File.WriteAllTextAsync(sourceFile, "nested test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: outer if "a" equals "a" → true
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.if", "Outer If", new
            {
                left = "a",
                @operator = "equals",
                right = "a",
            });

            // Step 1: middle if "b" equals "b" → true
            await TestDataSeeder.AddStep(db, jobId, 1, "flow.if", "Middle If", new
            {
                left = "b",
                @operator = "equals",
                right = "b",
            });

            // Step 2: inner if "c" equals "c" → true
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.if", "Inner If", new
            {
                left = "c",
                @operator = "equals",
                right = "c",
            });

            // Step 3: innermost body — copy file
            await TestDataSeeder.AddStep(db, jobId, 3, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Step 4: end inner if
            await TestDataSeeder.AddStep(db, jobId, 4, "flow.end", "End Inner", new { });

            // Step 5: end middle if
            await TestDataSeeder.AddStep(db, jobId, 5, "flow.end", "End Middle", new { });

            // Step 6: end outer if
            await TestDataSeeder.AddStep(db, jobId, 6, "flow.end", "End Outer", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Innermost body executed — file was copied
            File.Exists(destFile).ShouldBeTrue();

            var copyStep = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 3);
            copyStep.State.ShouldBe(StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task ForEachBodyStepFails_WithStopPolicy_AbortsLoop()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var destFile = Path.Combine(tempDir, "dest.txt");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: forEach over 3 items
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach Items", new
            {
                source = """["item1","item2","item3"]""",
            });

            // Step 1: body — file.copy with source from loop (will fail — "item1" is not a real file)
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy File", new
            {
                source_path = "context:loop.current_item",
                destination_path = destFile,
            });

            // Step 2: end forEach
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            // Only 1 step execution for step 1 — first iteration failed, rest skipped
            var bodySteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .ToListAsync();
            bodySteps.Count.ShouldBe(1);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

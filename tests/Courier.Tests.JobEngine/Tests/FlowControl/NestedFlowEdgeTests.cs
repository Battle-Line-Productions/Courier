using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.FlowControl;

[Collection("EngineTests")]
[Trait("Category", "FlowControl")]
public class NestedFlowEdgeTests
{
    private readonly DatabaseFixture _database;
    public NestedFlowEdgeTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task ForEach_Inside_IfThenBranch_ExecutesCorrectly()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new OutputTestStep())
            .Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create files to delete
            var file0 = Path.Combine(tempDir, "file0.txt");
            var file1 = Path.Combine(tempDir, "file1.txt");
            await File.WriteAllTextAsync(file0, "content0");
            await File.WriteAllTextAsync(file1, "content1");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: if "a" equals "a" → true
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.if", "If True", new
            {
                left = "a",
                @operator = "equals",
                right = "a",
            });

            // Step 1: forEach inside then-branch
            var escapedFile0 = file0.Replace("\\", "\\\\");
            var escapedFile1 = file1.Replace("\\", "\\\\");
            await TestDataSeeder.AddStep(db, jobId, 1, "flow.foreach", "ForEach In Then", new
            {
                source = $"""["{escapedFile0}","{escapedFile1}"]""",
            });

            // Step 2: body — delete file
            await TestDataSeeder.AddStep(db, jobId, 2, "file.delete", "Delete File", new
            {
                path = "context:loop.current_item",
            });

            // Step 3: end forEach
            await TestDataSeeder.AddStep(db, jobId, 3, "flow.end", "End ForEach", new { });

            // Step 4: end if
            await TestDataSeeder.AddStep(db, jobId, 4, "flow.end", "End If", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Both files deleted
            File.Exists(file0).ShouldBeFalse();
            File.Exists(file1).ShouldBeFalse();

            // Body step executed twice (one per file)
            var bodySteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 2)
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
    public async Task ForEach_Inside_IfElseBranch_ExecutesCorrectly()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new OutputTestStep())
            .Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var file0 = Path.Combine(tempDir, "file0.txt");
            var file1 = Path.Combine(tempDir, "file1.txt");
            await File.WriteAllTextAsync(file0, "content0");
            await File.WriteAllTextAsync(file1, "content1");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            var escapedFile0 = file0.Replace("\\", "\\\\");
            var escapedFile1 = file1.Replace("\\", "\\\\");

            // Step 0: if "a" equals "b" → false → else branch
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.if", "If False", new
            {
                left = "a",
                @operator = "equals",
                right = "b",
            });

            // Step 1: then branch — should be skipped
            await TestDataSeeder.AddStep(db, jobId, 1, "test.output", "Then Step", new
            {
                outputs = """{"result":"then_ran"}""",
            });

            // Step 2: else
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.else", "Else", new { });

            // Step 3: forEach inside else-branch
            await TestDataSeeder.AddStep(db, jobId, 3, "flow.foreach", "ForEach In Else", new
            {
                source = $"""["{escapedFile0}","{escapedFile1}"]""",
            });

            // Step 4: body — delete file
            await TestDataSeeder.AddStep(db, jobId, 4, "file.delete", "Delete File", new
            {
                path = "context:loop.current_item",
            });

            // Step 5: end forEach
            await TestDataSeeder.AddStep(db, jobId, 5, "flow.end", "End ForEach", new { });

            // Step 6: end if
            await TestDataSeeder.AddStep(db, jobId, 6, "flow.end", "End If", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Files deleted via else branch
            File.Exists(file0).ShouldBeFalse();
            File.Exists(file1).ShouldBeFalse();

            // Then step should NOT have run
            var thenSteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
                .ToListAsync();
            thenSteps.Count.ShouldBe(0);

            // Body step in else executed twice
            var bodySteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 4)
                .ToListAsync();
            bodySteps.Count.ShouldBe(2);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TripleNestedForEach_ExecutesAllIterations()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new OutputTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // Step 0: outer forEach (2 items)
        await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "Outer", new
        {
            source = """["a","b"]""",
        });

        // Step 1: middle forEach (2 items)
        await TestDataSeeder.AddStep(db, jobId, 1, "flow.foreach", "Middle", new
        {
            source = """["x","y"]""",
        });

        // Step 2: inner forEach (2 items)
        await TestDataSeeder.AddStep(db, jobId, 2, "flow.foreach", "Inner", new
        {
            source = """["1","2"]""",
        });

        // Step 3: body — output step
        await TestDataSeeder.AddStep(db, jobId, 3, "test.output", "Body", new
        {
            outputs = """{"done":"true"}""",
        });

        // Step 4: end inner
        await TestDataSeeder.AddStep(db, jobId, 4, "flow.end", "End Inner", new { });

        // Step 5: end middle
        await TestDataSeeder.AddStep(db, jobId, 5, "flow.end", "End Middle", new { });

        // Step 6: end outer
        await TestDataSeeder.AddStep(db, jobId, 6, "flow.end", "End Outer", new { });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        // Body step should have 2*2*2 = 8 executions
        var bodySteps = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 3)
            .ToListAsync();
        bodySteps.Count.ShouldBe(8);
        bodySteps.ShouldAllBe(se => se.State == StepExecutionState.Completed);
    }

    [Fact]
    public async Task If_Inside_ForEach_DifferentBranchPerIteration()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new OutputTestStep())
            .Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var yesFile = Path.Combine(tempDir, "yes_result.txt");
            var noFile = Path.Combine(tempDir, "no_result.txt");
            await File.WriteAllTextAsync(yesFile, "to be deleted");
            await File.WriteAllTextAsync(noFile, "to be deleted");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: forEach over ["yes","no","yes"]
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach", new
            {
                source = """["yes","no","yes"]""",
            });

            // Step 1: if loop item equals "yes"
            await TestDataSeeder.AddStep(db, jobId, 1, "flow.if", "If Yes", new
            {
                left = "context:loop.current_item",
                @operator = "equals",
                right = "yes",
            });

            // Step 2: then — output "then_ran"
            await TestDataSeeder.AddStep(db, jobId, 2, "test.output", "Then Output", new
            {
                outputs = """{"branch":"then"}""",
            });

            // Step 3: else
            await TestDataSeeder.AddStep(db, jobId, 3, "flow.else", "Else", new { });

            // Step 4: else — output "else_ran"
            await TestDataSeeder.AddStep(db, jobId, 4, "test.output", "Else Output", new
            {
                outputs = """{"branch":"else"}""",
            });

            // Step 5: end if
            await TestDataSeeder.AddStep(db, jobId, 5, "flow.end", "End If", new { });

            // Step 6: end forEach
            await TestDataSeeder.AddStep(db, jobId, 6, "flow.end", "End ForEach", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Then step (step 2) should execute for iterations 0 and 2 (where item = "yes")
            var thenSteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 2)
                .OrderBy(se => se.IterationIndex)
                .ToListAsync();
            thenSteps.Count.ShouldBe(2);

            // Else step (step 4) should execute for iteration 1 (where item = "no")
            var elseSteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 4)
                .ToListAsync();
            elseSteps.Count.ShouldBe(1);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ForEach_Then_If_Then_ForEach_Sequential()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new OutputTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // Block 1: ForEach over 2 items
        await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach 1", new
        {
            source = """["a","b"]""",
        });
        await TestDataSeeder.AddStep(db, jobId, 1, "test.output", "Body 1", new
        {
            outputs = """{"block":"1"}""",
        });
        await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End ForEach 1", new { });

        // Block 2: If true
        await TestDataSeeder.AddStep(db, jobId, 3, "flow.if", "If True", new
        {
            left = "x",
            @operator = "equals",
            right = "x",
        });
        await TestDataSeeder.AddStep(db, jobId, 4, "test.output", "If Body", new
        {
            outputs = """{"block":"2"}""",
        });
        await TestDataSeeder.AddStep(db, jobId, 5, "flow.end", "End If", new { });

        // Block 3: ForEach over 3 items
        await TestDataSeeder.AddStep(db, jobId, 6, "flow.foreach", "ForEach 2", new
        {
            source = """["x","y","z"]""",
        });
        await TestDataSeeder.AddStep(db, jobId, 7, "test.output", "Body 2", new
        {
            outputs = """{"block":"3"}""",
        });
        await TestDataSeeder.AddStep(db, jobId, 8, "flow.end", "End ForEach 2", new { });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        // Block 1 body: 2 executions
        var block1Body = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
            .ToListAsync();
        block1Body.Count.ShouldBe(2);

        // Block 2 body: 1 execution (if true)
        var block2Body = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 4)
            .ToListAsync();
        block2Body.Count.ShouldBe(1);

        // Block 3 body: 3 executions
        var block3Body = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 7)
            .ToListAsync();
        block3Body.Count.ShouldBe(3);
    }

    [Fact]
    public async Task EmptyThenBranch_WithElse_ElseBranchExecutes()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new OutputTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // Step 0: if false
        await TestDataSeeder.AddStep(db, jobId, 0, "flow.if", "If False", new
        {
            left = "a",
            @operator = "equals",
            right = "b",
        });

        // Step 1: else (no then-branch steps between if and else)
        await TestDataSeeder.AddStep(db, jobId, 1, "flow.else", "Else", new { });

        // Step 2: else body
        await TestDataSeeder.AddStep(db, jobId, 2, "test.output", "Else Body", new
        {
            outputs = """{"branch":"else_ran"}""",
        });

        // Step 3: end if
        await TestDataSeeder.AddStep(db, jobId, 3, "flow.end", "End If", new { });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        // Else body should have executed
        var elseBody = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 2)
            .ToListAsync();
        elseBody.Count.ShouldBe(1);
        elseBody[0].State.ShouldBe(StepExecutionState.Completed);
    }
}

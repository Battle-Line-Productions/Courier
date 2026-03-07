using System.Text.Json;
using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.Context;

[Collection("EngineTests")]
[Trait("Category", "Context")]
public class ContextPassingTests
{
    private readonly DatabaseFixture _database;
    public ContextPassingTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task StepOutput_AvailableToNextStep_ViaContextPrefix()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "copied.txt");
            await File.WriteAllTextAsync(sourceFile, "context passing test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: file.copy → outputs { copied_file: destFile }
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Step 1: file.delete using context:0.copied_file as path
            // FileDeleteStep calls ContextResolver.Resolve on the path config
            await TestDataSeeder.AddStep(db, jobId, 1, "file.delete", "Delete Copied", new
            {
                path = "context:0.copied_file",
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Step 0 should have completed and produced output
            var step0Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            step0Exec.State.ShouldBe(StepExecutionState.Completed);
            step0Exec.OutputData.ShouldNotBeNull();
            step0Exec.OutputData.ShouldContain("copied_file");

            // Step 1 should have completed (it resolved context:0.copied_file successfully)
            var step1Exec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 1);
            step1Exec.State.ShouldBe(StepExecutionState.Completed);
            step1Exec.OutputData.ShouldNotBeNull();
            step1Exec.OutputData.ShouldContain("deleted_file");

            // The file should have been deleted
            File.Exists(destFile).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ThreeStepChain_ProgressiveContextBuilding()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var file1 = Path.Combine(tempDir, "original.txt");
            var file2 = Path.Combine(tempDir, "copy1.txt");
            var file3 = Path.Combine(tempDir, "copy2.txt");
            await File.WriteAllTextAsync(file1, "chain test content");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: copy original → copy1 (output: 0.copied_file = copy1.txt)
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "First Copy", new
            {
                source_path = file1,
                destination_path = file2,
            });

            // Step 1: copy copy1 → copy2 (output: 1.copied_file = copy2.txt)
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Second Copy", new
            {
                source_path = file2,
                destination_path = file3,
            });

            // Step 2: delete copy2 using context:1.copied_file
            await TestDataSeeder.AddStep(db, jobId, 2, "file.delete", "Delete Last Copy", new
            {
                path = "context:1.copied_file",
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // All 3 steps completed
            var allSteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .OrderBy(se => se.StepOrder)
                .ToListAsync();
            allSteps.Count.ShouldBe(3);
            allSteps.ShouldAllBe(se => se.State == StepExecutionState.Completed);

            // Original and copy1 still exist, copy2 was deleted
            File.Exists(file1).ShouldBeTrue();
            File.Exists(file2).ShouldBeTrue();
            File.Exists(file3).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Workspace_ContextKey_IsAccessible()
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
            await File.WriteAllTextAsync(sourceFile, "workspace test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Single step to trigger execution and context initialization
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // The execution should have a context snapshot that includes "workspace"
            execution.ContextSnapshot.ShouldNotBeNull();
            execution.ContextSnapshot.ShouldContain("workspace");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task ContextRef_NonexistentStepOutput_ExecutionFails()
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
            await File.WriteAllTextAsync(sourceFile, "test content");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: file.copy referencing nonexistent step 99 output
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Bad Ref", new
            {
                source_path = "context:99.copied_file",
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Failed);
            stepExec.ErrorMessage.ShouldNotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task ContextRef_NoPrefix_TreatedAsLiteral()
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

            // Step 0: file.copy with literal string (not a context ref) as source
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Literal", new
            {
                source_path = "no-context-prefix",
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Failed);
            stepExec.ErrorMessage.ShouldNotBeNull();
            stepExec.ErrorMessage.ShouldContain("not found");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task ContextRef_EmptyKey_ExecutionFails()
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

            // Step 0: file.copy with empty context key
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Empty Key", new
            {
                source_path = "context:",
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Failed);
            stepExec.ErrorMessage.ShouldNotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task ContextRef_InsideForEachLoop_ResolvesLoopItem()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var file0 = Path.Combine(tempDir, "item0.txt");
            var file1 = Path.Combine(tempDir, "item1.txt");
            await File.WriteAllTextAsync(file0, "content0");
            await File.WriteAllTextAsync(file1, "content1");

            var filePaths = new[] { file0, file1 };

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: forEach over file paths
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach Files", new
            {
                source = JsonSerializer.Serialize(filePaths),
            });

            // Step 1: delete each file using context:loop.current_item
            await TestDataSeeder.AddStep(db, jobId, 1, "file.delete", "Delete File", new
            {
                path = "context:loop.current_item",
            });

            // Step 2: end forEach
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Both files should have been deleted
            File.Exists(file0).ShouldBeFalse();
            File.Exists(file1).ShouldBeFalse();

            // Step 1 should have 2 step executions (one per iteration)
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
}

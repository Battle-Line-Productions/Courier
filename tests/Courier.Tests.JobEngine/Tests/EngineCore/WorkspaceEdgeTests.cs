using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.EngineCore;

[Collection("EngineTests")]
[Trait("Category", "Workspace")]
public class WorkspaceEdgeTests
{
    private readonly DatabaseFixture _database;
    public WorkspaceEdgeTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task Workspace_NotCleanedUp_WhenPaused()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var baseDir = Path.Combine(Path.GetTempPath(), $"engine-workspace-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(baseDir);

        try
        {
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: output step to produce some work
            await TestDataSeeder.AddStep(db, jobId, 0, "test.output", "Output", new
            {
                outputs = """{"key":"value"}""",
            });

            // Step 1: sets pause signal
            await TestDataSeeder.AddStep(db, jobId, 1, "test.set_signal", "Set Pause", new
            {
                signal = "paused",
                execution_id = executionId.ToString(),
            });

            // Step 2: should not run
            await TestDataSeeder.AddStep(db, jobId, 2, "test.output", "After Pause", new { });

            var engine = new JobEngineBuilder(db, encryptor)
                .WithBaseDirectory(baseDir)
                .WithCleanupOnCompletion(true)
                .WithAdditionalSteps(new OutputTestStep(), new SignalSettingTestStep(db))
                .Build();

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Paused);

            // Workspace should still exist because execution was paused (not cleaned up)
            execution.ContextSnapshot.ShouldNotBeNull();
            execution.ContextSnapshot.ShouldContain("workspace");
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public async Task Workspace_CleanedUp_OnFailure()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var baseDir = Path.Combine(Path.GetTempPath(), $"engine-workspace-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(baseDir);

        try
        {
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: fails
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Bad Copy", new
            {
                source_path = Path.Combine(baseDir, "nonexistent.txt"),
                destination_path = Path.Combine(baseDir, "dest.txt"),
            });

            var engine = new JobEngineBuilder(db, encryptor)
                .WithBaseDirectory(baseDir)
                .WithCleanupOnCompletion(true)
                .Build();

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            // Workspace execution directory should have been cleaned up
            // (courier-workspaces parent may still exist, but the executionId subdir should be gone)
            var executionDir = Path.Combine(baseDir, "courier-workspaces", executionId.ToString());
            Directory.Exists(executionDir).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public async Task Workspace_PreservedWhenCleanupDisabled()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var baseDir = Path.Combine(Path.GetTempPath(), $"engine-workspace-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(baseDir);

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "workspace preserve test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            var engine = new JobEngineBuilder(db, encryptor)
                .WithBaseDirectory(baseDir)
                .WithCleanupOnCompletion(false)
                .Build();

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Workspace directory should still exist (cleanup disabled)
            var workspaceDirs = Directory.GetDirectories(baseDir, "courier-*");
            workspaceDirs.Length.ShouldBe(1);

            // Clean up manually
            Directory.Delete(tempDir, true);
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public async Task Workspace_ContextContainsWorkspacePath()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();

        var baseDir = Path.Combine(Path.GetTempPath(), $"engine-workspace-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(baseDir);

        try
        {
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "test.output", "Output", new
            {
                outputs = """{"key":"value"}""",
            });

            var engine = new JobEngineBuilder(db, encryptor)
                .WithBaseDirectory(baseDir)
                .WithCleanupOnCompletion(false)
                .WithAdditionalSteps(new OutputTestStep())
                .Build();

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);
            execution.ContextSnapshot.ShouldNotBeNull();

            // Context snapshot should include workspace path under the base directory
            execution.ContextSnapshot.ShouldContain("workspace");
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
        }
    }
}

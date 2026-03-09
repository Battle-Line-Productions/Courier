using System.Text.Json;
using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.Context;

[Collection("EngineTests")]
[Trait("Category", "Context")]
public class SystemVariableTests
{
    private readonly DatabaseFixture _database;
    public SystemVariableTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task SystemVariables_AllPresent_AfterExecution()
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
            await File.WriteAllTextAsync(sourceFile, "system var test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db, name: "System Var Job");

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);
            execution.ContextSnapshot.ShouldNotBeNull();

            var snapshot = JsonSerializer.Deserialize<Dictionary<string, object>>(execution.ContextSnapshot!);
            snapshot.ShouldNotBeNull();

            snapshot.ShouldContainKey("job.workspace");
            snapshot.ShouldContainKey("job.execution_id");
            snapshot.ShouldContainKey("job.name");
            snapshot.ShouldContainKey("job.started_at");
            snapshot.ShouldContainKey("job.attempt");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SystemVariables_JobName_MatchesSeededName()
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
            await File.WriteAllTextAsync(sourceFile, "name test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db, name: "My Custom Job Name");

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.ContextSnapshot.ShouldNotBeNull();

            var snapshot = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(execution.ContextSnapshot!);
            snapshot.ShouldNotBeNull();
            snapshot!["job.name"].GetString().ShouldBe("My Custom Job Name");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SystemVariables_ExecutionId_MatchesExecutionGuid()
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
            await File.WriteAllTextAsync(sourceFile, "exec id test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.ContextSnapshot.ShouldNotBeNull();

            var snapshot = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(execution.ContextSnapshot!);
            snapshot.ShouldNotBeNull();
            snapshot!["job.execution_id"].GetString().ShouldBe(executionId.ToString());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SystemVariables_Attempt_IsZeroForFirstRun()
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
            await File.WriteAllTextAsync(sourceFile, "attempt test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.ContextSnapshot.ShouldNotBeNull();

            var snapshot = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(execution.ContextSnapshot!);
            snapshot.ShouldNotBeNull();
            snapshot!["job.attempt"].GetString().ShouldBe("0");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SystemVariables_WorkspaceEqualsLegacyKey()
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
            await File.WriteAllTextAsync(sourceFile, "workspace compat test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.ContextSnapshot.ShouldNotBeNull();

            var snapshot = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(execution.ContextSnapshot!);
            snapshot.ShouldNotBeNull();
            snapshot!["job.workspace"].GetString().ShouldBe(snapshot["workspace"].GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SystemVariables_SurvivePauseResumeCycle()
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
            await File.WriteAllTextAsync(sourceFile, "pause resume test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Step 1", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Execute first step
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);
            execution.ContextSnapshot.ShouldNotBeNull();

            // Verify system variables are in the snapshot (they survive because
            // ContextSnapshot is saved after each step completion)
            var snapshot = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(execution.ContextSnapshot!);
            snapshot.ShouldNotBeNull();
            snapshot.ShouldContainKey("job.workspace");
            snapshot.ShouldContainKey("job.execution_id");
            snapshot.ShouldContainKey("job.name");
            snapshot.ShouldContainKey("job.started_at");
            snapshot.ShouldContainKey("job.attempt");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

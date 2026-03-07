using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.FailurePolicy;

[Collection("EngineTests")]
[Trait("Category", "FailurePolicy")]
public class StepTimeoutTests
{
    private readonly DatabaseFixture _database;
    public StepTimeoutTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task Step_CompletesWithinTimeout_Succeeds()
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
            await File.WriteAllTextAsync(sourceFile, "timeout test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step with generous timeout — should complete well within limit
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File",
                new
                {
                    source_path = sourceFile,
                    destination_path = destFile,
                },
                timeoutSeconds: 60);

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Completed);
            stepExec.DurationMs.ShouldNotBeNull();
            stepExec.DurationMs.Value.ShouldBeGreaterThanOrEqualTo(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Step_DurationMs_IsRecorded()
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
            await File.WriteAllTextAsync(sourceFile, "duration tracking test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
            stepExec.State.ShouldBe(StepExecutionState.Completed);
            stepExec.StartedAt.ShouldNotBeNull();
            stepExec.CompletedAt.ShouldNotBeNull();
            stepExec.DurationMs.ShouldNotBeNull();
            stepExec.CompletedAt.Value.ShouldBeGreaterThanOrEqualTo(stepExec.StartedAt.Value);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

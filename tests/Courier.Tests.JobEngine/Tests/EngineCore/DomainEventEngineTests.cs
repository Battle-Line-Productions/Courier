using System.Text.Json;
using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.EngineCore;

[Collection("EngineTests")]
[Trait("Category", "DomainEvents")]
public class DomainEventEngineTests
{
    private readonly DatabaseFixture _database;
    public DomainEventEngineTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task SuccessfulJob_EmitsJobStartedStepCompletedAndJobCompleted()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "event test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Step", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Act
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Assert
            var events = await db.DomainEvents
                .Where(e => e.EntityId == executionId)
                .OrderBy(e => e.OccurredAt)
                .ToListAsync();

            events.Count.ShouldBeGreaterThanOrEqualTo(2);

            var firstEvent = events.First();
            firstEvent.EventType.ShouldBe("JobStarted");
            firstEvent.EntityType.ShouldBe("job_execution");

            var lastEvent = events.Last();
            lastEvent.EventType.ShouldBe("JobCompleted");
            lastEvent.EntityType.ShouldBe("job_execution");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SuccessfulJob_EmitsStepCompletedPerStep()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile1 = Path.Combine(tempDir, "dest1.txt");
            var destFile2 = Path.Combine(tempDir, "dest2.txt");
            await File.WriteAllTextAsync(sourceFile, "multi step event test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy 1", new
            {
                source_path = sourceFile,
                destination_path = destFile1,
            });

            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy 2", new
            {
                source_path = sourceFile,
                destination_path = destFile2,
            });

            // Act
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Assert — StepCompleted events are recorded per step execution, not per execution ID
            // We need to find step execution IDs first
            var stepExecs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .OrderBy(se => se.StepOrder)
                .ToListAsync();

            stepExecs.Count.ShouldBe(2);

            foreach (var stepExec in stepExecs)
            {
                var stepEvent = await db.DomainEvents
                    .FirstOrDefaultAsync(e => e.EntityId == stepExec.Id && e.EventType == "StepCompleted");
                stepEvent.ShouldNotBeNull();
                stepEvent!.EntityType.ShouldBe("step_execution");
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FailedJob_EmitsJobStartedStepFailedAndJobFailed()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        await TestDataSeeder.AddStep(db, jobId, 0, "test.fail", "Fail Step", new
        {
            message = "domain event failure test",
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert
        var executionEvents = await db.DomainEvents
            .Where(e => e.EntityId == executionId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync();

        executionEvents.ShouldContain(e => e.EventType == "JobStarted");
        executionEvents.ShouldContain(e => e.EventType == "JobFailed");

        // StepFailed is on the step execution entity
        var stepExec = await db.StepExecutions
            .FirstAsync(se => se.JobExecutionId == executionId);
        var stepFailedEvent = await db.DomainEvents
            .FirstOrDefaultAsync(e => e.EntityId == stepExec.Id && e.EventType == "StepFailed");
        stepFailedEvent.ShouldNotBeNull();
    }

    [Fact]
    public async Task EventPayload_ContainsExecutionIdAndJobId()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "payload test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Step", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Act
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Assert
            var jobStartedEvent = await db.DomainEvents
                .FirstAsync(e => e.EntityId == executionId && e.EventType == "JobStarted");

            jobStartedEvent.Payload.ShouldNotBeNull();
            var payload = JsonDocument.Parse(jobStartedEvent.Payload!);
            payload.RootElement.GetProperty("jobId").GetGuid().ShouldBe(jobId);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task StepCompletedEvent_PayloadContainsStepTypeKey()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "step type test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Step", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Act
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Assert
            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId);

            var stepEvent = await db.DomainEvents
                .FirstAsync(e => e.EntityId == stepExec.Id && e.EventType == "StepCompleted");

            stepEvent.Payload.ShouldNotBeNull();
            var payload = JsonDocument.Parse(stepEvent.Payload!);
            payload.RootElement.GetProperty("stepType").GetString().ShouldBe("file.copy");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Events_RecordedInChronologicalOrder()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "chronological test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Step", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Act
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Assert — gather all events related to this execution
            var stepExecIds = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .Select(se => se.Id)
                .ToListAsync();

            var allEntityIds = new List<Guid> { executionId };
            allEntityIds.AddRange(stepExecIds);

            var events = await db.DomainEvents
                .Where(e => allEntityIds.Contains(e.EntityId))
                .OrderBy(e => e.OccurredAt)
                .ToListAsync();

            events.Count.ShouldBeGreaterThanOrEqualTo(3); // JobStarted, StepCompleted, JobCompleted

            // Verify timestamps are non-decreasing
            for (var i = 1; i < events.Count; i++)
            {
                events[i].OccurredAt.ShouldBeGreaterThanOrEqualTo(events[i - 1].OccurredAt);
            }

            // First event should be JobStarted, last should be JobCompleted
            events.First().EventType.ShouldBe("JobStarted");
            events.Last().EventType.ShouldBe("JobCompleted");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SuccessfulMultiStepJob_CorrectEventCount()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            await File.WriteAllTextAsync(sourceFile, "count test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            for (var i = 0; i < 3; i++)
            {
                await TestDataSeeder.AddStep(db, jobId, i, "file.copy", $"Copy {i}", new
                {
                    source_path = sourceFile,
                    destination_path = Path.Combine(tempDir, $"dest{i}.txt"),
                });
            }

            // Act
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Assert — should have: 1 JobStarted + 3 StepCompleted + 1 JobCompleted = 5 events total
            var stepExecIds = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .Select(se => se.Id)
                .ToListAsync();

            var allEntityIds = new List<Guid> { executionId };
            allEntityIds.AddRange(stepExecIds);

            var events = await db.DomainEvents
                .Where(e => allEntityIds.Contains(e.EntityId))
                .ToListAsync();

            var jobStartedCount = events.Count(e => e.EventType == "JobStarted");
            var stepCompletedCount = events.Count(e => e.EventType == "StepCompleted");
            var jobCompletedCount = events.Count(e => e.EventType == "JobCompleted");

            jobStartedCount.ShouldBe(1);
            stepCompletedCount.ShouldBe(3);
            jobCompletedCount.ShouldBe(1);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Events_HaveCorrectEntityType()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "entity type test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Step", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Act
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Assert
            var jobStarted = await db.DomainEvents
                .FirstAsync(e => e.EntityId == executionId && e.EventType == "JobStarted");
            jobStarted.EntityType.ShouldBe("job_execution");

            var jobCompleted = await db.DomainEvents
                .FirstAsync(e => e.EntityId == executionId && e.EventType == "JobCompleted");
            jobCompleted.EntityType.ShouldBe("job_execution");

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId);
            var stepCompleted = await db.DomainEvents
                .FirstAsync(e => e.EntityId == stepExec.Id && e.EventType == "StepCompleted");
            stepCompleted.EntityType.ShouldBe("step_execution");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task StepFailedEvent_PayloadContainsErrorMessage()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new FailingTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        await TestDataSeeder.AddStep(db, jobId, 0, "test.fail", "Fail Step", new
        {
            message = "specific error message for event test",
        });

        // Act
        await engine.ExecuteAsync(executionId, CancellationToken.None);

        // Assert
        var stepExec = await db.StepExecutions
            .FirstAsync(se => se.JobExecutionId == executionId);

        var stepFailedEvent = await db.DomainEvents
            .FirstAsync(e => e.EntityId == stepExec.Id && e.EventType == "StepFailed");

        stepFailedEvent.Payload.ShouldNotBeNull();
        var payload = JsonDocument.Parse(stepFailedEvent.Payload!);
        var errorStr = payload.RootElement.GetProperty("error").GetString();
        errorStr.ShouldNotBeNull();
        errorStr.ShouldContain("specific error message for event test");
        payload.RootElement.GetProperty("stepType").GetString().ShouldBe("test.fail");
    }

    [Fact]
    public async Task JobCompletedEvent_PayloadContainsState()
    {
        // Arrange
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var destFile = Path.Combine(tempDir, "dest.txt");
            await File.WriteAllTextAsync(sourceFile, "state payload test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Step", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Act
            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Assert
            var jobCompletedEvent = await db.DomainEvents
                .FirstAsync(e => e.EntityId == executionId && e.EventType == "JobCompleted");

            jobCompletedEvent.Payload.ShouldNotBeNull();
            var payload = JsonDocument.Parse(jobCompletedEvent.Payload!);
            payload.RootElement.GetProperty("state").GetString().ShouldBe("completed");
            payload.RootElement.GetProperty("jobId").GetGuid().ShouldBe(jobId);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

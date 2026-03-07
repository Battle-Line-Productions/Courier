using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.FlowControl;

[Collection("EngineTests")]
[Trait("Category", "FlowControl")]
public class IfElseEngineTests
{
    private readonly DatabaseFixture _database;
    public IfElseEngineTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task If_EqualsTrue_ThenBranchExecutes()
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

            // Step 0: if "yes" equals "yes" → true
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.if", "If Equals", new
            {
                left = "yes",
                @operator = "equals",
                right = "yes",
            });

            // Step 1: then branch — copy file
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Step 2: end if
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Then branch executed — file was copied
            File.Exists(destFile).ShouldBeTrue();

            var stepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 1);
            stepExec.State.ShouldBe(StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task If_EqualsFalse_ElseBranchExecutes()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var thenFile = Path.Combine(tempDir, "then-source.txt");
            var elseFile = Path.Combine(tempDir, "else-source.txt");
            var thenDest = Path.Combine(tempDir, "then-dest.txt");
            var elseDest = Path.Combine(tempDir, "else-dest.txt");
            await File.WriteAllTextAsync(thenFile, "then");
            await File.WriteAllTextAsync(elseFile, "else");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: if "no" equals "yes" → false
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.if", "If NotEquals", new
            {
                left = "no",
                @operator = "equals",
                right = "yes",
            });

            // Step 1: then branch — copy then file (should NOT execute)
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy Then", new
            {
                source_path = thenFile,
                destination_path = thenDest,
            });

            // Step 2: else
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.else", "Else", new { });

            // Step 3: else branch — copy else file (should execute)
            await TestDataSeeder.AddStep(db, jobId, 3, "file.copy", "Copy Else", new
            {
                source_path = elseFile,
                destination_path = elseDest,
            });

            // Step 4: end if
            await TestDataSeeder.AddStep(db, jobId, 4, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Then branch did NOT execute
            File.Exists(thenDest).ShouldBeFalse();

            // Else branch DID execute
            File.Exists(elseDest).ShouldBeTrue();

            var elseStepExec = await db.StepExecutions
                .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 3);
            elseStepExec.State.ShouldBe(StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task If_FalseCondition_NoElseBranch_SkipsAndCompletes()
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

            // Step 0: if "a" equals "b" → false
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.if", "If False", new
            {
                left = "a",
                @operator = "equals",
                right = "b",
            });

            // Step 1: then branch (should not execute)
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Step 2: end if (no else)
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Body did not execute
            File.Exists(destFile).ShouldBeFalse();

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
    public async Task If_ContainsOperator_MatchesSubstring()
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
            await File.WriteAllTextAsync(sourceFile, "contains test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: if "hello world" contains "world" → true
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.if", "If Contains", new
            {
                left = "hello world",
                @operator = "contains",
                right = "world",
            });

            // Step 1: then branch
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Step 2: end if
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Contains matched — file was copied
            File.Exists(destFile).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task If_ExistsOperator_TrueWhenContextKeyHasValue()
    {
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
            await File.WriteAllTextAsync(sourceFile, "exists test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: file.copy to produce context output at 0.copied_file
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy Source", new
            {
                source_path = sourceFile,
                destination_path = destFile1,
            });

            // Step 1: if context:0.copied_file exists → true (it has a non-empty value)
            await TestDataSeeder.AddStep(db, jobId, 1, "flow.if", "If Exists", new
            {
                left = "context:0.copied_file",
                @operator = "exists",
            });

            // Step 2: then branch — copy file again
            await TestDataSeeder.AddStep(db, jobId, 2, "file.copy", "Copy Again", new
            {
                source_path = sourceFile,
                destination_path = destFile2,
            });

            // Step 3: end if
            await TestDataSeeder.AddStep(db, jobId, 3, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // exists was true — second copy executed
            File.Exists(destFile2).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task If_UnknownOperator_ExecutionFails()
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

            // Step 0: if with unknown operator "starts_with"
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.if", "If Unknown Op", new
            {
                left = "a",
                @operator = "starts_with",
                right = "a",
            });

            // Step 1: then branch
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Step 2: end if
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Engine catches the error and marks execution as Failed
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
    public async Task If_RegexOperator_MatchesPattern()
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
            await File.WriteAllTextAsync(sourceFile, "regex test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: if "file2024.csv" matches regex pattern
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.if", "If Regex", new
            {
                left = "file2024.csv",
                @operator = "regex",
                right = @"file\d{4}\.csv",
            });

            // Step 1: then branch — copy file
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Step 2: end if
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Regex matched — file was copied
            File.Exists(destFile).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task If_RegexOperator_InvalidPattern_ExecutionFails()
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

            // Step 0: if with invalid regex pattern
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.if", "If Bad Regex", new
            {
                left = "test",
                @operator = "regex",
                right = "[invalid(",
            });

            // Step 1: then branch
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = destFile,
            });

            // Step 2: end if
            await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            // Engine catches the error and marks execution as Failed
            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

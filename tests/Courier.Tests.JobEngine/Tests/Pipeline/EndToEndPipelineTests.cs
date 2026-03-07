using System.Text.Json;
using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.Pipeline;

[Collection("EngineTests")]
[Trait("Category", "Pipeline")]
public class EndToEndPipelineTests
{
    private readonly DatabaseFixture _database;
    public EndToEndPipelineTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task ThreeStepCopyPipeline_AllComplete_WithContextChaining()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var originalFile = Path.Combine(tempDir, "original.txt");
            var copy1 = Path.Combine(tempDir, "copy1.txt");
            var copy2 = Path.Combine(tempDir, "copy2.txt");
            var copy3 = Path.Combine(tempDir, "copy3.txt");
            await File.WriteAllTextAsync(originalFile, "pipeline content");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: copy original → copy1
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy 1", new
            {
                source_path = originalFile,
                destination_path = copy1,
            });

            // Step 1: copy copy1 → copy2
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy 2", new
            {
                source_path = copy1,
                destination_path = copy2,
            });

            // Step 2: copy copy2 → copy3
            await TestDataSeeder.AddStep(db, jobId, 2, "file.copy", "Copy 3", new
            {
                source_path = copy2,
                destination_path = copy3,
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

            // All copies exist with the same content
            File.Exists(copy1).ShouldBeTrue();
            File.Exists(copy2).ShouldBeTrue();
            File.Exists(copy3).ShouldBeTrue();
            (await File.ReadAllTextAsync(copy3)).ShouldBe("pipeline content");

            // Each step should have output data with copied_file
            foreach (var stepExec in allSteps)
            {
                stepExec.OutputData.ShouldNotBeNull();
                stepExec.OutputData.ShouldContain("copied_file");
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ZipUnzipPipeline_FilesPreserved()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(tempDir, "source");
        var extractDir = Path.Combine(tempDir, "extracted");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(extractDir);

        try
        {
            // Create 3 source files
            for (var i = 0; i < 3; i++)
                await File.WriteAllTextAsync(Path.Combine(sourceDir, $"doc{i}.txt"), $"document {i} content");

            var zipPath = Path.Combine(tempDir, "archive.zip");
            var filePaths = Enumerable.Range(0, 3)
                .Select(i => Path.Combine(sourceDir, $"doc{i}.txt"))
                .ToArray();

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: zip the 3 files
            await TestDataSeeder.AddStep(db, jobId, 0, "file.zip", "Zip Files", new
            {
                source_paths = filePaths,
                output_path = zipPath,
            });

            // Step 1: unzip to extract directory
            await TestDataSeeder.AddStep(db, jobId, 1, "file.unzip", "Unzip Files", new
            {
                archive_path = zipPath,
                output_directory = extractDir,
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var allSteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .OrderBy(se => se.StepOrder)
                .ToListAsync();
            allSteps.Count.ShouldBe(2);
            allSteps.ShouldAllBe(se => se.State == StepExecutionState.Completed);

            // Verify the zip file was created
            File.Exists(zipPath).ShouldBeTrue();

            // Verify extracted files match originals
            for (var i = 0; i < 3; i++)
            {
                var extractedFile = Path.Combine(extractDir, $"doc{i}.txt");
                File.Exists(extractedFile).ShouldBeTrue();
                (await File.ReadAllTextAsync(extractedFile)).ShouldBe($"document {i} content");
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FullPipeline_CopyDeleteWithContextChaining()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var file1 = Path.Combine(tempDir, "input.txt");
            var file2 = Path.Combine(tempDir, "stage1.txt");
            var file3 = Path.Combine(tempDir, "stage2.txt");
            await File.WriteAllTextAsync(file1, "full pipeline test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: copy input → stage1 (output: 0.copied_file = stage1)
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy to Stage 1", new
            {
                source_path = file1,
                destination_path = file2,
            });

            // Step 1: copy stage1 → stage2 (output: 1.copied_file = stage2)
            await TestDataSeeder.AddStep(db, jobId, 1, "file.copy", "Copy to Stage 2", new
            {
                source_path = file2,
                destination_path = file3,
            });

            // Step 2: delete stage1 using context:0.copied_file
            await TestDataSeeder.AddStep(db, jobId, 2, "file.delete", "Delete Stage 1", new
            {
                path = "context:0.copied_file",
            });

            // Step 3: delete stage2 using context:1.copied_file
            await TestDataSeeder.AddStep(db, jobId, 3, "file.delete", "Delete Stage 2", new
            {
                path = "context:1.copied_file",
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var allSteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .OrderBy(se => se.StepOrder)
                .ToListAsync();
            allSteps.Count.ShouldBe(4);
            allSteps.ShouldAllBe(se => se.State == StepExecutionState.Completed);

            // Original input still exists
            File.Exists(file1).ShouldBeTrue();

            // Intermediate files were cleaned up via context-referenced deletes
            File.Exists(file2).ShouldBeFalse();
            File.Exists(file3).ShouldBeFalse();

            // Verify context snapshot was saved
            execution.ContextSnapshot.ShouldNotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Pipeline_WithForEachAndCondition_ComplexWorkflow()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create files to process
            var keepFile = Path.Combine(tempDir, "keep.txt");
            var deleteFile = Path.Combine(tempDir, "delete.txt");
            await File.WriteAllTextAsync(keepFile, "keep this");
            await File.WriteAllTextAsync(deleteFile, "delete this");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            var filePaths = JsonSerializer.Serialize(new[] { keepFile, deleteFile });

            // Step 0: forEach over file paths
            await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach Files", new
            {
                source = filePaths,
            });

            // Step 1: if current item contains "delete"
            await TestDataSeeder.AddStep(db, jobId, 1, "flow.if", "If Delete", new
            {
                left = "context:loop.current_item",
                @operator = "contains",
                right = "delete",
            });

            // Step 2: then branch — delete the file
            await TestDataSeeder.AddStep(db, jobId, 2, "file.delete", "Delete File", new
            {
                path = "context:loop.current_item",
            });

            // Step 3: end if
            await TestDataSeeder.AddStep(db, jobId, 3, "flow.end", "End If", new { });

            // Step 4: end forEach
            await TestDataSeeder.AddStep(db, jobId, 4, "flow.end", "End ForEach", new { });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // "keep.txt" should still exist (condition was false for its path)
            File.Exists(keepFile).ShouldBeTrue();

            // "delete.txt" should be deleted (condition was true)
            File.Exists(deleteFile).ShouldBeFalse();

            // Step 2 (delete) should have 1 execution — only for the "delete" file
            var deleteSteps = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId && se.StepOrder == 2)
                .ToListAsync();
            deleteSteps.Count.ShouldBe(1);
            deleteSteps[0].State.ShouldBe(StepExecutionState.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

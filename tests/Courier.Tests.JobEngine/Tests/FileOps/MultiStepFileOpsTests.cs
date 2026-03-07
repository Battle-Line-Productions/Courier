using System.IO.Compression;
using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.FileOps;

[Collection("EngineTests")]
[Trait("Category", "FileOps")]
public class MultiStepFileOpsTests
{
    private readonly DatabaseFixture _database;
    public MultiStepFileOpsTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task CopyThenZip_PipelineCompletes_ArchiveContainsCopiedFile()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "original.txt");
            var copiedFile = Path.Combine(tempDir, "copied.txt");
            var archivePath = Path.Combine(tempDir, "output.zip");
            await File.WriteAllTextAsync(sourceFile, "pipeline content");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: copy
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy File", new
            {
                source_path = sourceFile,
                destination_path = copiedFile
            });

            // Step 1: zip the copied file using context reference
            await TestDataSeeder.AddStep(db, jobId, 1, "file.zip", "Zip Copied", new
            {
                source_path = "context:0.copied_file",
                output_path = archivePath
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Verify both steps completed
            var stepExecs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .OrderBy(se => se.StepOrder)
                .ToListAsync();

            stepExecs.Count.ShouldBe(2);
            stepExecs[0].State.ShouldBe(StepExecutionState.Completed);
            stepExecs[1].State.ShouldBe(StepExecutionState.Completed);

            // Verify outputs
            File.Exists(copiedFile).ShouldBeTrue();
            File.Exists(archivePath).ShouldBeTrue();
            new FileInfo(archivePath).Length.ShouldBeGreaterThan(0);

            stepExecs[0].OutputData.ShouldNotBeNull();
            stepExecs[0].OutputData!.ShouldContain("copied_file");
            stepExecs[1].OutputData.ShouldNotBeNull();
            stepExecs[1].OutputData!.ShouldContain("archive_path");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ZipThenUnzip_Roundtrip_ContentPreserved()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "roundtrip.txt");
            var archivePath = Path.Combine(tempDir, "roundtrip.zip");
            var extractDir = Path.Combine(tempDir, "extracted");
            await File.WriteAllTextAsync(sourceFile, "roundtrip data");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: zip
            await TestDataSeeder.AddStep(db, jobId, 0, "file.zip", "Zip File", new
            {
                source_path = sourceFile,
                output_path = archivePath
            });

            // Step 1: unzip using context reference to archive from step 0
            await TestDataSeeder.AddStep(db, jobId, 1, "file.unzip", "Unzip File", new
            {
                archive_path = "context:0.archive_path",
                output_directory = extractDir
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            // Verify all steps completed
            var stepExecs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .OrderBy(se => se.StepOrder)
                .ToListAsync();

            stepExecs.Count.ShouldBe(2);
            stepExecs[0].State.ShouldBe(StepExecutionState.Completed);
            stepExecs[1].State.ShouldBe(StepExecutionState.Completed);

            // Verify roundtrip: extracted file matches original
            var extractedFile = Path.Combine(extractDir, "roundtrip.txt");
            File.Exists(extractedFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(extractedFile)).ShouldBe("roundtrip data");

            stepExecs[1].OutputData.ShouldNotBeNull();
            stepExecs[1].OutputData!.ShouldContain("extracted_directory");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CopyMoveThenDelete_FullPipeline_AllStepsComplete()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.txt");
            var copiedFile = Path.Combine(tempDir, "copied.txt");
            var movedFile = Path.Combine(tempDir, "moved.txt");
            await File.WriteAllTextAsync(sourceFile, "pipeline test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: copy source -> copied
            await TestDataSeeder.AddStep(db, jobId, 0, "file.copy", "Copy", new
            {
                source_path = sourceFile,
                destination_path = copiedFile
            });

            // Step 1: move copied -> moved (using context ref)
            await TestDataSeeder.AddStep(db, jobId, 1, "file.move", "Move", new
            {
                source_path = "context:0.copied_file",
                destination_path = movedFile
            });

            // Step 2: delete the moved file (using context ref)
            await TestDataSeeder.AddStep(db, jobId, 2, "file.delete", "Delete", new
            {
                path = "context:1.moved_file"
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var stepExecs = await db.StepExecutions
                .Where(se => se.JobExecutionId == executionId)
                .OrderBy(se => se.StepOrder)
                .ToListAsync();

            stepExecs.Count.ShouldBe(3);
            stepExecs.ShouldAllBe(se => se.State == StepExecutionState.Completed);

            // Source still exists (only copied, not moved)
            File.Exists(sourceFile).ShouldBeTrue();
            // Copied file was moved, so it's gone
            File.Exists(copiedFile).ShouldBeFalse();
            // Moved file was deleted
            File.Exists(movedFile).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

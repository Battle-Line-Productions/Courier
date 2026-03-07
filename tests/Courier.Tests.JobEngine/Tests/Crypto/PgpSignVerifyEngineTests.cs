using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.Crypto;

[Collection("EngineTests")]
[Trait("Category", "Crypto")]
public class PgpSignVerifyEngineTests
{
    private readonly DatabaseFixture _database;

    public PgpSignVerifyEngineTests(DatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task DetachedSign_CreatesSignatureFile()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "document.txt");
            var signatureFile = Path.Combine(tempDir, "document.txt.sig");
            await File.WriteAllTextAsync(sourceFile, "document to sign");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "pgp.sign", "Sign", new
            {
                input_path = sourceFile,
                output_path = signatureFile,
                signing_key_id = PgpTestKeys.SigningKeyId.ToString(),
                mode = "detached",
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(signatureFile).ShouldBeTrue();
            var sigBytes = await File.ReadAllBytesAsync(signatureFile);
            sigBytes.Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DetachedSignVerify_Roundtrip_VerificationSucceeds()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "document.txt");
            var signatureFile = Path.Combine(tempDir, "document.txt.sig");
            await File.WriteAllTextAsync(sourceFile, "document for detached verify");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "pgp.sign", "Sign", new
            {
                input_path = sourceFile,
                output_path = signatureFile,
                signing_key_id = PgpTestKeys.SigningKeyId.ToString(),
                mode = "detached",
            });

            await TestDataSeeder.AddStep(db, jobId, 1, "pgp.verify", "Verify", new
            {
                input_path = sourceFile,
                detached_signature_path = signatureFile,
                expected_signer_key_id = PgpTestKeys.SigningKeyId.ToString(),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var verifyStep = await db.StepExecutions
                .FirstAsync(s => s.JobExecutionId == executionId && s.StepOrder == 1);
            verifyStep.State.ShouldBe(StepExecutionState.Completed);
            verifyStep.OutputData.ShouldNotBeNullOrEmpty();
            verifyStep.OutputData.ShouldContain("true");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task InlineSignVerify_Roundtrip_VerificationSucceeds()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "document.txt");
            var signedFile = Path.Combine(tempDir, "document.txt.signed");
            await File.WriteAllTextAsync(sourceFile, "document for inline sign");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "pgp.sign", "Sign", new
            {
                input_path = sourceFile,
                output_path = signedFile,
                signing_key_id = PgpTestKeys.SigningKeyId.ToString(),
                mode = "inline",
            });

            await TestDataSeeder.AddStep(db, jobId, 1, "pgp.verify", "Verify", new
            {
                input_path = signedFile,
                expected_signer_key_id = PgpTestKeys.SigningKeyId.ToString(),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var verifyStep = await db.StepExecutions
                .FirstAsync(s => s.JobExecutionId == executionId && s.StepOrder == 1);
            verifyStep.State.ShouldBe(StepExecutionState.Completed);
            verifyStep.OutputData.ShouldNotBeNullOrEmpty();
            verifyStep.OutputData.ShouldContain("true");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ClearsignVerify_Roundtrip_VerificationSucceeds()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "document.txt");
            var clearsignedFile = Path.Combine(tempDir, "document.txt.asc");
            await File.WriteAllTextAsync(sourceFile, "document for clearsign");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "pgp.sign", "Sign", new
            {
                input_path = sourceFile,
                output_path = clearsignedFile,
                signing_key_id = PgpTestKeys.SigningKeyId.ToString(),
                mode = "clearsign",
            });

            await TestDataSeeder.AddStep(db, jobId, 1, "pgp.verify", "Verify", new
            {
                input_path = clearsignedFile,
                expected_signer_key_id = PgpTestKeys.SigningKeyId.ToString(),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var verifyStep = await db.StepExecutions
                .FirstAsync(s => s.JobExecutionId == executionId && s.StepOrder == 1);
            verifyStep.State.ShouldBe(StepExecutionState.Completed);
            verifyStep.OutputData.ShouldNotBeNullOrEmpty();
            verifyStep.OutputData.ShouldContain("true");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task Verify_TamperedFile_VerificationFails()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "document.txt");
            var signatureFile = Path.Combine(tempDir, "document.txt.sig");
            await File.WriteAllTextAsync(sourceFile, "original document content");

            // Execution 1: sign the file (detached)
            var (signJobId, signExecId) = await TestDataSeeder.SeedJob(db, "sign-for-tamper");

            await TestDataSeeder.AddStep(db, signJobId, 0, "pgp.sign", "Sign", new
            {
                input_path = sourceFile,
                output_path = signatureFile,
                signing_key_id = PgpTestKeys.SigningKeyId.ToString(),
                mode = "detached",
            });

            await engine.ExecuteAsync(signExecId, CancellationToken.None);

            var signExec = await db.JobExecutions.FirstAsync(e => e.Id == signExecId);
            signExec.State.ShouldBe(JobExecutionState.Completed);
            File.Exists(signatureFile).ShouldBeTrue();

            // Tamper with the source file
            await File.WriteAllTextAsync(sourceFile, "TAMPERED document content");

            // Execution 2: verify with the tampered file and original signature
            var (verifyJobId, verifyExecId) = await TestDataSeeder.SeedJob(db, "verify-tampered");

            await TestDataSeeder.AddStep(db, verifyJobId, 0, "pgp.verify", "Verify", new
            {
                input_path = sourceFile,
                detached_signature_path = signatureFile,
                expected_signer_key_id = PgpTestKeys.SigningKeyId.ToString(),
            });

            await engine.ExecuteAsync(verifyExecId, CancellationToken.None);

            var verifyExec = await db.JobExecutions.FirstAsync(e => e.Id == verifyExecId);
            verifyExec.State.ShouldBe(JobExecutionState.Completed);

            var verifyStep = await db.StepExecutions
                .FirstAsync(s => s.JobExecutionId == verifyExecId && s.StepOrder == 0);
            verifyStep.State.ShouldBe(StepExecutionState.Completed);
            verifyStep.OutputData.ShouldNotBeNullOrEmpty();
            verifyStep.OutputData.ShouldContain("false");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task Verify_NonPgpFile_CompletesWithInvalid()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var plainFile = Path.Combine(tempDir, "not-signed.txt");
            var fakeSigFile = Path.Combine(tempDir, "not-a-signature.sig");
            await File.WriteAllTextAsync(plainFile, "this is just plain text");
            await File.WriteAllTextAsync(fakeSigFile, "this is not a PGP signature");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "pgp.verify", "Verify", new
            {
                input_path = plainFile,
                detached_signature_path = fakeSigFile,
                expected_signer_key_id = PgpTestKeys.SigningKeyId.ToString(),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            var step = await db.StepExecutions
                .FirstAsync(s => s.JobExecutionId == executionId && s.StepOrder == 0);
            step.State.ShouldBe(StepExecutionState.Completed);
            step.OutputData.ShouldNotBeNullOrEmpty();
            step.OutputData.ShouldContain("false");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.Crypto;

[Collection("EngineTests")]
[Trait("Category", "Crypto")]
public class PgpEncryptDecryptEngineTests
{
    private readonly DatabaseFixture _database;

    public PgpEncryptDecryptEngineTests(DatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Encrypt_OutputFileExists_AndIsNotPlaintext()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var plainFile = Path.Combine(tempDir, "plain.txt");
            var encryptedFile = Path.Combine(tempDir, "encrypted.pgp");
            var content = "this is plaintext content that should be encrypted";
            await File.WriteAllTextAsync(plainFile, content);

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "pgp.encrypt", "Encrypt", new
            {
                input_path = plainFile,
                output_path = encryptedFile,
                recipient_key_ids = new[] { PgpTestKeys.EncryptionKeyId.ToString() },
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(encryptedFile).ShouldBeTrue();

            var encryptedBytes = await File.ReadAllBytesAsync(encryptedFile);
            encryptedBytes.Length.ShouldBeGreaterThan(0);

            // Encrypted content should not contain the original plaintext
            var encryptedText = await File.ReadAllTextAsync(encryptedFile);
            encryptedText.ShouldNotContain(content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task EncryptDecrypt_Roundtrip_ContentPreserved()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var plainFile = Path.Combine(tempDir, "plain.txt");
            var encryptedFile = Path.Combine(tempDir, "encrypted.pgp");
            var decryptedFile = Path.Combine(tempDir, "decrypted.txt");
            await File.WriteAllTextAsync(plainFile, "secret data");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "pgp.encrypt", "Encrypt", new
            {
                input_path = plainFile,
                output_path = encryptedFile,
                recipient_key_ids = new[] { PgpTestKeys.EncryptionKeyId.ToString() },
            });

            await TestDataSeeder.AddStep(db, jobId, 1, "pgp.decrypt", "Decrypt", new
            {
                input_path = encryptedFile,
                output_path = decryptedFile,
                private_key_id = PgpTestKeys.EncryptionKeyId.ToString(),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(decryptedFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(decryptedFile)).ShouldBe("secret data");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Encrypt_ArmoredOutput_ContainsPgpMessageHeader()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var plainFile = Path.Combine(tempDir, "plain.txt");
            var encryptedFile = Path.Combine(tempDir, "encrypted.asc");
            await File.WriteAllTextAsync(plainFile, "armored content test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "pgp.encrypt", "Encrypt", new
            {
                input_path = plainFile,
                output_path = encryptedFile,
                recipient_key_ids = new[] { PgpTestKeys.EncryptionKeyId.ToString() },
                output_format = "armored",
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(encryptedFile).ShouldBeTrue();

            var armoredText = await File.ReadAllTextAsync(encryptedFile);
            armoredText.ShouldContain("BEGIN PGP MESSAGE");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Encrypt_MultipleRecipients_DecryptSucceeds()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var plainFile = Path.Combine(tempDir, "plain.txt");
            var encryptedFile = Path.Combine(tempDir, "encrypted.pgp");
            var decryptedFile = Path.Combine(tempDir, "decrypted.txt");
            await File.WriteAllTextAsync(plainFile, "multi-recipient secret");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Encrypt to both keys (both are RSA keypairs, so both can be used for encryption)
            await TestDataSeeder.AddStep(db, jobId, 0, "pgp.encrypt", "Encrypt", new
            {
                input_path = plainFile,
                output_path = encryptedFile,
                recipient_key_ids = new[]
                {
                    PgpTestKeys.EncryptionKeyId.ToString(),
                    PgpTestKeys.SigningKeyId.ToString(),
                },
            });

            await TestDataSeeder.AddStep(db, jobId, 1, "pgp.decrypt", "Decrypt", new
            {
                input_path = encryptedFile,
                output_path = decryptedFile,
                private_key_id = PgpTestKeys.EncryptionKeyId.ToString(),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(decryptedFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(decryptedFile)).ShouldBe("multi-recipient secret");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task Encrypt_ZeroByteFile_SucceedsWithOutput()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var emptyFile = Path.Combine(tempDir, "empty.txt");
            var encryptedFile = Path.Combine(tempDir, "encrypted.pgp");
            File.Create(emptyFile).Dispose();

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "pgp.encrypt", "Encrypt Empty", new
            {
                input_path = emptyFile,
                output_path = encryptedFile,
                recipient_key_ids = new[] { PgpTestKeys.EncryptionKeyId.ToString() },
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Completed);

            File.Exists(encryptedFile).ShouldBeTrue();
            // PGP envelope adds overhead even for empty input
            new FileInfo(encryptedFile).Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task Encrypt_NonexistentInputFile_ExecutionFails()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var nonexistentFile = Path.Combine(tempDir, "does-not-exist.txt");
            var encryptedFile = Path.Combine(tempDir, "encrypted.pgp");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "pgp.encrypt", "Encrypt", new
            {
                input_path = nonexistentFile,
                output_path = encryptedFile,
                recipient_key_ids = new[] { PgpTestKeys.EncryptionKeyId.ToString() },
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            var step = await db.StepExecutions
                .FirstAsync(s => s.JobExecutionId == executionId && s.StepOrder == 0);
            step.State.ShouldBe(StepExecutionState.Failed);
            step.ErrorMessage.ShouldNotBeNullOrEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task Encrypt_NonexistentKeyId_ExecutionFails()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var plainFile = Path.Combine(tempDir, "plain.txt");
            var encryptedFile = Path.Combine(tempDir, "encrypted.pgp");
            await File.WriteAllTextAsync(plainFile, "content for nonexistent key test");

            var fakeKeyId = Guid.CreateVersion7();
            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            await TestDataSeeder.AddStep(db, jobId, 0, "pgp.encrypt", "Encrypt", new
            {
                input_path = plainFile,
                output_path = encryptedFile,
                recipient_key_ids = new[] { fakeKeyId.ToString() },
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            var step = await db.StepExecutions
                .FirstAsync(s => s.JobExecutionId == executionId && s.StepOrder == 0);
            step.State.ShouldBe(StepExecutionState.Failed);
            step.ErrorMessage.ShouldNotBeNullOrEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public async Task Decrypt_WrongKey_ExecutionFails()
    {
        await PgpTestKeys.EnsureCreatedAsync(_database);
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor).Build();

        var tempDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var plainFile = Path.Combine(tempDir, "plain.txt");
            var encryptedFile = Path.Combine(tempDir, "encrypted.pgp");
            var decryptedFile = Path.Combine(tempDir, "decrypted.txt");
            await File.WriteAllTextAsync(plainFile, "secret for wrong key test");

            var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

            // Step 0: encrypt with EncryptionKeyId
            await TestDataSeeder.AddStep(db, jobId, 0, "pgp.encrypt", "Encrypt", new
            {
                input_path = plainFile,
                output_path = encryptedFile,
                recipient_key_ids = new[] { PgpTestKeys.EncryptionKeyId.ToString() },
            });

            // Step 1: decrypt with SigningKeyId (wrong key — can't decrypt)
            await TestDataSeeder.AddStep(db, jobId, 1, "pgp.decrypt", "Decrypt", new
            {
                input_path = encryptedFile,
                output_path = decryptedFile,
                private_key_id = PgpTestKeys.SigningKeyId.ToString(),
            });

            await engine.ExecuteAsync(executionId, CancellationToken.None);

            var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
            execution.State.ShouldBe(JobExecutionState.Failed);

            var encryptStep = await db.StepExecutions
                .FirstAsync(s => s.JobExecutionId == executionId && s.StepOrder == 0);
            encryptStep.State.ShouldBe(StepExecutionState.Completed);

            var decryptStep = await db.StepExecutions
                .FirstAsync(s => s.JobExecutionId == executionId && s.StepOrder == 1);
            decryptStep.State.ShouldBe(StepExecutionState.Failed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

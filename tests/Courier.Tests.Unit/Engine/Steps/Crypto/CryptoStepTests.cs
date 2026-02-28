using Courier.Domain.Engine;
using Courier.Features.Engine.Crypto;
using Courier.Features.Engine.Steps.Crypto;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Steps.Crypto;

public class CryptoStepTests
{
    private readonly ICryptoProvider _provider;

    public CryptoStepTests()
    {
        _provider = Substitute.For<ICryptoProvider>();
    }

    // ── TypeKey tests ──────────────────────────────────────────────────

    [Fact]
    public void PgpEncryptStep_TypeKey_IsCorrect()
    {
        new PgpEncryptStep(_provider).TypeKey.ShouldBe("pgp.encrypt");
    }

    [Fact]
    public void PgpDecryptStep_TypeKey_IsCorrect()
    {
        new PgpDecryptStep(_provider).TypeKey.ShouldBe("pgp.decrypt");
    }

    [Fact]
    public void PgpSignStep_TypeKey_IsCorrect()
    {
        new PgpSignStep(_provider).TypeKey.ShouldBe("pgp.sign");
    }

    [Fact]
    public void PgpVerifyStep_TypeKey_IsCorrect()
    {
        new PgpVerifyStep(_provider).TypeKey.ShouldBe("pgp.verify");
    }

    // ── Validation tests ───────────────────────────────────────────────

    [Fact]
    public async Task PgpEncryptStep_ValidateAsync_MissingInputPath_Fails()
    {
        var step = new PgpEncryptStep(_provider);
        var config = new StepConfiguration("""{"recipient_key_ids": ["00000000-0000-0000-0000-000000000001"]}""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("input_path");
    }

    [Fact]
    public async Task PgpEncryptStep_ValidateAsync_EmptyRecipients_Fails()
    {
        var step = new PgpEncryptStep(_provider);
        var config = new StepConfiguration("""{"input_path": "/tmp/file.txt", "recipient_key_ids": []}""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("recipient_key_ids");
    }

    [Fact]
    public async Task PgpEncryptStep_ValidateAsync_Valid_Succeeds()
    {
        var step = new PgpEncryptStep(_provider);
        var config = new StepConfiguration("""{"input_path": "/tmp/file.txt", "recipient_key_ids": ["00000000-0000-0000-0000-000000000001"]}""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task PgpDecryptStep_ValidateAsync_MissingPrivateKeyId_Fails()
    {
        var step = new PgpDecryptStep(_provider);
        var config = new StepConfiguration("""{"input_path": "/tmp/file.pgp"}""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("private_key_id");
    }

    [Fact]
    public async Task PgpDecryptStep_ValidateAsync_MissingInputPath_Fails()
    {
        var step = new PgpDecryptStep(_provider);
        var config = new StepConfiguration("""{"private_key_id": "00000000-0000-0000-0000-000000000001"}""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("input_path");
    }

    [Fact]
    public async Task PgpSignStep_ValidateAsync_MissingSigningKeyId_Fails()
    {
        var step = new PgpSignStep(_provider);
        var config = new StepConfiguration("""{"input_path": "/tmp/file.txt"}""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("signing_key_id");
    }

    [Fact]
    public async Task PgpVerifyStep_ValidateAsync_MissingInputPath_Fails()
    {
        var step = new PgpVerifyStep(_provider);
        var config = new StepConfiguration("""{}""");

        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("input_path");
    }

    // ── ExecuteAsync tests ─────────────────────────────────────────────

    [Fact]
    public async Task PgpEncryptStep_ExecuteAsync_DelegatesToProvider()
    {
        var recipientId = Guid.NewGuid();
        _provider.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<IProgress<CryptoProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new CryptoResult(true, 1024, "/tmp/output.pgp", null));

        var step = new PgpEncryptStep(_provider);
        var config = new StepConfiguration($$"""
        {
            "input_path": "/tmp/data.txt",
            "output_path": "/tmp/output.pgp",
            "recipient_key_ids": ["{{recipientId}}"]
        }
        """);

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.BytesProcessed.ShouldBe(1024);
        result.Outputs!["encrypted_file"].ShouldBe("/tmp/output.pgp");

        await _provider.Received(1).EncryptAsync(
            Arg.Is<EncryptRequest>(r =>
                r.InputPath == "/tmp/data.txt" &&
                r.OutputPath == "/tmp/output.pgp" &&
                r.RecipientKeyIds.Count == 1 &&
                r.RecipientKeyIds[0] == recipientId &&
                r.Format == OutputFormat.Binary),
            Arg.Any<IProgress<CryptoProgress>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PgpEncryptStep_ExecuteAsync_ProviderFails_ReturnsFail()
    {
        _provider.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<IProgress<CryptoProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new CryptoResult(false, 0, "", "Encryption failed: key expired"));

        var step = new PgpEncryptStep(_provider);
        var config = new StepConfiguration($$"""
        {
            "input_path": "/tmp/data.txt",
            "recipient_key_ids": ["{{Guid.NewGuid()}}"]
        }
        """);

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Encryption failed");
    }

    [Fact]
    public async Task PgpDecryptStep_ExecuteAsync_OutputsDecryptedFile()
    {
        var keyId = Guid.NewGuid();
        _provider.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<IProgress<CryptoProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new CryptoResult(true, 2048, "/tmp/data.txt", null));

        var step = new PgpDecryptStep(_provider);
        var config = new StepConfiguration($$"""
        {
            "input_path": "/tmp/data.txt.pgp",
            "output_path": "/tmp/data.txt",
            "private_key_id": "{{keyId}}"
        }
        """);

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.BytesProcessed.ShouldBe(2048);
        result.Outputs!["decrypted_file"].ShouldBe("/tmp/data.txt");

        await _provider.Received(1).DecryptAsync(
            Arg.Is<DecryptRequest>(r =>
                r.InputPath == "/tmp/data.txt.pgp" &&
                r.OutputPath == "/tmp/data.txt" &&
                r.PrivateKeyId == keyId &&
                r.VerifySignature == false),
            Arg.Any<IProgress<CryptoProgress>?>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("detached", SignatureMode.Detached)]
    [InlineData("inline", SignatureMode.Inline)]
    [InlineData("clearsign", SignatureMode.Clearsign)]
    public async Task PgpSignStep_ExecuteAsync_ParsesMode(string modeStr, SignatureMode expectedMode)
    {
        var keyId = Guid.NewGuid();
        _provider.SignAsync(Arg.Any<SignRequest>(), Arg.Any<IProgress<CryptoProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new CryptoResult(true, 512, "/tmp/data.txt.sig", null));

        var step = new PgpSignStep(_provider);
        var config = new StepConfiguration($$"""
        {
            "input_path": "/tmp/data.txt",
            "output_path": "/tmp/data.txt.sig",
            "signing_key_id": "{{keyId}}",
            "mode": "{{modeStr}}"
        }
        """);

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Outputs!["signature_file"].ShouldBe("/tmp/data.txt.sig");

        await _provider.Received(1).SignAsync(
            Arg.Is<SignRequest>(r =>
                r.InputPath == "/tmp/data.txt" &&
                r.SigningKeyId == keyId &&
                r.Mode == expectedMode),
            Arg.Any<IProgress<CryptoProgress>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PgpVerifyStep_ExecuteAsync_OutputsVerifyStatus()
    {
        var signerKeyId = Guid.NewGuid();
        var timestamp = new DateTime(2026, 2, 28, 12, 0, 0, DateTimeKind.Utc);

        _provider.VerifyAsync(Arg.Any<VerifyRequest>(), Arg.Any<IProgress<CryptoProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyResult(true, VerifyStatus.Valid, "ABCDEF1234567890", timestamp));

        var step = new PgpVerifyStep(_provider);
        var config = new StepConfiguration($$"""
        {
            "input_path": "/tmp/data.txt",
            "detached_signature_path": "/tmp/data.txt.sig",
            "expected_signer_key_id": "{{signerKeyId}}"
        }
        """);

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Outputs!["verify_status"].ShouldBe("Valid");
        result.Outputs!["is_valid"].ShouldBe(true);
        result.Outputs!["signer_fingerprint"].ShouldBe("ABCDEF1234567890");
        result.Outputs!.ShouldContainKey("signature_timestamp");

        await _provider.Received(1).VerifyAsync(
            Arg.Is<VerifyRequest>(r =>
                r.InputPath == "/tmp/data.txt" &&
                r.DetachedSignaturePath == "/tmp/data.txt.sig" &&
                r.ExpectedSignerKeyId == signerKeyId),
            Arg.Any<IProgress<CryptoProgress>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PgpVerifyStep_ExecuteAsync_NullOptionalFields_OmitsFromOutputs()
    {
        _provider.VerifyAsync(Arg.Any<VerifyRequest>(), Arg.Any<IProgress<CryptoProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyResult(false, VerifyStatus.Invalid, null, null));

        var step = new PgpVerifyStep(_provider);
        var config = new StepConfiguration("""{"input_path": "/tmp/data.txt"}""");

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Outputs!["verify_status"].ShouldBe("Invalid");
        result.Outputs!["is_valid"].ShouldBe(false);
        result.Outputs!.ShouldNotContainKey("signer_fingerprint");
        result.Outputs!.ShouldNotContainKey("signature_timestamp");
    }

    // ── ResolveContextRef tests ────────────────────────────────────────

    [Fact]
    public async Task ResolveContextRef_ResolvesFromContext()
    {
        var keyId = Guid.NewGuid();
        _provider.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<IProgress<CryptoProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new CryptoResult(true, 512, "/tmp/resolved.pgp", null));

        var context = new JobContext();
        context.Set("1.downloaded_file", "/tmp/resolved_input.txt");

        var step = new PgpEncryptStep(_provider);
        var config = new StepConfiguration($$"""
        {
            "input_path": "context:1.downloaded_file",
            "recipient_key_ids": ["{{keyId}}"]
        }
        """);

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeTrue();
        await _provider.Received(1).EncryptAsync(
            Arg.Is<EncryptRequest>(r => r.InputPath == "/tmp/resolved_input.txt"),
            Arg.Any<IProgress<CryptoProgress>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveContextRef_LiteralValue_ReturnsAsIs()
    {
        var keyId = Guid.NewGuid();
        _provider.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<IProgress<CryptoProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new CryptoResult(true, 512, "/tmp/output.pgp", null));

        var step = new PgpEncryptStep(_provider);
        var config = new StepConfiguration($$"""
        {
            "input_path": "/path/to/file.txt",
            "recipient_key_ids": ["{{keyId}}"]
        }
        """);

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeTrue();
        await _provider.Received(1).EncryptAsync(
            Arg.Is<EncryptRequest>(r => r.InputPath == "/path/to/file.txt"),
            Arg.Any<IProgress<CryptoProgress>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ResolveContextRef_MissingContextKey_ThrowsInvalidOperationException()
    {
        var step = new PgpEncryptStep(_provider);
        var config = new StepConfiguration($$"""
        {
            "input_path": "context:missing_key",
            "recipient_key_ids": ["{{Guid.NewGuid()}}"]
        }
        """);

        Should.Throw<InvalidOperationException>(
            () => step.ExecuteAsync(config, new JobContext(), CancellationToken.None));
    }
}

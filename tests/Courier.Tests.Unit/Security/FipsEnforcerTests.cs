using Courier.Features.Security;
using Shouldly;

namespace Courier.Tests.Unit.Security;

public class FipsEnforcerTests
{
    private readonly FipsEnforcer _enforcer = new();

    // --- ValidateSshAlgorithms ---

    [Fact]
    public void ValidateSshAlgorithms_AllApprovedAlgorithms_ReturnsValid()
    {
        // Arrange
        var json = """
        {
            "keyExchange": ["ecdh-sha2-nistp256", "diffie-hellman-group14-sha256"],
            "encryption": ["aes256-ctr", "aes128-gcm@openssh.com"],
            "hmac": ["hmac-sha2-256", "hmac-sha2-512-etm@openssh.com"],
            "hostKey": ["rsa-sha2-256", "ecdsa-sha2-nistp384"]
        }
        """;

        // Act
        var result = _enforcer.ValidateSshAlgorithms(json, fipsEnabled: true);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Violations.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateSshAlgorithms_ProhibitedKeyExchange_ReturnsViolation()
    {
        // Arrange
        var json = """
        {
            "keyExchange": ["curve25519-sha256"]
        }
        """;

        // Act
        var result = _enforcer.ValidateSshAlgorithms(json, fipsEnabled: true);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Violations.Count.ShouldBe(1);
        result.Violations[0].ShouldContain("curve25519-sha256");
    }

    [Fact]
    public void ValidateSshAlgorithms_ProhibitedEncryption_ReturnsViolation()
    {
        // Arrange
        var json = """
        {
            "encryption": ["chacha20-poly1305@openssh.com"]
        }
        """;

        // Act
        var result = _enforcer.ValidateSshAlgorithms(json, fipsEnabled: true);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Violations.Count.ShouldBe(1);
        result.Violations[0].ShouldContain("chacha20-poly1305@openssh.com");
    }

    [Fact]
    public void ValidateSshAlgorithms_FipsDisabled_ReturnsValid()
    {
        // Arrange
        var json = """
        {
            "keyExchange": ["curve25519-sha256"],
            "encryption": ["chacha20-poly1305@openssh.com"]
        }
        """;

        // Act
        var result = _enforcer.ValidateSshAlgorithms(json, fipsEnabled: false);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Violations.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValidateSshAlgorithms_NullOrEmpty_ReturnsValid(string? algorithmsJson)
    {
        // Act
        var result = _enforcer.ValidateSshAlgorithms(algorithmsJson, fipsEnabled: true);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateSshAlgorithms_CaseInsensitive_ReturnsValid()
    {
        // Arrange
        var json = """
        {
            "encryption": ["AES256-CTR", "AES128-GCM@OPENSSH.COM"]
        }
        """;

        // Act
        var result = _enforcer.ValidateSshAlgorithms(json, fipsEnabled: true);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateSshAlgorithms_MixedApprovedAndProhibited_ReturnsViolation()
    {
        // Arrange
        var json = """
        {
            "encryption": ["aes256-ctr", "chacha20-poly1305@openssh.com"]
        }
        """;

        // Act
        var result = _enforcer.ValidateSshAlgorithms(json, fipsEnabled: true);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Violations.Count.ShouldBe(1);
        result.Violations[0].ShouldContain("chacha20-poly1305@openssh.com");
    }

    [Fact]
    public void ValidateSshAlgorithms_InvalidJson_ReturnsViolation()
    {
        // Arrange
        var json = "not valid json{{{";

        // Act
        var result = _enforcer.ValidateSshAlgorithms(json, fipsEnabled: true);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Violations.Count.ShouldBe(1);
        result.Violations[0].ShouldContain("Invalid SSH algorithms JSON format");
    }

    // --- ValidatePgpAlgorithm ---

    [Theory]
    [InlineData("rsa", 4096)]
    [InlineData("rsa", 2048)]
    [InlineData("aes256", null)]
    public void ValidatePgpAlgorithm_ApprovedAlgorithm_ReturnsValid(string algorithm, int? keySize)
    {
        // Act
        var result = _enforcer.ValidatePgpAlgorithm(algorithm, fipsEnabled: true, keySize);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("sha1")]
    [InlineData("md5")]
    [InlineData("des")]
    [InlineData("3des")]
    [InlineData("idea")]
    [InlineData("cast5")]
    public void ValidatePgpAlgorithm_ProhibitedAlgorithm_ReturnsViolation(string algorithm)
    {
        // Act
        var result = _enforcer.ValidatePgpAlgorithm(algorithm, fipsEnabled: true);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Violations.ShouldContain(v => v.Contains(algorithm));
    }

    [Theory]
    [InlineData("ed25519")]
    [InlineData("Ed25519")]
    [InlineData("curve25519")]
    [InlineData("Curve25519")]
    public void ValidatePgpAlgorithm_Ed25519OrCurve25519_ReturnsViolation(string algorithm)
    {
        // Act
        var result = _enforcer.ValidatePgpAlgorithm(algorithm, fipsEnabled: true);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Violations.ShouldContain(v => v.Contains("not FIPS 140-2 approved"));
    }

    [Fact]
    public void ValidatePgpAlgorithm_RsaKeySizeTooSmall_ReturnsViolation()
    {
        // Act
        var result = _enforcer.ValidatePgpAlgorithm("rsa", fipsEnabled: true, keySize: 1024);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Violations.ShouldContain(v => v.Contains("below FIPS minimum of 2048"));
    }

    [Fact]
    public void ValidatePgpAlgorithm_FipsDisabled_AllowsAnything()
    {
        // Act
        var result = _enforcer.ValidatePgpAlgorithm("md5", fipsEnabled: false);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    // --- IsAlgorithmApproved ---

    [Theory]
    [InlineData("keyexchange", "ecdh-sha2-nistp256", true)]
    [InlineData("encryption", "aes256-ctr", true)]
    [InlineData("hmac", "hmac-sha2-256", true)]
    [InlineData("hostkey", "rsa-sha2-512", true)]
    [InlineData("encryption", "chacha20-poly1305@openssh.com", false)]
    [InlineData("hmac", "hmac-md5", false)]
    [InlineData("invalidcategory", "aes256-ctr", false)]
    public void IsAlgorithmApproved_ReturnsExpected(string category, string algorithm, bool expected)
    {
        // Act
        var result = _enforcer.IsAlgorithmApproved(category, algorithm);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void ValidateSshAlgorithms_MultipleCategoriesWithViolations_ReturnsAllViolations()
    {
        // Arrange
        var json = """
        {
            "keyExchange": ["curve25519-sha256"],
            "encryption": ["chacha20-poly1305@openssh.com"],
            "hmac": ["hmac-md5"]
        }
        """;

        // Act
        var result = _enforcer.ValidateSshAlgorithms(json, fipsEnabled: true);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Violations.Count.ShouldBe(3);
    }
}

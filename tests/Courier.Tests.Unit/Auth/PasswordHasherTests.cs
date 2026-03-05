using Courier.Features.Auth;
using Shouldly;

namespace Courier.Tests.Unit.Auth;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ReturnsArgon2idFormat()
    {
        var hash = PasswordHasher.Hash("password123");

        hash.ShouldStartWith("$argon2id$");
        // Format: $argon2id$<salt>$<hash> — 3 non-empty parts when split
        hash.Split('$', StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(3);
    }

    [Fact]
    public void Hash_DifferentHashesForSamePassword()
    {
        var hash1 = PasswordHasher.Hash("password123");
        var hash2 = PasswordHasher.Hash("password123");

        hash1.ShouldNotBe(hash2); // different salts each time
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = PasswordHasher.Hash("mySecretPassword");

        PasswordHasher.Verify("mySecretPassword", hash).ShouldBeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.Hash("mySecretPassword");

        PasswordHasher.Verify("wrongPassword", hash).ShouldBeFalse();
    }

    [Fact]
    public void Verify_MalformedHash_ReturnsFalse()
    {
        PasswordHasher.Verify("password", "invalid_hash").ShouldBeFalse();
    }

    [Fact]
    public void Verify_EmptyHash_ReturnsFalse()
    {
        PasswordHasher.Verify("password", "").ShouldBeFalse();
    }

    [Fact]
    public void Hash_EmptyPassword_ThrowsArgumentException()
    {
        // Argon2id requires a non-empty password
        Should.Throw<ArgumentException>(() => PasswordHasher.Hash(""));
    }
}

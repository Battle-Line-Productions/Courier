using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using System.Text;

namespace Courier.Features.Auth;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemorySize = 65536; // 64MB
    private const int Iterations = 4;
    private const int DegreeOfParallelism = 8;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = HashWithSalt(password, salt);

        // Format: $argon2id$salt$hash (base64 encoded)
        return $"$argon2id${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts[0] != "argon2id")
            return false;

        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);
        var actualHash = HashWithSalt(password, salt);

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private static byte[] HashWithSalt(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.MemorySize = MemorySize;
        argon2.Iterations = Iterations;
        argon2.DegreeOfParallelism = DegreeOfParallelism;
        return argon2.GetBytes(HashSize);
    }
}

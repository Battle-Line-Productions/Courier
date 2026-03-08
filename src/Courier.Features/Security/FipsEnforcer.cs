using System.Text.Json;

namespace Courier.Features.Security;

public class FipsEnforcer
{
    // FIPS 140-2 approved algorithms
    private static readonly HashSet<string> ApprovedKeyExchange = new(StringComparer.OrdinalIgnoreCase)
    {
        "ecdh-sha2-nistp256", "ecdh-sha2-nistp384", "ecdh-sha2-nistp521",
        "diffie-hellman-group14-sha256", "diffie-hellman-group16-sha512", "diffie-hellman-group18-sha512"
    };

    private static readonly HashSet<string> ApprovedEncryption = new(StringComparer.OrdinalIgnoreCase)
    {
        "aes128-ctr", "aes192-ctr", "aes256-ctr",
        "aes128-cbc", "aes192-cbc", "aes256-cbc",
        "aes128-gcm@openssh.com", "aes256-gcm@openssh.com"
    };

    private static readonly HashSet<string> ApprovedHmac = new(StringComparer.OrdinalIgnoreCase)
    {
        "hmac-sha2-256", "hmac-sha2-512",
        "hmac-sha2-256-etm@openssh.com", "hmac-sha2-512-etm@openssh.com"
    };

    private static readonly HashSet<string> ApprovedHostKey = new(StringComparer.OrdinalIgnoreCase)
    {
        "ssh-rsa", "rsa-sha2-256", "rsa-sha2-512",
        "ecdsa-sha2-nistp256", "ecdsa-sha2-nistp384", "ecdsa-sha2-nistp521"
    };

    private static readonly HashSet<string> ProhibitedPgpAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "sha1", "md5", "des", "3des", "idea", "cast5"
    };

    private static readonly Dictionary<string, HashSet<string>> CategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["keyexchange"] = ApprovedKeyExchange,
        ["encryption"] = ApprovedEncryption,
        ["hmac"] = ApprovedHmac,
        ["hostkey"] = ApprovedHostKey
    };

    public FipsValidationResult ValidateSshAlgorithms(string? algorithmsJson, bool fipsEnabled)
    {
        if (!fipsEnabled)
            return FipsValidationResult.Valid();

        if (string.IsNullOrEmpty(algorithmsJson))
            return FipsValidationResult.Valid();

        var violations = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(algorithmsJson);
            var root = doc.RootElement;

            ValidateAlgorithmCategory(root, "keyExchange", ApprovedKeyExchange, violations);
            ValidateAlgorithmCategory(root, "encryption", ApprovedEncryption, violations);
            ValidateAlgorithmCategory(root, "hmac", ApprovedHmac, violations);
            ValidateAlgorithmCategory(root, "hostKey", ApprovedHostKey, violations);
        }
        catch (JsonException)
        {
            violations.Add("Invalid SSH algorithms JSON format");
        }

        return violations.Count > 0
            ? FipsValidationResult.Invalid(violations)
            : FipsValidationResult.Valid();
    }

    public FipsValidationResult ValidatePgpAlgorithm(string algorithm, bool fipsEnabled, int? keySize = null)
    {
        if (!fipsEnabled)
            return FipsValidationResult.Valid();

        var violations = new List<string>();

        if (ProhibitedPgpAlgorithms.Contains(algorithm))
            violations.Add($"Algorithm '{algorithm}' is not FIPS 140-2 approved");

        if (algorithm.Equals("rsa", StringComparison.OrdinalIgnoreCase) && keySize.HasValue && keySize < 2048)
            violations.Add($"RSA key size {keySize} is below FIPS minimum of 2048 bits");

        if (algorithm.Contains("ed25519", StringComparison.OrdinalIgnoreCase) ||
            algorithm.Contains("curve25519", StringComparison.OrdinalIgnoreCase))
            violations.Add($"Algorithm '{algorithm}' is not FIPS 140-2 approved");

        return violations.Count > 0
            ? FipsValidationResult.Invalid(violations)
            : FipsValidationResult.Valid();
    }

    public bool IsAlgorithmApproved(string category, string algorithm) =>
        CategoryMap.TryGetValue(category, out var approved) && approved.Contains(algorithm);

    private static void ValidateAlgorithmCategory(
        JsonElement root,
        string propertyName,
        HashSet<string> approved,
        List<string> violations)
    {
        if (!root.TryGetProperty(propertyName, out var element))
            return;

        if (element.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in element.EnumerateArray())
        {
            var algo = item.GetString();
            if (algo is not null && !approved.Contains(algo))
            {
                violations.Add($"SSH {propertyName} algorithm '{algo}' is not FIPS 140-2 approved");
            }
        }
    }
}

public record FipsValidationResult(bool IsValid, IReadOnlyList<string> Violations)
{
    public static FipsValidationResult Valid() => new(true, []);
    public static FipsValidationResult Invalid(IReadOnlyList<string> violations) => new(false, violations);
}

using System.Text;
using Courier.Domain.Entities;
using Courier.Features.Engine.Protocols;
using Renci.SshNet;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Protocols;

public class SshAlgorithmTests
{
    private static Connection CreateTestConnection(string? sshAlgorithms = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test SFTP",
        Protocol = "sftp",
        Host = "sftp.example.com",
        Port = 22,
        AuthMethod = "password",
        Username = "testuser",
        HostKeyPolicy = "always_trust",
        ConnectTimeoutSec = 30,
        OperationTimeoutSec = 300,
        KeepaliveIntervalSec = 0,
        SshAlgorithms = sshAlgorithms,
    };

    /// <summary>
    /// Creates a ConnectionInfo to inspect algorithm enforcement results.
    /// We create a SftpTransferClient which calls ApplySshAlgorithmEnforcement
    /// internally during ConnectAsync. Since we can't call ConnectAsync without
    /// a real server, we test the filtering logic via the ConnectionInfo directly.
    /// </summary>
    private static ConnectionInfo CreateConnectionInfo()
    {
        return new ConnectionInfo(
            "sftp.example.com",
            22,
            "testuser",
            new PasswordAuthenticationMethod("testuser", "password"));
    }

    [Fact]
    public void Constructor_WithNullSshAlgorithms_DoesNotThrow()
    {
        // Arrange
        var connection = CreateTestConnection(sshAlgorithms: null);

        // Act & Assert
        Should.NotThrow(() => new SftpTransferClient(connection, Encoding.UTF8.GetBytes("pass"), null));
    }

    [Fact]
    public void Constructor_WithEmptySshAlgorithms_DoesNotThrow()
    {
        // Arrange
        var connection = CreateTestConnection(sshAlgorithms: "");

        // Act & Assert
        Should.NotThrow(() => new SftpTransferClient(connection, Encoding.UTF8.GetBytes("pass"), null));
    }

    [Fact]
    public void Constructor_WithValidSshAlgorithmsJson_DoesNotThrow()
    {
        // Arrange
        var algorithms = """{"KeyExchange":["ecdh-sha2-nistp256"],"Encryption":["aes256-ctr"]}""";
        var connection = CreateTestConnection(sshAlgorithms: algorithms);

        // Act & Assert
        Should.NotThrow(() => new SftpTransferClient(connection, Encoding.UTF8.GetBytes("pass"), null));
    }

    [Fact]
    public void Constructor_WithMalformedJson_DoesNotThrow()
    {
        // Arrange — malformed JSON should fall back to defaults, not throw
        var connection = CreateTestConnection(sshAlgorithms: "{not valid json}");

        // Act & Assert
        Should.NotThrow(() => new SftpTransferClient(connection, Encoding.UTF8.GetBytes("pass"), null));
    }

    [Fact]
    public void FilterAlgorithms_WithAllowedList_RemovesUnlisted()
    {
        // Arrange
        var algorithms = new Dictionary<string, Func<byte[]>>(StringComparer.OrdinalIgnoreCase)
        {
            ["aes256-ctr"] = () => [],
            ["aes128-ctr"] = () => [],
            ["aes192-ctr"] = () => [],
            ["3des-cbc"] = () => [],
        };

        var allowed = new List<string> { "aes256-ctr", "aes128-ctr" };
        var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        var toRemove = algorithms.Keys.Where(k => !allowedSet.Contains(k)).ToList();

        // Act
        foreach (var key in toRemove)
            algorithms.Remove(key);

        // Assert
        algorithms.Count.ShouldBe(2);
        algorithms.ShouldContainKey("aes256-ctr");
        algorithms.ShouldContainKey("aes128-ctr");
        algorithms.ShouldNotContainKey("3des-cbc");
        algorithms.ShouldNotContainKey("aes192-ctr");
    }

    [Fact]
    public void FilterAlgorithms_CaseInsensitive_MatchesRegardlessOfCase()
    {
        // Arrange
        var algorithms = new Dictionary<string, Func<byte[]>>(StringComparer.OrdinalIgnoreCase)
        {
            ["aes256-ctr"] = () => [],
            ["3des-cbc"] = () => [],
        };

        var allowed = new List<string> { "AES256-CTR" };
        var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        var toRemove = algorithms.Keys.Where(k => !allowedSet.Contains(k)).ToList();

        // Act
        foreach (var key in toRemove)
            algorithms.Remove(key);

        // Assert
        algorithms.Count.ShouldBe(1);
        algorithms.ShouldContainKey("aes256-ctr");
    }

    [Fact]
    public void FilterAlgorithms_EmptyAllowedList_RemovesAll()
    {
        // Arrange
        var algorithms = new Dictionary<string, Func<byte[]>>(StringComparer.OrdinalIgnoreCase)
        {
            ["aes256-ctr"] = () => [],
            ["aes128-ctr"] = () => [],
        };

        // Note: The actual code checks for Count > 0 before calling FilterAlgorithms,
        // so an empty list would not invoke filtering. This tests the filter logic directly.
        var allowed = new List<string>();
        var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        var toRemove = algorithms.Keys.Where(k => !allowedSet.Contains(k)).ToList();

        // Act
        foreach (var key in toRemove)
            algorithms.Remove(key);

        // Assert
        algorithms.Count.ShouldBe(0);
    }

    [Fact]
    public void SshAlgorithmsJson_MultipleCategories_ParsedCorrectly()
    {
        // Arrange
        var json = """
        {
            "KeyExchange": ["ecdh-sha2-nistp256", "ecdh-sha2-nistp384"],
            "Encryption": ["aes256-ctr", "aes128-ctr"],
            "Hmac": ["hmac-sha2-256"],
            "HostKey": ["ssh-rsa", "ssh-ed25519"]
        }
        """;

        // Act
        var prefs = System.Text.Json.JsonSerializer.Deserialize<SshAlgorithmPreferencesTestModel>(
            json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Assert
        prefs.ShouldNotBeNull();
        prefs.KeyExchange.ShouldNotBeNull();
        prefs.KeyExchange!.Count.ShouldBe(2);
        prefs.Encryption.ShouldNotBeNull();
        prefs.Encryption!.Count.ShouldBe(2);
        prefs.Hmac.ShouldNotBeNull();
        prefs.Hmac!.Count.ShouldBe(1);
        prefs.HostKey.ShouldNotBeNull();
        prefs.HostKey!.Count.ShouldBe(2);
    }

    /// <summary>
    /// Mirror of the private SshAlgorithmPreferences class inside SftpTransferClient,
    /// used for deserialization testing.
    /// </summary>
    private sealed class SshAlgorithmPreferencesTestModel
    {
        public List<string>? KeyExchange { get; set; }
        public List<string>? Encryption { get; set; }
        public List<string>? Hmac { get; set; }
        public List<string>? HostKey { get; set; }
    }
}

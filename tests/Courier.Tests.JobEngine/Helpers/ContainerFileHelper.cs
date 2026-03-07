using DotNet.Testcontainers.Containers;

namespace Courier.Tests.JobEngine.Helpers;

public static class ContainerFileHelper
{
    public static async Task AssertFileExistsInContainer(IContainer container, string path)
    {
        var result = await container.ExecAsync(["test", "-f", path]);
        if (result.ExitCode != 0)
            throw new Exception($"File not found in container: {path}");
    }

    public static async Task<string> ReadFileFromContainer(IContainer container, string path)
    {
        var result = await container.ExecAsync(["cat", path]);
        if (result.ExitCode != 0)
            throw new Exception($"Failed to read file from container: {path} (exit code {result.ExitCode})");
        return result.Stdout;
    }

    public static async Task WriteFileToContainer(IContainer container, string path, string content)
    {
        var dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(dir))
            await container.ExecAsync(["mkdir", "-p", dir]);

        // Use printf to avoid echo adding trailing newline
        await container.ExecAsync(["sh", "-c", $"printf '%s' '{EscapeForShell(content)}' > {path}"]);
    }

    public static async Task<string[]> ListDirectoryInContainer(IContainer container, string path)
    {
        var result = await container.ExecAsync(["ls", "-1", path]);
        if (result.ExitCode != 0)
            return [];

        return result.Stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();
    }

    private static string EscapeForShell(string value)
        => value.Replace("'", "'\\''");
}

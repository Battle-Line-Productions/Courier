using Microsoft.Extensions.Logging;

namespace Courier.Features.Engine;

public sealed class JobWorkspace : IAsyncDisposable
{
    private readonly ILogger<JobWorkspace> _logger;

    public JobWorkspace(ILogger<JobWorkspace> logger)
    {
        _logger = logger;
    }

    public string? Path { get; private set; }
    public bool IsInitialized => Path is not null;

    public string Initialize(Guid executionId, string baseDirectory)
    {
        if (IsInitialized)
            throw new InvalidOperationException("Workspace is already initialized.");

        var dir = System.IO.Path.Combine(baseDirectory, "courier-workspaces", executionId.ToString());
        Directory.CreateDirectory(dir);
        Path = dir;
        _logger.LogDebug("Workspace initialized at {WorkspacePath}", dir);
        return dir;
    }

    public string EnsureInitialized(Guid executionId, string baseDirectory, string? existingPath)
    {
        if (IsInitialized)
            return Path!;

        if (existingPath is not null && Directory.Exists(existingPath))
        {
            Path = existingPath;
            _logger.LogDebug("Workspace reattached at {WorkspacePath}", existingPath);
            return existingPath;
        }

        return Initialize(executionId, baseDirectory);
    }

    public string GetStepDirectory(int stepOrder)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Workspace has not been initialized.");

        var dir = System.IO.Path.Combine(Path!, $"step-{stepOrder}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public async ValueTask DisposeAsync()
    {
        if (Path is null || !Directory.Exists(Path))
            return;

        try
        {
            await Task.Run(() => Directory.Delete(Path, recursive: true));
            _logger.LogDebug("Workspace cleaned up at {WorkspacePath}", Path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up workspace at {WorkspacePath}", Path);
        }
    }
}

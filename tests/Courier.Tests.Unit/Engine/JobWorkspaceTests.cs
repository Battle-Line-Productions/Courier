using Courier.Features.Engine;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class JobWorkspaceTests : IDisposable
{
    private readonly JobWorkspace _workspace = new(NullLogger<JobWorkspace>.Instance);
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), $"workspace-test-{Guid.NewGuid()}");

    public void Dispose()
    {
        var workspacesDir = Path.Combine(_baseDir, "courier-workspaces");
        if (Directory.Exists(workspacesDir))
            Directory.Delete(workspacesDir, recursive: true);
        if (Directory.Exists(_baseDir))
            Directory.Delete(_baseDir, recursive: true);
    }

    [Fact]
    public void Initialize_CreatesDirectoryAtExpectedPath()
    {
        var executionId = Guid.NewGuid();

        var result = _workspace.Initialize(executionId, _baseDir);

        result.ShouldBe(Path.Combine(_baseDir, "courier-workspaces", executionId.ToString()));
        Directory.Exists(result).ShouldBeTrue();
        _workspace.IsInitialized.ShouldBeTrue();
        _workspace.Path.ShouldBe(result);
    }

    [Fact]
    public void Initialize_CalledTwice_Throws()
    {
        _workspace.Initialize(Guid.NewGuid(), _baseDir);

        Should.Throw<InvalidOperationException>(() => _workspace.Initialize(Guid.NewGuid(), _baseDir));
    }

    [Fact]
    public void EnsureInitialized_WithExistingPath_ReusesIt()
    {
        var existingPath = Path.Combine(_baseDir, "courier-workspaces", "existing");
        Directory.CreateDirectory(existingPath);

        var result = _workspace.EnsureInitialized(Guid.NewGuid(), _baseDir, existingPath);

        result.ShouldBe(existingPath);
        _workspace.Path.ShouldBe(existingPath);
    }

    [Fact]
    public void EnsureInitialized_WithMissingPath_CreatesNew()
    {
        var executionId = Guid.NewGuid();
        var missingPath = Path.Combine(_baseDir, "courier-workspaces", "does-not-exist");

        var result = _workspace.EnsureInitialized(executionId, _baseDir, missingPath);

        result.ShouldBe(Path.Combine(_baseDir, "courier-workspaces", executionId.ToString()));
        Directory.Exists(result).ShouldBeTrue();
    }

    [Fact]
    public void EnsureInitialized_WithNullPath_CreatesNew()
    {
        var executionId = Guid.NewGuid();

        var result = _workspace.EnsureInitialized(executionId, _baseDir, existingPath: null);

        result.ShouldBe(Path.Combine(_baseDir, "courier-workspaces", executionId.ToString()));
        Directory.Exists(result).ShouldBeTrue();
    }

    [Fact]
    public void GetStepDirectory_CreatesSubdirectory()
    {
        _workspace.Initialize(Guid.NewGuid(), _baseDir);

        var stepDir = _workspace.GetStepDirectory(3);

        stepDir.ShouldEndWith($"step-3");
        Directory.Exists(stepDir).ShouldBeTrue();
    }

    [Fact]
    public void GetStepDirectory_BeforeInit_Throws()
    {
        Should.Throw<InvalidOperationException>(() => _workspace.GetStepDirectory(1));
    }

    [Fact]
    public async Task DisposeAsync_DeletesDirectoryRecursively()
    {
        var path = _workspace.Initialize(Guid.NewGuid(), _baseDir);
        File.WriteAllText(Path.Combine(path, "test.txt"), "data");

        await _workspace.DisposeAsync();

        Directory.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public async Task DisposeAsync_WhenDirectoryMissing_DoesNotThrow()
    {
        _workspace.Initialize(Guid.NewGuid(), _baseDir);
        Directory.Delete(_workspace.Path!, recursive: true);

        await Should.NotThrowAsync(() => _workspace.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        _workspace.Initialize(Guid.NewGuid(), _baseDir);

        await _workspace.DisposeAsync();
        await Should.NotThrowAsync(() => _workspace.DisposeAsync().AsTask());
    }
}

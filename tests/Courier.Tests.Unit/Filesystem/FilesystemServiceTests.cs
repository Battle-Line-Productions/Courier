using Courier.Domain.Common;
using Courier.Features.Filesystem;
using Shouldly;

namespace Courier.Tests.Unit.Filesystem;

public class FilesystemServiceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly FilesystemService _service;

    public FilesystemServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"courier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
        _service = new FilesystemService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, true);
    }

    [Fact]
    public async Task BrowseAsync_NullPath_ReturnsFilesystemRoots()
    {
        var result = await _service.BrowseAsync(null);

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.CurrentPath.ShouldBeEmpty();
        result.Data.ParentPath.ShouldBeNull();
        result.Data.Entries.ShouldNotBeEmpty();
        result.Data.Entries.ShouldAllBe(e => e.Type == "directory");
    }

    [Fact]
    public async Task BrowseAsync_EmptyPath_ReturnsFilesystemRoots()
    {
        var result = await _service.BrowseAsync("");

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.CurrentPath.ShouldBeEmpty();
        result.Data.Entries.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task BrowseAsync_ValidDirectory_ReturnsDirectoriesFirstThenFiles()
    {
        // Arrange — create dirs and files in test root
        Directory.CreateDirectory(Path.Combine(_testRoot, "beta-dir"));
        Directory.CreateDirectory(Path.Combine(_testRoot, "alpha-dir"));
        File.WriteAllText(Path.Combine(_testRoot, "zebra.txt"), "z");
        File.WriteAllText(Path.Combine(_testRoot, "apple.txt"), "a");

        // Act
        var result = await _service.BrowseAsync(_testRoot);

        // Assert
        result.Success.ShouldBeTrue();
        var entries = result.Data!.Entries;
        entries.Count.ShouldBe(4);

        // Directories first, sorted
        entries[0].Name.ShouldBe("alpha-dir");
        entries[0].Type.ShouldBe("directory");
        entries[1].Name.ShouldBe("beta-dir");
        entries[1].Type.ShouldBe("directory");

        // Files second, sorted
        entries[2].Name.ShouldBe("apple.txt");
        entries[2].Type.ShouldBe("file");
        entries[3].Name.ShouldBe("zebra.txt");
        entries[3].Type.ShouldBe("file");
    }

    [Fact]
    public async Task BrowseAsync_ValidDirectory_ReturnsCorrectPathInfo()
    {
        var result = await _service.BrowseAsync(_testRoot);

        result.Success.ShouldBeTrue();
        result.Data!.CurrentPath.ShouldBe(new DirectoryInfo(_testRoot).FullName);
        result.Data.ParentPath.ShouldNotBeNull();
    }

    [Fact]
    public async Task BrowseAsync_ValidDirectory_ReturnsFileSizeAndLastModified()
    {
        File.WriteAllText(Path.Combine(_testRoot, "data.txt"), "hello world");

        var result = await _service.BrowseAsync(_testRoot);

        result.Success.ShouldBeTrue();
        var fileEntry = result.Data!.Entries.ShouldHaveSingleItem();
        fileEntry.Type.ShouldBe("file");
        fileEntry.Size.ShouldNotBeNull();
        fileEntry.Size!.Value.ShouldBeGreaterThan(0);
        fileEntry.LastModified.ShouldNotBeNull();
    }

    [Fact]
    public async Task BrowseAsync_EmptyDirectory_ReturnsEmptyEntries()
    {
        var result = await _service.BrowseAsync(_testRoot);

        result.Success.ShouldBeTrue();
        result.Data!.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task BrowseAsync_NonExistentDirectory_ReturnsDirectoryNotFoundError()
    {
        var fakePath = Path.Combine(_testRoot, "does-not-exist");

        var result = await _service.BrowseAsync(fakePath);

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(ErrorCodes.DirectoryNotFound);
    }
}

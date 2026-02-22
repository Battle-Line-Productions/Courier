using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Filesystem;
using Shouldly;

namespace Courier.Tests.Integration.Filesystem;

public class FilesystemApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public FilesystemApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Browse_NoPath_ReturnsRoots()
    {
        var response = await _client.GetAsync("/api/v1/filesystem/browse");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BrowseResult>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.ShouldNotBeNull();
        body.Data!.CurrentPath.ShouldBeEmpty();
        body.Data.ParentPath.ShouldBeNull();
        body.Data.Entries.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Browse_TempDirectory_ReturnsEntries()
    {
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        var response = await _client.GetAsync(
            $"/api/v1/filesystem/browse?path={Uri.EscapeDataString(tempDir)}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BrowseResult>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.ShouldNotBeNull();
        body.Data!.CurrentPath.ShouldNotBeNullOrEmpty();
        body.Data.ParentPath.ShouldNotBeNull();
    }

    [Fact]
    public async Task Browse_NonExistentPath_ReturnsNotFound()
    {
        var fakePath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid():N}");

        var response = await _client.GetAsync(
            $"/api/v1/filesystem/browse?path={Uri.EscapeDataString(fakePath)}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BrowseResult>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeFalse();
        body.Error.ShouldNotBeNull();
        body.Error!.Code.ShouldBe(ErrorCodes.DirectoryNotFound);
    }

    [Fact]
    public async Task Browse_DirectoryWithContents_ReturnsSortedDirectoriesFirst()
    {
        // Arrange — create temp structure
        var testDir = Path.Combine(Path.GetTempPath(), $"courier-inttest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        try
        {
            Directory.CreateDirectory(Path.Combine(testDir, "zulu-dir"));
            Directory.CreateDirectory(Path.Combine(testDir, "alpha-dir"));
            File.WriteAllText(Path.Combine(testDir, "beta.txt"), "b");
            File.WriteAllText(Path.Combine(testDir, "alpha.txt"), "a");

            // Act
            var response = await _client.GetAsync(
                $"/api/v1/filesystem/browse?path={Uri.EscapeDataString(testDir)}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<ApiResponse<BrowseResult>>();
            var entries = body!.Data!.Entries;

            // Assert — directories first (sorted), then files (sorted)
            entries.Count.ShouldBe(4);
            entries[0].Name.ShouldBe("alpha-dir");
            entries[0].Type.ShouldBe("directory");
            entries[1].Name.ShouldBe("zulu-dir");
            entries[1].Type.ShouldBe("directory");
            entries[2].Name.ShouldBe("alpha.txt");
            entries[2].Type.ShouldBe("file");
            entries[3].Name.ShouldBe("beta.txt");
            entries[3].Type.ShouldBe("file");
        }
        finally
        {
            Directory.Delete(testDir, true);
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Jobs;
using Shouldly;

namespace Courier.Tests.Integration.Jobs;

public class JobDependencyApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public JobDependencyApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueName(string prefix = "Job") =>
        $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    private async Task<Guid> CreateJobAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/jobs",
            new CreateJobRequest { Name = name });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
        return body!.Data!.Id;
    }

    [Fact]
    public async Task AddDependency_ValidPair_Returns201()
    {
        // Arrange
        var upstreamId = await CreateJobAsync(UniqueName("Up"));
        var downstreamId = await CreateJobAsync(UniqueName("Down"));

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/jobs/{downstreamId}/dependencies",
            new AddJobDependencyRequest { UpstreamJobId = upstreamId });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ListDependencies_Returns200()
    {
        // Arrange
        var jobId = await CreateJobAsync(UniqueName("List"));

        // Act
        var response = await _client.GetAsync($"/api/v1/jobs/{jobId}/dependencies");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddDependency_SelfDependency_Returns400()
    {
        // Arrange
        var jobId = await CreateJobAsync(UniqueName("Self"));

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/jobs/{jobId}/dependencies",
            new AddJobDependencyRequest { UpstreamJobId = jobId });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveDependency_Existing_Returns200()
    {
        // Arrange
        var upstreamId = await CreateJobAsync(UniqueName("Up"));
        var downstreamId = await CreateJobAsync(UniqueName("Down"));

        var addResponse = await _client.PostAsJsonAsync(
            $"/api/v1/jobs/{downstreamId}/dependencies",
            new AddJobDependencyRequest { UpstreamJobId = upstreamId });
        var added = await addResponse.Content.ReadFromJsonAsync<ApiResponse<JobDependencyDto>>();

        // Act
        var response = await _client.DeleteAsync(
            $"/api/v1/jobs/{downstreamId}/dependencies/{added!.Data!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemoveDependency_NonExistent_Returns404()
    {
        // Arrange
        var jobId = await CreateJobAsync(UniqueName("NoSuchDep"));

        // Act
        var response = await _client.DeleteAsync(
            $"/api/v1/jobs/{jobId}/dependencies/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

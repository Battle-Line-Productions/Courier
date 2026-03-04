using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Chains;
using Courier.Features.Jobs;
using Shouldly;

namespace Courier.Tests.Integration.Chains;

public class ChainsApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public ChainsApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueName(string prefix = "Chain") =>
        $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    private async Task<Guid> CreateJobAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/jobs",
            new CreateJobRequest { Name = name });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
        return body!.Data!.Id;
    }

    #region CRUD

    [Fact]
    public async Task CreateChain_ValidRequest_Returns201()
    {
        // Arrange
        var request = new CreateChainRequest
        {
            Name = UniqueName("Create"),
            Description = "Integration test chain"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/chains", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobChainDto>>();
        body.ShouldNotBeNull();
        body!.Success.ShouldBeTrue();
        body.Data.ShouldNotBeNull();
        body.Data!.Name.ShouldBe(request.Name);
        body.Data.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateChain_EmptyName_Returns400()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/chains",
            new CreateChainRequest { Name = "" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetChain_Existing_Returns200()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/chains",
            new CreateChainRequest { Name = UniqueName() });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<JobChainDto>>();

        // Act
        var response = await _client.GetAsync($"/api/v1/chains/{created!.Data!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetChain_NonExistent_Returns404()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/chains/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListChains_Returns200()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/chains");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteChain_Existing_Returns200()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/chains",
            new CreateChainRequest { Name = UniqueName("Delete") });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<JobChainDto>>();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/chains/{created!.Data!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion

    #region Members

    [Fact]
    public async Task ReplaceMembers_ValidMembers_Returns200()
    {
        // Arrange
        var chainResponse = await _client.PostAsJsonAsync("/api/v1/chains",
            new CreateChainRequest { Name = UniqueName("Members") });
        var chain = await chainResponse.Content.ReadFromJsonAsync<ApiResponse<JobChainDto>>();

        var jobId = await CreateJobAsync(UniqueName("Job"));

        var membersRequest = new ReplaceChainMembersRequest
        {
            Members = [new ChainMemberInput { JobId = jobId, ExecutionOrder = 1 }]
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/chains/{chain!.Data!.Id}/members", membersRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion

    #region Execution

    [Fact]
    public async Task TriggerChain_ValidChain_Returns202()
    {
        // Arrange
        var chainResponse = await _client.PostAsJsonAsync("/api/v1/chains",
            new CreateChainRequest { Name = UniqueName("Trigger") });
        var chain = await chainResponse.Content.ReadFromJsonAsync<ApiResponse<JobChainDto>>();

        var jobId = await CreateJobAsync(UniqueName("Job"));

        await _client.PutAsJsonAsync($"/api/v1/chains/{chain!.Data!.Id}/members",
            new ReplaceChainMembersRequest
            {
                Members = [new ChainMemberInput { JobId = jobId, ExecutionOrder = 1 }]
            });

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/chains/{chain.Data.Id}/execute",
            new TriggerChainRequest { TriggeredBy = "test" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task ListExecutions_Returns200()
    {
        // Arrange
        var chainResponse = await _client.PostAsJsonAsync("/api/v1/chains",
            new CreateChainRequest { Name = UniqueName("Execs") });
        var chain = await chainResponse.Content.ReadFromJsonAsync<ApiResponse<JobChainDto>>();

        // Act
        var response = await _client.GetAsync($"/api/v1/chains/{chain!.Data!.Id}/executions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion
}

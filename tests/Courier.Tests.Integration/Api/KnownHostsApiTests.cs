using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Connections;
using Shouldly;

namespace Courier.Tests.Integration.Api;

public class KnownHostsApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public KnownHostsApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateConnectionAsync(string? name = null)
    {
        var connName = name ?? $"kh-test-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/v1/connections", new
        {
            name = connName,
            protocol = "sftp",
            host = "sftp.example.com",
            authMethod = "password",
            username = "testuser",
            password = "s3cret!",
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ConnectionDto>>();
        return body!.Data!.Id;
    }

    private async Task<Guid> CreateKnownHostAsync(Guid connectionId, string? fingerprint = null)
    {
        var fp = fingerprint ?? $"SHA256:{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync($"/api/v1/connections/{connectionId}/known-hosts", new
        {
            keyType = "ssh-ed25519",
            fingerprint = fp,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<KnownHostDto>>();
        return body!.Data!.Id;
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var connectionId = await CreateConnectionAsync();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/connections/{connectionId}/known-hosts", new
        {
            keyType = "ssh-ed25519",
            fingerprint = $"SHA256:{Guid.NewGuid():N}",
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<KnownHostDto>>();
        body.ShouldNotBeNull();
        body!.Success.ShouldBeTrue();
        body.Data.ShouldNotBeNull();
        body.Data!.KeyType.ShouldBe("ssh-ed25519");
        body.Data.ConnectionId.ShouldBe(connectionId);
        body.Data.IsApproved.ShouldBeFalse();
    }

    [Fact]
    public async Task ListByConnection_AfterCreate_ReturnsList()
    {
        // Arrange
        var connectionId = await CreateConnectionAsync();
        await CreateKnownHostAsync(connectionId);
        await CreateKnownHostAsync(connectionId);

        // Act
        var response = await _client.GetAsync($"/api/v1/connections/{connectionId}/known-hosts");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<KnownHostDto>>>();
        body.ShouldNotBeNull();
        body!.Success.ShouldBeTrue();
        body.Data.ShouldNotBeNull();
        body.Data!.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsSingle()
    {
        // Arrange
        var connectionId = await CreateConnectionAsync();
        var fingerprint = $"SHA256:getbyid-{Guid.NewGuid():N}";
        var knownHostId = await CreateKnownHostAsync(connectionId, fingerprint);

        // Act
        var response = await _client.GetAsync($"/api/v1/known-hosts/{knownHostId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<KnownHostDto>>();
        body.ShouldNotBeNull();
        body!.Data.ShouldNotBeNull();
        body.Data!.Id.ShouldBe(knownHostId);
        body.Data.Fingerprint.ShouldBe(fingerprint);
    }

    [Fact]
    public async Task Delete_ExistingHost_RemovesSuccessfully()
    {
        // Arrange
        var connectionId = await CreateConnectionAsync();
        var knownHostId = await CreateKnownHostAsync(connectionId);

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/v1/known-hosts/{knownHostId}");

        // Assert
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify it's gone
        var getResponse = await _client.GetAsync($"/api/v1/known-hosts/{knownHostId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Approve_ExistingHost_UpdatesStatus()
    {
        // Arrange
        var connectionId = await CreateConnectionAsync();
        var knownHostId = await CreateKnownHostAsync(connectionId);

        // Act
        var approveResponse = await _client.PostAsync($"/api/v1/known-hosts/{knownHostId}/approve", null);

        // Assert
        approveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await approveResponse.Content.ReadFromJsonAsync<ApiResponse<KnownHostDto>>();
        body.ShouldNotBeNull();
        body!.Data.ShouldNotBeNull();
        body.Data!.IsApproved.ShouldBeTrue();
        body.Data.ApprovedBy.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Create_NonExistentConnection_ReturnsNotFound()
    {
        // Arrange
        var fakeConnectionId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/connections/{fakeConnectionId}/known-hosts", new
        {
            keyType = "ssh-ed25519",
            fingerprint = $"SHA256:{Guid.NewGuid():N}",
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistentHost_ReturnsNotFound()
    {
        // Arrange
        var fakeId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/known-hosts/{fakeId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_DuplicateFingerprint_ReturnsConflict()
    {
        // Arrange
        var connectionId = await CreateConnectionAsync();
        var fingerprint = $"SHA256:dup-{Guid.NewGuid():N}";
        await CreateKnownHostAsync(connectionId, fingerprint);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/connections/{connectionId}/known-hosts", new
        {
            keyType = "ssh-ed25519",
            fingerprint,
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Approve_AlreadyApproved_ReturnsConflict()
    {
        // Arrange
        var connectionId = await CreateConnectionAsync();
        var knownHostId = await CreateKnownHostAsync(connectionId);
        await _client.PostAsync($"/api/v1/known-hosts/{knownHostId}/approve", null);

        // Act — approve again
        var response = await _client.PostAsync($"/api/v1/known-hosts/{knownHostId}/approve", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ListByConnection_NonExistentConnection_ReturnsNotFound()
    {
        // Arrange
        var fakeConnectionId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/connections/{fakeConnectionId}/known-hosts");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

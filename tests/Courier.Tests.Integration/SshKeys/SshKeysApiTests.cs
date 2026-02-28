using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.SshKeys;
using Shouldly;

namespace Courier.Tests.Integration.SshKeys;

public class SshKeysApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public SshKeysApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static object MakeGenerateRequest(string name = "Test SSH Key", string keyType = "ed25519") => new
    {
        name,
        keyType,
    };

    [Fact]
    public async Task Generate_ValidRequest_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/ssh-keys/generate", MakeGenerateRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SshKeyDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.ShouldNotBeNull();
        body.Data!.Name.ShouldBe("Test SSH Key");
        body.Data.KeyType.ShouldBe("ed25519");
        body.Data.Status.ShouldBe("active");
        body.Data.Fingerprint.ShouldStartWith("SHA256:");
    }

    [Fact]
    public async Task Generate_EmptyName_ReturnsBadRequest()
    {
        var request = new { name = "", keyType = "ed25519" };

        var response = await _client.PostAsJsonAsync("/api/v1/ssh-keys/generate", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Generate_InvalidKeyType_ReturnsBadRequest()
    {
        var request = new { name = "Bad Type", keyType = "dsa_1024" };

        var response = await _client.PostAsJsonAsync("/api/v1/ssh-keys/generate", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_AfterGenerate_ReturnsKeys()
    {
        var name = $"List Test {Guid.NewGuid().ToString("N")[..8]}";
        await _client.PostAsJsonAsync("/api/v1/ssh-keys/generate", MakeGenerateRequest(name));

        var response = await _client.GetAsync("/api/v1/ssh-keys");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<SshKeyDto>>();
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetById_ExistingKey_ReturnsKey()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/ssh-keys/generate", MakeGenerateRequest("Get By Id Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<SshKeyDto>>();

        var response = await _client.GetAsync($"/api/v1/ssh-keys/{created!.Data!.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SshKeyDto>>();
        body!.Data!.Name.ShouldBe("Get By Id Test");
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/ssh-keys/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ValidRequest_ReturnsUpdated()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/ssh-keys/generate", MakeGenerateRequest("Original"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<SshKeyDto>>();

        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/ssh-keys/{created!.Data!.Id}", new
        {
            name = "Updated",
            notes = "Some notes",
        });

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<SshKeyDto>>();
        updated!.Data!.Name.ShouldBe("Updated");
    }

    [Fact]
    public async Task Delete_ExistingKey_ReturnsOk()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/ssh-keys/generate", MakeGenerateRequest("To Delete"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<SshKeyDto>>();

        var deleteResponse = await _client.DeleteAsync($"/api/v1/ssh-keys/{created!.Data!.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/v1/ssh-keys/{created.Data.Id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExportPublicKey_ExistingKey_ReturnsFile()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/ssh-keys/generate", MakeGenerateRequest("Export Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<SshKeyDto>>();

        var response = await _client.GetAsync($"/api/v1/ssh-keys/{created!.Data!.Id}/export/public");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldStartWith("ssh-ed25519");
    }

    [Fact]
    public async Task Retire_ActiveKey_ReturnsOk()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/ssh-keys/generate", MakeGenerateRequest("Retire Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<SshKeyDto>>();

        var response = await _client.PostAsync($"/api/v1/ssh-keys/{created!.Data!.Id}/retire", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SshKeyDto>>();
        body!.Data!.Status.ShouldBe("retired");
    }

    [Fact]
    public async Task Activate_RetiredKey_ReturnsOk()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/ssh-keys/generate", MakeGenerateRequest("Activate Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<SshKeyDto>>();

        await _client.PostAsync($"/api/v1/ssh-keys/{created!.Data!.Id}/retire", null);

        var response = await _client.PostAsync($"/api/v1/ssh-keys/{created.Data.Id}/activate", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SshKeyDto>>();
        body!.Data!.Status.ShouldBe("active");
    }
}

using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.PgpKeys;
using Shouldly;

namespace Courier.Tests.Integration.PgpKeys;

public class PgpKeysApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public PgpKeysApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static object MakeGenerateRequest(string name = "Test PGP Key") => new
    {
        name,
        algorithm = "rsa_2048",
        realName = "Test User",
        email = "test@example.com",
    };

    [Fact]
    public async Task Generate_ValidRequest_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/pgp-keys/generate", MakeGenerateRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.ShouldNotBeNull();
        body.Data!.Name.ShouldBe("Test PGP Key");
        body.Data.Algorithm.ShouldBe("rsa_2048");
        body.Data.KeyType.ShouldBe("key_pair");
        body.Data.Status.ShouldBe("active");
        body.Data.Fingerprint.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Generate_EmptyName_ReturnsBadRequest()
    {
        var request = new { name = "", algorithm = "rsa_2048" };

        var response = await _client.PostAsJsonAsync("/api/v1/pgp-keys/generate", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Generate_InvalidAlgorithm_ReturnsBadRequest()
    {
        var request = new { name = "Bad Algo", algorithm = "dsa_1024" };

        var response = await _client.PostAsJsonAsync("/api/v1/pgp-keys/generate", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_AfterGenerate_ReturnsKeys()
    {
        var name = $"List Test {Guid.NewGuid().ToString("N")[..8]}";
        await _client.PostAsJsonAsync("/api/v1/pgp-keys/generate", MakeGenerateRequest(name));

        var response = await _client.GetAsync("/api/v1/pgp-keys");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<PgpKeyDto>>();
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetById_ExistingKey_ReturnsKey()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/pgp-keys/generate", MakeGenerateRequest("Get By Id Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();

        var response = await _client.GetAsync($"/api/v1/pgp-keys/{created!.Data!.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();
        body!.Data!.Name.ShouldBe("Get By Id Test");
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/pgp-keys/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_PrivateKeyNeverExposed()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/pgp-keys/generate", MakeGenerateRequest("Privacy Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();

        var rawJson = await (await _client.GetAsync($"/api/v1/pgp-keys/{created!.Data!.Id}")).Content.ReadAsStringAsync();
        rawJson.ShouldNotContain("privateKeyData");
        rawJson.ShouldNotContain("private_key_data");
        rawJson.ShouldNotContain("publicKeyData");
        rawJson.ShouldNotContain("public_key_data");
    }

    [Fact]
    public async Task Update_ValidRequest_ReturnsUpdated()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/pgp-keys/generate", MakeGenerateRequest("Original"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();

        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/pgp-keys/{created!.Data!.Id}", new
        {
            name = "Updated",
            purpose = "New purpose",
        });

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();
        updated!.Data!.Name.ShouldBe("Updated");
    }

    [Fact]
    public async Task Delete_ExistingKey_ReturnsOk()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/pgp-keys/generate", MakeGenerateRequest("To Delete"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();

        var deleteResponse = await _client.DeleteAsync($"/api/v1/pgp-keys/{created!.Data!.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/v1/pgp-keys/{created.Data.Id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExportPublicKey_ExistingKey_ReturnsFile()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/pgp-keys/generate", MakeGenerateRequest("Export Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();

        var response = await _client.GetAsync($"/api/v1/pgp-keys/{created!.Data!.Id}/export/public");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("BEGIN PGP PUBLIC KEY BLOCK");
    }

    [Fact]
    public async Task Retire_ActiveKey_ReturnsOkWithRetiredStatus()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/pgp-keys/generate", MakeGenerateRequest("Retire Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();

        var response = await _client.PostAsync($"/api/v1/pgp-keys/{created!.Data!.Id}/retire", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();
        body!.Data!.Status.ShouldBe("retired");
    }

    [Fact]
    public async Task Revoke_ActiveKey_ReturnsOkWithRevokedStatus()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/pgp-keys/generate", MakeGenerateRequest("Revoke Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();

        var response = await _client.PostAsync($"/api/v1/pgp-keys/{created!.Data!.Id}/revoke", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();
        body!.Data!.Status.ShouldBe("revoked");
        body.Data.HasPrivateKey.ShouldBeFalse();
    }

    [Fact]
    public async Task Activate_RetiredKey_ReturnsOkWithActiveStatus()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/pgp-keys/generate", MakeGenerateRequest("Activate Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();

        await _client.PostAsync($"/api/v1/pgp-keys/{created!.Data!.Id}/retire", null);

        var response = await _client.PostAsync($"/api/v1/pgp-keys/{created.Data.Id}/activate", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PgpKeyDto>>();
        body!.Data!.Status.ShouldBe("active");
    }
}

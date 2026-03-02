using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Connections;
using Shouldly;

namespace Courier.Tests.Integration.Connections;

public class ConnectionsApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public ConnectionsApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static object MakeSftpRequest(string name = "Test SFTP Connection") => new
    {
        name,
        protocol = "sftp",
        host = "sftp.example.com",
        authMethod = "password",
        username = "testuser",
        password = "s3cret!",
    };

    [Fact]
    public async Task CreateConnection_ValidRequest_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/connections", MakeSftpRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ConnectionDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.ShouldNotBeNull();
        body.Data!.Name.ShouldBe("Test SFTP Connection");
        body.Data.Protocol.ShouldBe("sftp");
        body.Data.Port.ShouldBe(22);
        body.Data.Status.ShouldBe("active");
    }

    [Fact]
    public async Task CreateConnection_EmptyName_ReturnsBadRequest()
    {
        var request = new
        {
            name = "",
            protocol = "sftp",
            host = "sftp.example.com",
            authMethod = "password",
            username = "user",
            password = "pass",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/connections", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateConnection_InvalidProtocol_ReturnsBadRequest()
    {
        var request = new
        {
            name = "Bad Proto",
            protocol = "ssh",
            host = "example.com",
            authMethod = "password",
            username = "user",
            password = "pass",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/connections", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListConnections_AfterCreate_ReturnsConnection()
    {
        var name = $"List Test {Guid.NewGuid().ToString("N")[..8]}";
        await _client.PostAsJsonAsync("/api/v1/connections", MakeSftpRequest(name));

        var response = await _client.GetAsync("/api/v1/connections");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<ConnectionDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListConnections_FilterByProtocol_ReturnsFiltered()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        await _client.PostAsJsonAsync("/api/v1/connections", new
        {
            name = $"FTP-{unique}", protocol = "ftp", host = "ftp.example.com",
            authMethod = "password", username = "user", password = "pass",
        });

        var response = await _client.GetAsync($"/api/v1/connections?protocol=ftp&search={unique}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<ConnectionDto>>();
        body!.Data.ShouldAllBe(c => c.Protocol == "ftp");
    }

    [Fact]
    public async Task GetConnection_ExistingId_ReturnsConnection()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/connections", MakeSftpRequest("Get By Id Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ConnectionDto>>();

        var response = await _client.GetAsync($"/api/v1/connections/{created!.Data!.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ConnectionDto>>();
        body!.Data!.Name.ShouldBe("Get By Id Test");
    }

    [Fact]
    public async Task GetConnection_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/connections/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetConnection_PasswordNeverExposed()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/connections", MakeSftpRequest("Password Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ConnectionDto>>();

        var response = await _client.GetAsync($"/api/v1/connections/{created!.Data!.Id}");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ConnectionDto>>();

        body!.Data!.HasPassword.ShouldBeTrue();
        // Verify the response JSON doesn't contain a passwordEncrypted field
        var rawJson = await (await _client.GetAsync($"/api/v1/connections/{created.Data.Id}")).Content.ReadAsStringAsync();
        rawJson.ShouldNotContain("passwordEncrypted");
        rawJson.ShouldNotContain("password_encrypted");
    }

    [Fact]
    public async Task UpdateConnection_ValidRequest_ReturnsUpdated()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/connections", MakeSftpRequest("Original"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ConnectionDto>>();

        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/connections/{created!.Data!.Id}", new
        {
            name = "Updated",
            protocol = "sftp",
            host = "new-host.example.com",
            authMethod = "password",
            username = "newuser",
        });

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<ConnectionDto>>();
        updated!.Data!.Name.ShouldBe("Updated");
        updated.Data.Host.ShouldBe("new-host.example.com");
    }

    [Fact]
    public async Task DeleteConnection_ExistingConnection_SoftDeletes()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/connections", MakeSftpRequest("To Delete"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ConnectionDto>>();

        var deleteResponse = await _client.DeleteAsync($"/api/v1/connections/{created!.Data!.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify it's gone from list
        var getResponse = await _client.GetAsync($"/api/v1/connections/{created.Data.Id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestConnection_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.PostAsync($"/api/v1/connections/{Guid.NewGuid()}/test", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestConnection_SftpConnection_ReturnsTestResult()
    {
        // No real SFTP server → Connected=false, but response shape is correct
        var createResponse = await _client.PostAsJsonAsync("/api/v1/connections", MakeSftpRequest("Test Connection"));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ConnectionDto>>();

        var response = await _client.PostAsync($"/api/v1/connections/{created!.Data!.Id}/test", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ConnectionTestDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.ShouldNotBeNull();
        body.Data!.Connected.ShouldBeFalse(); // no real server
        body.Data.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateConnection_FtpDefaultPort21()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/connections", new
        {
            name = "FTP Port Test",
            protocol = "ftp",
            host = "ftp.example.com",
            authMethod = "password",
            username = "user",
            password = "pass",
        });

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ConnectionDto>>();
        body!.Data!.Port.ShouldBe(21);
    }

    [Fact]
    public async Task CreateConnection_FtpsDefaultPort990()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/connections", new
        {
            name = "FTPS Port Test",
            protocol = "ftps",
            host = "ftps.example.com",
            authMethod = "password",
            username = "user",
            password = "pass",
        });

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ConnectionDto>>();
        body!.Data!.Port.ShouldBe(990);
    }
}

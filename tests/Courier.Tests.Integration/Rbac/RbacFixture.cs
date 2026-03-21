using System.Net.Http.Json;
using System.Text.Json;

namespace Courier.Tests.Integration.Rbac;

public class RbacFixture : IAsyncLifetime
{
    private CourierApiFactory _factory = null!;

    public HttpClient AdminClient { get; private set; } = null!;
    public HttpClient OperatorClient { get; private set; } = null!;
    public HttpClient ViewerClient { get; private set; } = null!;
    public HttpClient AnonymousClient { get; private set; } = null!;

    // Seeded test data IDs — populated during InitializeAsync.
    // Individual test classes may seed additional entity types as needed.
    public Guid TestJobId { get; private set; }
    public Guid TestTagId { get; private set; }

    public async ValueTask InitializeAsync()
    {
        _factory = new CourierApiFactory();
        await _factory.InitializeAsync();

        AdminClient = CreateClientWithHandler(new RoleHeaderHandler("admin"));
        OperatorClient = CreateClientWithHandler(new RoleHeaderHandler("operator"));
        ViewerClient = CreateClientWithHandler(new RoleHeaderHandler("viewer"));
        AnonymousClient = CreateClientWithHandler(new AnonymousHeaderHandler());

        await SeedTestDataAsync();
    }

    private HttpClient CreateClientWithHandler(DelegatingHandler handler)
    {
        handler.InnerHandler = _factory.Server.CreateHandler();
        return new HttpClient(handler)
        {
            BaseAddress = _factory.Server.BaseAddress,
        };
    }

    private async Task SeedTestDataAsync()
    {
        // Create a test job
        var jobResponse = await AdminClient.PostAsJsonAsync("api/v1/jobs", new
        {
            name = "rbac-test-job",
            description = "Test job for RBAC tests",
        });
        if (jobResponse.IsSuccessStatusCode)
        {
            var jobResult = await jobResponse.Content.ReadFromJsonAsync<JsonElement>();
            if (jobResult.TryGetProperty("data", out var data) &&
                data.TryGetProperty("id", out var id))
            {
                TestJobId = Guid.Parse(id.GetString()!);
            }
        }

        // Create a test tag
        var tagResponse = await AdminClient.PostAsJsonAsync("api/v1/tags", new
        {
            name = "rbac-test-tag",
            color = "#FF0000",
        });
        if (tagResponse.IsSuccessStatusCode)
        {
            var tagResult = await tagResponse.Content.ReadFromJsonAsync<JsonElement>();
            if (tagResult.TryGetProperty("data", out var data) &&
                data.TryGetProperty("id", out var id))
            {
                TestTagId = Guid.Parse(id.GetString()!);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (TestJobId != Guid.Empty)
            await AdminClient.DeleteAsync($"api/v1/jobs/{TestJobId}");
        if (TestTagId != Guid.Empty)
            await AdminClient.DeleteAsync($"api/v1/tags/{TestTagId}");

        AdminClient?.Dispose();
        OperatorClient?.Dispose();
        ViewerClient?.Dispose();
        AnonymousClient?.Dispose();

        if (_factory is not null)
            await _factory.DisposeAsync();
    }
}

[CollectionDefinition("Rbac")]
public class RbacCollection : ICollectionFixture<RbacFixture> { }

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class TagsRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
{
    private static void AssertAuthorized(HttpResponseMessage response)
    {
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden,
            "Expected authorized access but got Forbidden");
    }

    // --- View Endpoints (all roles allowed) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ListTags_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/tags");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetTag_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync($"api/v1/tags/{Fixture.TestTagId}");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetTagEntities_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync($"api/v1/tags/{Fixture.TestTagId}/entities");
        AssertAuthorized(response);
    }

    // --- Create (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task CreateTag_AllowedRoles_NotForbidden(string role)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/tags", new
        {
            name = $"rbac-tag-{role}-{suffix}",
            color = "#00FF00",
        });

        AssertAuthorized(response);

        // Clean up
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (result.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
            {
                await Fixture.AdminClient.DeleteAsync($"api/v1/tags/{id.GetString()}");
            }
        }
    }

    [Fact]
    public async Task CreateTag_Viewer_Forbidden()
    {
        var response = await ClientForRole("viewer").PostAsJsonAsync("api/v1/tags", new
        {
            name = "rbac-tag-viewer-should-fail",
            color = "#00FF00",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Update (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task UpdateTag_AllowedRoles_NotForbidden(string role)
    {
        var response = await ClientForRole(role).PutAsJsonAsync($"api/v1/tags/{Fixture.TestTagId}", new
        {
            name = "rbac-test-tag",
            color = "#FF0000",
        });

        AssertAuthorized(response);
    }

    [Fact]
    public async Task UpdateTag_Viewer_Forbidden()
    {
        var response = await ClientForRole("viewer").PutAsJsonAsync($"api/v1/tags/{Fixture.TestTagId}", new
        {
            name = "rbac-test-tag",
            color = "#FF0000",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Delete (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task DeleteTag_AllowedRoles_NotForbidden(string role)
    {
        // Create a tag to delete
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await Fixture.AdminClient.PostAsJsonAsync("api/v1/tags", new
        {
            name = $"rbac-tag-delete-{role}-{suffix}",
            color = "#0000FF",
        });
        createResponse.IsSuccessStatusCode.ShouldBeTrue("Failed to create test tag for deletion");
        var result = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var tagId = result.GetProperty("data").GetProperty("id").GetString()!;

        var response = await ClientForRole(role).DeleteAsync($"api/v1/tags/{tagId}");
        AssertAuthorized(response);

        // Clean up if not deleted
        if (!response.IsSuccessStatusCode)
        {
            await Fixture.AdminClient.DeleteAsync($"api/v1/tags/{tagId}");
        }
    }

    [Fact]
    public async Task DeleteTag_Viewer_Forbidden()
    {
        var response = await ClientForRole("viewer").DeleteAsync($"api/v1/tags/{Fixture.TestTagId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Assign / Unassign (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task AssignTag_AllowedRoles_NotForbidden(string role)
    {
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/tags/assign", new
        {
            tagId = Fixture.TestTagId,
            entityId = Fixture.TestJobId,
            entityType = "job",
        });

        AssertAuthorized(response);
    }

    [Fact]
    public async Task AssignTag_Viewer_Forbidden()
    {
        var response = await ClientForRole("viewer").PostAsJsonAsync("api/v1/tags/assign", new
        {
            tagId = Fixture.TestTagId,
            entityId = Fixture.TestJobId,
            entityType = "job",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task UnassignTag_AllowedRoles_NotForbidden(string role)
    {
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/tags/unassign", new
        {
            tagId = Fixture.TestTagId,
            entityId = Fixture.TestJobId,
            entityType = "job",
        });

        AssertAuthorized(response);
    }

    [Fact]
    public async Task UnassignTag_Viewer_Forbidden()
    {
        var response = await ClientForRole("viewer").PostAsJsonAsync("api/v1/tags/unassign", new
        {
            tagId = Fixture.TestTagId,
            entityId = Fixture.TestJobId,
            entityType = "job",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Anonymous gets Unauthorized ---

    [Fact]
    public async Task ListTags_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/tags");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

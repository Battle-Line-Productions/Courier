using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class JobsRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
{
    private static void AssertAuthorized(HttpResponseMessage response)
    {
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden,
            $"Expected authorized access but got Forbidden");
    }

    // --- View Endpoints (all roles allowed) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ListJobs_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/jobs");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetJob_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync($"api/v1/jobs/{Fixture.TestJobId}");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetJobSteps_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync($"api/v1/jobs/{Fixture.TestJobId}/steps");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetJobExecutions_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync($"api/v1/jobs/{Fixture.TestJobId}/executions");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetJobSchedules_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync($"api/v1/jobs/{Fixture.TestJobId}/schedules");
        AssertAuthorized(response);
    }

    // --- Create (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task CreateJob_AllowedRoles_NotForbidden(string role)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/jobs", new
        {
            name = $"rbac-create-{role}-{suffix}",
            description = "RBAC test job",
        });

        AssertAuthorized(response);

        // Clean up if created successfully
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (result.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
            {
                await Fixture.AdminClient.DeleteAsync($"api/v1/jobs/{id.GetString()}");
            }
        }
    }

    [Fact]
    public async Task CreateJob_Viewer_Forbidden()
    {
        var response = await ClientForRole("viewer").PostAsJsonAsync("api/v1/jobs", new
        {
            name = "rbac-create-viewer-should-fail",
            description = "Should be forbidden",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Update (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task UpdateJob_AllowedRoles_NotForbidden(string role)
    {
        var response = await ClientForRole(role).PutAsJsonAsync($"api/v1/jobs/{Fixture.TestJobId}", new
        {
            name = "rbac-test-job",
            description = $"Updated by {role}",
        });

        AssertAuthorized(response);
    }

    [Fact]
    public async Task UpdateJob_Viewer_Forbidden()
    {
        var response = await ClientForRole("viewer").PutAsJsonAsync($"api/v1/jobs/{Fixture.TestJobId}", new
        {
            name = "rbac-test-job",
            description = "Should be forbidden",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Delete (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task DeleteJob_AllowedRoles_NotForbidden(string role)
    {
        // Create a job to delete
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await Fixture.AdminClient.PostAsJsonAsync("api/v1/jobs", new
        {
            name = $"rbac-delete-{role}-{suffix}",
            description = "Job to delete for RBAC test",
        });
        createResponse.IsSuccessStatusCode.ShouldBeTrue("Failed to create test job for deletion");
        var result = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = result.GetProperty("data").GetProperty("id").GetString()!;

        var response = await ClientForRole(role).DeleteAsync($"api/v1/jobs/{jobId}");
        AssertAuthorized(response);

        // Clean up if delete didn't succeed (e.g., soft delete already happened)
        if (!response.IsSuccessStatusCode)
        {
            await Fixture.AdminClient.DeleteAsync($"api/v1/jobs/{jobId}");
        }
    }

    [Fact]
    public async Task DeleteJob_Viewer_Forbidden()
    {
        var response = await ClientForRole("viewer").DeleteAsync($"api/v1/jobs/{Fixture.TestJobId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Trigger (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task TriggerJob_AllowedRoles_NotForbidden(string role)
    {
        // Trigger may fail with 400 (no steps) but should NOT be 403
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/jobs/{Fixture.TestJobId}/trigger", null);
        AssertAuthorized(response);
    }

    [Fact]
    public async Task TriggerJob_Viewer_Forbidden()
    {
        var response = await ClientForRole("viewer").PostAsync(
            $"api/v1/jobs/{Fixture.TestJobId}/trigger", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Anonymous gets Unauthorized ---

    [Fact]
    public async Task ListJobs_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/jobs");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

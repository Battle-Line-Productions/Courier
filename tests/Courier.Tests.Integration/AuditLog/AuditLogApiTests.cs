using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.AuditLog;
using Courier.Features.Jobs;
using Shouldly;

namespace Courier.Tests.Integration.AuditLog;

public class AuditLogApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public AuditLogApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListAuditLog_ReturnsPagedResponse()
    {
        var response = await _client.GetAsync("/api/v1/audit-log");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<AuditLogEntryDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Pagination.ShouldNotBeNull();
    }

    [Fact]
    public async Task ListAuditLog_FilterByEntityType_ReturnsFiltered()
    {
        // Create a job to generate audit entries
        await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Name = $"Audit Filter Test {Guid.NewGuid().ToString("N")[..8]}"
        });

        var response = await _client.GetAsync("/api/v1/audit-log?entityType=job");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<AuditLogEntryDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();

        if (body.Data.Count > 0)
            body.Data.ShouldAllBe(e => e.EntityType == "job");
    }

    [Fact]
    public async Task ListAuditLogByEntity_ReturnsEntityHistory()
    {
        // Create a job
        var createResponse = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Name = $"Entity History Test {Guid.NewGuid().ToString("N")[..8]}"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
        var jobId = created!.Data!.Id;

        // Get audit log for this entity
        var response = await _client.GetAsync($"/api/v1/audit-log/entity/job/{jobId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<AuditLogEntryDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.Count.ShouldBeGreaterThanOrEqualTo(1);
        body.Data.ShouldAllBe(e => e.EntityId == jobId);
    }

    [Fact]
    public async Task CrudOnJob_CreatesCorrespondingAuditEntries()
    {
        // Create a job
        var name = $"Audit E2E {Guid.NewGuid().ToString("N")[..8]}";
        var createResponse = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest { Name = name });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
        var jobId = created!.Data!.Id;

        // Update the job
        await _client.PutAsJsonAsync($"/api/v1/jobs/{jobId}", new { name = $"{name} Updated" });

        // Delete the job
        await _client.DeleteAsync($"/api/v1/jobs/{jobId}");

        // Check audit log for this entity
        var response = await _client.GetAsync($"/api/v1/audit-log/entity/job/{jobId}");
        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<AuditLogEntryDto>>();

        body.ShouldNotBeNull();
        body.Data.Count.ShouldBeGreaterThanOrEqualTo(3);

        var operations = body.Data.Select(e => e.Operation).ToList();
        operations.ShouldContain("Created");
        operations.ShouldContain("Updated");
        operations.ShouldContain("Deleted");
    }
}

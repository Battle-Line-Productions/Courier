using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Jobs;
using Shouldly;

namespace Courier.Tests.Integration.Jobs;

public class JobsApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public JobsApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateJob_ValidRequest_ReturnsCreated()
    {
        var request = new CreateJobRequest { Name = "Integration Test Job", Description = "Created by test" };

        var response = await _client.PostAsJsonAsync("/api/v1/jobs", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.ShouldNotBeNull();
        body.Data!.Name.ShouldBe("Integration Test Job");
    }

    [Fact]
    public async Task CreateJob_EmptyName_ReturnsBadRequest()
    {
        var request = new CreateJobRequest { Name = "" };

        var response = await _client.PostAsJsonAsync("/api/v1/jobs", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListJobs_AfterCreate_ReturnsJob()
    {
        var request = new CreateJobRequest { Name = $"List Test {Guid.NewGuid().ToString("N")[..8]}" };
        await _client.PostAsJsonAsync("/api/v1/jobs", request);

        var response = await _client.GetAsync("/api/v1/jobs");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<JobDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetJob_ExistingId_ReturnsJob()
    {
        var request = new CreateJobRequest { Name = "Get By Id Test" };
        var createResponse = await _client.PostAsJsonAsync("/api/v1/jobs", request);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();

        var response = await _client.GetAsync($"/api/v1/jobs/{created!.Data!.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
        body!.Data!.Name.ShouldBe("Get By Id Test");
    }

    [Fact]
    public async Task GetJob_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/jobs/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthCheckReady_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/ready");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddStep_ValidRequest_ReturnsCreated()
    {
        var jobRequest = new CreateJobRequest { Name = "Step Test Job" };
        var jobResponse = await _client.PostAsJsonAsync("/api/v1/jobs", jobRequest);
        var job = await jobResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();

        var stepRequest = new AddJobStepRequest
        {
            Name = "Copy File",
            TypeKey = "file.copy",
            StepOrder = 0,
            Configuration = """{"source_path": "/tmp/in.txt", "destination_path": "/tmp/out.txt"}""",
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{job!.Data!.Id}/steps", stepRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobStepDto>>();
        body.ShouldNotBeNull();
        body!.Data!.TypeKey.ShouldBe("file.copy");
    }

    [Fact]
    public async Task ListSteps_AfterAdd_ReturnsSteps()
    {
        var jobRequest = new CreateJobRequest { Name = "List Steps Test" };
        var jobResponse = await _client.PostAsJsonAsync("/api/v1/jobs", jobRequest);
        var job = await jobResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
        var jobId = job!.Data!.Id;

        await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/steps", new AddJobStepRequest
        {
            Name = "Step 1", TypeKey = "file.copy", StepOrder = 0,
        });

        var response = await _client.GetAsync($"/api/v1/jobs/{jobId}/steps");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<JobStepDto>>>();
        body.ShouldNotBeNull();
        body!.Data.ShouldNotBeNull();
        body.Data!.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task TriggerJob_WithSteps_ReturnsAccepted()
    {
        var jobRequest = new CreateJobRequest { Name = "Trigger Test Job" };
        var jobResponse = await _client.PostAsJsonAsync("/api/v1/jobs", jobRequest);
        var job = await jobResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
        var jobId = job!.Data!.Id;

        await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/steps", new AddJobStepRequest
        {
            Name = "Copy", TypeKey = "file.copy", StepOrder = 0,
            Configuration = """{"source_path": "/tmp/in.txt", "destination_path": "/tmp/out.txt"}""",
        });

        var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/trigger",
            new TriggerJobRequest { TriggeredBy = "test" });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobExecutionDto>>();
        body.ShouldNotBeNull();
        body!.Data!.State.ShouldBe("queued");
    }

    [Fact]
    public async Task TriggerJob_NoSteps_ReturnsBadRequest()
    {
        var jobRequest = new CreateJobRequest { Name = "No Steps Job" };
        var jobResponse = await _client.PostAsJsonAsync("/api/v1/jobs", jobRequest);
        var job = await jobResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();

        var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{job!.Data!.Id}/trigger",
            new TriggerJobRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}

using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Jobs;
using Shouldly;

namespace Courier.Tests.Integration.Jobs;

public class ExecutionControlApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public ExecutionControlApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(Guid jobId, Guid executionId)> CreateJobAndTriggerAsync()
    {
        // Create a job with a step so it can be triggered
        var jobRequest = new CreateJobRequest { Name = $"Control Test {Guid.NewGuid():N}" };
        var jobResponse = await _client.PostAsJsonAsync("/api/v1/jobs", jobRequest);
        var jobBody = await jobResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
        var jobId = jobBody!.Data!.Id;

        // Add a step
        var stepRequest = new AddJobStepRequest
        {
            Name = "Test Step",
            TypeKey = "file.copy",
            Configuration = """{"source": "/tmp/a", "destination": "/tmp/b"}"""
        };
        await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/steps", stepRequest);

        // Trigger execution
        var triggerResponse = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/trigger", new TriggerJobRequest());
        var triggerBody = await triggerResponse.Content.ReadFromJsonAsync<ApiResponse<JobExecutionDto>>();
        var executionId = triggerBody!.Data!.Id;

        return (jobId, executionId);
    }

    [Fact]
    public async Task PauseExecution_NotRunning_Returns409()
    {
        // Arrange — newly triggered execution is in Queued state (not Running)
        var (_, executionId) = await CreateJobAndTriggerAsync();

        // Act
        var response = await _client.PostAsync($"/api/v1/jobs/executions/{executionId}/pause", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ResumeExecution_NotPaused_Returns409()
    {
        // Arrange
        var (_, executionId) = await CreateJobAndTriggerAsync();

        // Act
        var response = await _client.PostAsync($"/api/v1/jobs/executions/{executionId}/resume", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CancelExecution_QueuedExecution_Returns200()
    {
        // Arrange
        var (_, executionId) = await CreateJobAndTriggerAsync();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/jobs/executions/{executionId}/cancel",
            new CancelExecutionRequest { Reason = "Testing cancellation" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobExecutionDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data!.State.ShouldBe("cancelled");
        body.Data.CancelledBy.ShouldBe("system");
        body.Data.CancelReason.ShouldBe("Testing cancellation");
    }

    [Fact]
    public async Task CancelExecution_AlreadyCancelled_Returns409()
    {
        // Arrange
        var (_, executionId) = await CreateJobAndTriggerAsync();
        await _client.PostAsJsonAsync($"/api/v1/jobs/executions/{executionId}/cancel", new CancelExecutionRequest());

        // Act — cancel again
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/jobs/executions/{executionId}/cancel",
            new CancelExecutionRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PauseExecution_NonExistent_Returns404()
    {
        var response = await _client.PostAsync($"/api/v1/jobs/executions/{Guid.NewGuid()}/pause", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResumeExecution_NonExistent_Returns404()
    {
        var response = await _client.PostAsync($"/api/v1/jobs/executions/{Guid.NewGuid()}/resume", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelExecution_NonExistent_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/jobs/executions/{Guid.NewGuid()}/cancel",
            new CancelExecutionRequest());
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

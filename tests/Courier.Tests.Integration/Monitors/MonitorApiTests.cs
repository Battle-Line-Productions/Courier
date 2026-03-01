using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Jobs;
using Courier.Features.Monitors;
using Shouldly;

namespace Courier.Tests.Integration.Monitors;

public class MonitorApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public MonitorApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateTestJobAsync()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var response = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Name = $"Monitor Test Job {unique}",
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
        return body!.Data!.Id;
    }

    private async Task<MonitorDto> CreateTestMonitorAsync(Guid? jobId = null)
    {
        var jid = jobId ?? await CreateTestJobAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var response = await _client.PostAsJsonAsync("/api/v1/monitors", new
        {
            name = $"Test Monitor {unique}",
            watchTarget = """{"type":"local","path":"/data/incoming"}""",
            triggerEvents = 1,
            pollingIntervalSec = 60,
            maxConsecutiveFailures = 5,
            jobIds = new[] { jid },
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MonitorDto>>();
        return body!.Data!;
    }

    [Fact]
    public async Task CreateMonitor_ValidRequest_ReturnsCreated()
    {
        var jobId = await CreateTestJobAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/monitors", new
        {
            name = "Integration Monitor",
            watchTarget = """{"type":"local","path":"/data/incoming"}""",
            triggerEvents = 1,
            pollingIntervalSec = 60,
            maxConsecutiveFailures = 5,
            jobIds = new[] { jobId },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MonitorDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.ShouldNotBeNull();
        body.Data!.Name.ShouldBe("Integration Monitor");
        body.Data.State.ShouldBe("active");
        body.Data.Bindings.Count.ShouldBe(1);
        body.Data.Bindings[0].JobId.ShouldBe(jobId);
    }

    [Fact]
    public async Task CreateMonitor_EmptyName_ReturnsBadRequest()
    {
        var jobId = await CreateTestJobAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/monitors", new
        {
            name = "",
            watchTarget = """{"type":"local","path":"/data/incoming"}""",
            triggerEvents = 1,
            pollingIntervalSec = 60,
            jobIds = new[] { jobId },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateMonitor_PollingIntervalTooLow_ReturnsBadRequest()
    {
        var jobId = await CreateTestJobAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/monitors", new
        {
            name = "Bad Interval",
            watchTarget = """{"type":"local","path":"/data/incoming"}""",
            triggerEvents = 1,
            pollingIntervalSec = 10,
            jobIds = new[] { jobId },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateMonitor_NoJobIds_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/monitors", new
        {
            name = "No Jobs Monitor",
            watchTarget = """{"type":"local","path":"/data/incoming"}""",
            triggerEvents = 1,
            pollingIntervalSec = 60,
            jobIds = Array.Empty<Guid>(),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListMonitors_AfterCreate_ReturnsMonitor()
    {
        await CreateTestMonitorAsync();

        var response = await _client.GetAsync("/api/v1/monitors");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<MonitorDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListMonitors_FilterByState_ReturnsFiltered()
    {
        var monitor = await CreateTestMonitorAsync();

        // Pause it
        await _client.PostAsync($"/api/v1/monitors/{monitor.Id}/pause", null);

        var response = await _client.GetAsync("/api/v1/monitors?state=paused");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<MonitorDto>>();
        body!.Data.ShouldAllBe(m => m.State == "paused");
    }

    [Fact]
    public async Task GetMonitor_ExistingId_ReturnsMonitor()
    {
        var monitor = await CreateTestMonitorAsync();

        var response = await _client.GetAsync($"/api/v1/monitors/{monitor.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MonitorDto>>();
        body!.Data!.Id.ShouldBe(monitor.Id);
    }

    [Fact]
    public async Task GetMonitor_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/monitors/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateMonitor_ValidRequest_ReturnsUpdated()
    {
        var monitor = await CreateTestMonitorAsync();

        var response = await _client.PutAsJsonAsync($"/api/v1/monitors/{monitor.Id}", new
        {
            name = "Updated Name",
            pollingIntervalSec = 120,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MonitorDto>>();
        body!.Data!.Name.ShouldBe("Updated Name");
        body.Data.PollingIntervalSec.ShouldBe(120);
    }

    [Fact]
    public async Task DeleteMonitor_ExistingMonitor_SoftDeletes()
    {
        var monitor = await CreateTestMonitorAsync();

        var deleteResponse = await _client.DeleteAsync($"/api/v1/monitors/{monitor.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/v1/monitors/{monitor.Id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ActivateMonitor_FromPaused_ReturnsActive()
    {
        var monitor = await CreateTestMonitorAsync();
        await _client.PostAsync($"/api/v1/monitors/{monitor.Id}/pause", null);

        var response = await _client.PostAsync($"/api/v1/monitors/{monitor.Id}/activate", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MonitorDto>>();
        body!.Data!.State.ShouldBe("active");
    }

    [Fact]
    public async Task ActivateMonitor_AlreadyActive_ReturnsConflict()
    {
        var monitor = await CreateTestMonitorAsync();

        var response = await _client.PostAsync($"/api/v1/monitors/{monitor.Id}/activate", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PauseMonitor_FromActive_ReturnsPaused()
    {
        var monitor = await CreateTestMonitorAsync();

        var response = await _client.PostAsync($"/api/v1/monitors/{monitor.Id}/pause", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MonitorDto>>();
        body!.Data!.State.ShouldBe("paused");
    }

    [Fact]
    public async Task DisableMonitor_FromActive_ReturnsDisabled()
    {
        var monitor = await CreateTestMonitorAsync();

        var response = await _client.PostAsync($"/api/v1/monitors/{monitor.Id}/disable", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MonitorDto>>();
        body!.Data!.State.ShouldBe("disabled");
    }

    [Fact]
    public async Task AcknowledgeError_NotInError_ReturnsConflict()
    {
        var monitor = await CreateTestMonitorAsync();

        var response = await _client.PostAsync($"/api/v1/monitors/{monitor.Id}/acknowledge-error", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetFileLog_ExistingMonitor_ReturnsEmptyLog()
    {
        var monitor = await CreateTestMonitorAsync();

        var response = await _client.GetAsync($"/api/v1/monitors/{monitor.Id}/file-log");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<MonitorFileLogDto>>();
        body!.Success.ShouldBeTrue();
        body.Data.ShouldBeEmpty();
        body.Pagination.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetFileLog_NonExistentMonitor_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/monitors/{Guid.NewGuid()}/file-log");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

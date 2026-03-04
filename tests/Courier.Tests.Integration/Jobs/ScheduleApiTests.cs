using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Jobs;
using Shouldly;

namespace Courier.Tests.Integration.Jobs;

public class ScheduleApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public ScheduleApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateTestJob()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/jobs",
            new CreateJobRequest { Name = $"Schedule Test {Guid.NewGuid():N}" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
        return body!.Data!.Id;
    }

    [Fact]
    public async Task CreateSchedule_CronType_ReturnsCreated()
    {
        var jobId = await CreateTestJob();
        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/schedules", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobScheduleDto>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data!.ScheduleType.ShouldBe("cron");
        body.Data.CronExpression.ShouldBe("0 0 3 * * ?");
        body.Data.JobId.ShouldBe(jobId);
    }

    [Fact]
    public async Task CreateSchedule_OneShotType_ReturnsCreated()
    {
        var jobId = await CreateTestJob();
        var runAt = DateTimeOffset.UtcNow.AddHours(2);
        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "one_shot",
            RunAt = runAt,
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/schedules", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobScheduleDto>>();
        body!.Data!.ScheduleType.ShouldBe("one_shot");
        body.Data.RunAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateSchedule_NonExistentJob_ReturnsNotFound()
    {
        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{Guid.NewGuid()}/schedules", request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateSchedule_InvalidCronExpression_ReturnsBadRequest()
    {
        var jobId = await CreateTestJob();
        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "not-valid",
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/schedules", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSchedule_OneShotPastDate_ReturnsBadRequest()
    {
        var jobId = await CreateTestJob();
        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "one_shot",
            RunAt = DateTimeOffset.UtcNow.AddHours(-1),
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/schedules", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListSchedules_AfterCreate_ReturnsSchedules()
    {
        var jobId = await CreateTestJob();
        await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/schedules", new CreateJobScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
        });

        var response = await _client.GetAsync($"/api/v1/jobs/{jobId}/schedules");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<JobScheduleDto>>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListSchedules_NonExistentJob_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/jobs/{Guid.NewGuid()}/schedules");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateSchedule_ValidRequest_ReturnsOk()
    {
        var jobId = await CreateTestJob();
        var createResponse = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/schedules",
            new CreateJobScheduleRequest
            {
                ScheduleType = "cron",
                CronExpression = "0 0 3 * * ?",
            });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<JobScheduleDto>>();
        var scheduleId = created!.Data!.Id;

        var response = await _client.PutAsJsonAsync($"/api/v1/jobs/{jobId}/schedules/{scheduleId}",
            new UpdateJobScheduleRequest { CronExpression = "0 0 6 * * ?" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobScheduleDto>>();
        body!.Data!.CronExpression.ShouldBe("0 0 6 * * ?");
    }

    [Fact]
    public async Task UpdateSchedule_JobMismatch_ReturnsBadRequest()
    {
        // Create a schedule on one job
        var jobId = await CreateTestJob();
        var createResponse = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/schedules",
            new CreateJobScheduleRequest
            {
                ScheduleType = "cron",
                CronExpression = "0 0 3 * * ?",
            });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<JobScheduleDto>>();
        var scheduleId = created!.Data!.Id;

        // Try to update it via a different job
        var otherJobId = await CreateTestJob();
        var response = await _client.PutAsJsonAsync($"/api/v1/jobs/{otherJobId}/schedules/{scheduleId}",
            new UpdateJobScheduleRequest { IsEnabled = false });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteSchedule_ExistingSchedule_ReturnsOk()
    {
        var jobId = await CreateTestJob();
        var createResponse = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/schedules",
            new CreateJobScheduleRequest
            {
                ScheduleType = "cron",
                CronExpression = "0 0 3 * * ?",
            });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<JobScheduleDto>>();
        var scheduleId = created!.Data!.Id;

        var response = await _client.DeleteAsync($"/api/v1/jobs/{jobId}/schedules/{scheduleId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteSchedule_NonExistent_ReturnsNotFound()
    {
        var jobId = await CreateTestJob();

        var response = await _client.DeleteAsync($"/api/v1/jobs/{jobId}/schedules/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Chains;
using Shouldly;

namespace Courier.Tests.Integration.Chains;

public class ChainScheduleApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public ChainScheduleApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateChainAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/chains",
            new CreateChainRequest { Name = $"Chain-{Guid.NewGuid().ToString("N")[..8]}" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobChainDto>>();
        return body!.Data!.Id;
    }

    [Fact]
    public async Task ListSchedules_EmptyChain_ReturnsEmptyList()
    {
        var chainId = await CreateChainAsync();

        var response = await _client.GetAsync($"/api/v1/chains/{chainId}/schedules");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ChainScheduleDto>>>();
        body!.Success.ShouldBeTrue();
        body.Data!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task CreateSchedule_CronSchedule_Returns201()
    {
        var chainId = await CreateChainAsync();
        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/chains/{chainId}/schedules", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ChainScheduleDto>>();
        body!.Success.ShouldBeTrue();
        body.Data!.ScheduleType.ShouldBe("cron");
        body.Data.CronExpression.ShouldBe("0 0 3 * * ?");
        body.Data.ChainId.ShouldBe(chainId);
    }

    [Fact]
    public async Task CreateSchedule_OneShotSchedule_Returns201()
    {
        var chainId = await CreateChainAsync();
        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "one_shot",
            RunAt = DateTimeOffset.UtcNow.AddHours(2),
            IsEnabled = true,
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/chains/{chainId}/schedules", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ChainScheduleDto>>();
        body!.Success.ShouldBeTrue();
        body.Data!.ScheduleType.ShouldBe("one_shot");
    }

    [Fact]
    public async Task CreateSchedule_InvalidType_Returns400()
    {
        var chainId = await CreateChainAsync();
        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "weekly",
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/chains/{chainId}/schedules", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSchedule_NonexistentChain_Returns404()
    {
        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/chains/{Guid.NewGuid()}/schedules", request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateSchedule_ToggleEnabled_Returns200()
    {
        var chainId = await CreateChainAsync();
        var createResponse = await _client.PostAsJsonAsync($"/api/v1/chains/{chainId}/schedules",
            new CreateChainScheduleRequest { ScheduleType = "cron", CronExpression = "0 0 3 * * ?", IsEnabled = true });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ChainScheduleDto>>();

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/chains/{chainId}/schedules/{created!.Data!.Id}",
            new UpdateChainScheduleRequest { IsEnabled = false });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ChainScheduleDto>>();
        body!.Data!.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateSchedule_NotFound_Returns404()
    {
        var chainId = await CreateChainAsync();

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/chains/{chainId}/schedules/{Guid.NewGuid()}",
            new UpdateChainScheduleRequest { IsEnabled = false });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSchedule_Existing_Returns200()
    {
        var chainId = await CreateChainAsync();
        var createResponse = await _client.PostAsJsonAsync($"/api/v1/chains/{chainId}/schedules",
            new CreateChainScheduleRequest { ScheduleType = "cron", CronExpression = "0 0 3 * * ?", IsEnabled = true });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ChainScheduleDto>>();

        var response = await _client.DeleteAsync($"/api/v1/chains/{chainId}/schedules/{created!.Data!.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify it's gone
        var listResponse = await _client.GetAsync($"/api/v1/chains/{chainId}/schedules");
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<ChainScheduleDto>>>();
        list!.Data!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task DeleteSchedule_NotFound_Returns404()
    {
        var chainId = await CreateChainAsync();

        var response = await _client.DeleteAsync($"/api/v1/chains/{chainId}/schedules/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListSchedules_AfterCreate_ReturnsSchedule()
    {
        var chainId = await CreateChainAsync();
        await _client.PostAsJsonAsync($"/api/v1/chains/{chainId}/schedules",
            new CreateChainScheduleRequest { ScheduleType = "cron", CronExpression = "0 0 3 * * ?", IsEnabled = true });

        var response = await _client.GetAsync($"/api/v1/chains/{chainId}/schedules");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ChainScheduleDto>>>();
        body!.Data!.Count.ShouldBe(1);
    }
}

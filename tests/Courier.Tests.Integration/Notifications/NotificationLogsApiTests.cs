using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Notifications;
using Shouldly;

namespace Courier.Tests.Integration.Notifications;

public class NotificationLogsApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public NotificationLogsApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_ReturnsEmptyPagedResults()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/notification-logs");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<NotificationLogDto>>();
        body.ShouldNotBeNull();
        body!.Success.ShouldBeTrue();
        body.Pagination.ShouldNotBeNull();
    }

    [Fact]
    public async Task List_WithFilters_Returns200()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/notification-logs?entityType=job&success=true&page=1&pageSize=10");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

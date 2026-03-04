using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Notifications;
using Shouldly;

namespace Courier.Tests.Integration.Notifications;

public class NotificationRulesApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public NotificationRulesApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueName(string prefix = "Rule") =>
        $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    #region Create

    [Fact]
    public async Task Create_ValidWebhookRule_Returns201()
    {
        // Arrange
        var request = new CreateNotificationRuleRequest
        {
            Name = UniqueName("Webhook"),
            Description = "Integration test rule",
            EntityType = "job",
            EventTypes = ["job_failed", "job_completed"],
            Channel = "webhook",
            ChannelConfig = new { url = "https://example.com/webhook", secret = "s3cret" },
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/notification-rules", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationRuleDto>>();
        body.ShouldNotBeNull();
        body!.Success.ShouldBeTrue();
        body.Data.ShouldNotBeNull();
        body.Data!.Name.ShouldBe(request.Name);
        body.Data.Channel.ShouldBe("webhook");
        body.Data.EventTypes.ShouldContain("job_failed");
        body.Data.EventTypes.ShouldContain("job_completed");
        body.Data.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Create_ValidEmailRule_Returns201()
    {
        // Arrange
        var request = new CreateNotificationRuleRequest
        {
            Name = UniqueName("Email"),
            EntityType = "job",
            EventTypes = ["job_completed"],
            Channel = "email",
            ChannelConfig = new { recipients = new[] { "admin@example.com" }, subjectPrefix = "[Test]" },
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/notification-rules", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409()
    {
        // Arrange
        var name = UniqueName("Dup");
        await _client.PostAsJsonAsync("/api/v1/notification-rules", new CreateNotificationRuleRequest
        {
            Name = name,
            EntityType = "job",
            EventTypes = ["job_failed"],
            Channel = "webhook",
            ChannelConfig = new { url = "https://example.com" },
        });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/notification-rules", new CreateNotificationRuleRequest
        {
            Name = name,
            EntityType = "job",
            EventTypes = ["job_failed"],
            Channel = "webhook",
            ChannelConfig = new { url = "https://example.com" },
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_EmptyName_Returns400()
    {
        // Arrange
        var request = new CreateNotificationRuleRequest
        {
            Name = "",
            EntityType = "job",
            EventTypes = ["job_failed"],
            Channel = "webhook",
            ChannelConfig = new { url = "https://example.com" },
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/notification-rules", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_InvalidChannel_Returns400()
    {
        // Arrange
        var request = new CreateNotificationRuleRequest
        {
            Name = UniqueName(),
            EntityType = "job",
            EventTypes = ["job_failed"],
            Channel = "slack",
            ChannelConfig = new { url = "https://example.com" },
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/notification-rules", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region List

    [Fact]
    public async Task List_ReturnsPaginatedResults()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/v1/notification-rules", new CreateNotificationRuleRequest
        {
            Name = UniqueName("List"),
            EntityType = "job",
            EventTypes = ["job_failed"],
            Channel = "webhook",
            ChannelConfig = new { url = "https://example.com" },
        });

        // Act
        var response = await _client.GetAsync("/api/v1/notification-rules");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<NotificationRuleDto>>();
        body.ShouldNotBeNull();
        body!.Success.ShouldBeTrue();
        body.Data.ShouldNotBeEmpty();
        body.Pagination.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region Get

    [Fact]
    public async Task GetById_ExistingRule_Returns200()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/notification-rules", new CreateNotificationRuleRequest
        {
            Name = UniqueName("Get"),
            EntityType = "job",
            EventTypes = ["job_failed"],
            Channel = "webhook",
            ChannelConfig = new { url = "https://example.com" },
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<NotificationRuleDto>>();

        // Act
        var response = await _client.GetAsync($"/api/v1/notification-rules/{created!.Data!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/notification-rules/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Update

    [Fact]
    public async Task Update_ValidRequest_Returns200()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/notification-rules", new CreateNotificationRuleRequest
        {
            Name = UniqueName("Update"),
            EntityType = "job",
            EventTypes = ["job_failed"],
            Channel = "webhook",
            ChannelConfig = new { url = "https://example.com" },
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<NotificationRuleDto>>();

        var updateRequest = new UpdateNotificationRuleRequest
        {
            Name = UniqueName("Updated"),
            EntityType = "monitor",
            EventTypes = ["job_completed"],
            Channel = "email",
            ChannelConfig = new { recipients = new[] { "test@example.com" } },
            IsEnabled = false,
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/notification-rules/{created!.Data!.Id}", updateRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationRuleDto>>();
        body!.Data!.Name.ShouldBe(updateRequest.Name);
        body.Data.Channel.ShouldBe("email");
        body.Data.IsEnabled.ShouldBeFalse();
    }

    #endregion

    #region Delete

    [Fact]
    public async Task Delete_ExistingRule_Returns200()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/notification-rules", new CreateNotificationRuleRequest
        {
            Name = UniqueName("Delete"),
            EntityType = "job",
            EventTypes = ["job_failed"],
            Channel = "webhook",
            ChannelConfig = new { url = "https://example.com" },
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<NotificationRuleDto>>();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/notification-rules/{created!.Data!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify it's gone
        var getResponse = await _client.GetAsync($"/api/v1/notification-rules/{created.Data.Id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion
}

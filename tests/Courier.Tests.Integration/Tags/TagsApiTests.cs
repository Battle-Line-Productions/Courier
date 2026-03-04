using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Tags;
using Shouldly;

namespace Courier.Tests.Integration.Tags;

public class TagsApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public TagsApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueName(string prefix = "Tag") =>
        $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    #region Create

    [Fact]
    public async Task CreateTag_ValidRequest_Returns201()
    {
        // Arrange
        var request = new CreateTagRequest
        {
            Name = UniqueName("Create"),
            Color = "#FF0000",
            Category = "environment",
            Description = "Integration test tag"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tags", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TagDto>>();
        body.ShouldNotBeNull();
        body!.Success.ShouldBeTrue();
        body.Data.ShouldNotBeNull();
        body.Data!.Name.ShouldBe(request.Name);
        body.Data.Color.ShouldBe("#FF0000");
        body.Data.Category.ShouldBe("environment");
        body.Data.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateTag_DuplicateName_Returns409()
    {
        // Arrange
        var name = UniqueName("Dup");
        await _client.PostAsJsonAsync("/api/v1/tags", new CreateTagRequest { Name = name });

        // Act — same name again
        var response = await _client.PostAsJsonAsync("/api/v1/tags", new CreateTagRequest { Name = name });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateTag_EmptyName_Returns400()
    {
        // Arrange
        var request = new CreateTagRequest { Name = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tags", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region List

    [Fact]
    public async Task ListTags_ReturnsPaginatedList()
    {
        // Arrange
        var name = UniqueName("List");
        await _client.PostAsJsonAsync("/api/v1/tags", new CreateTagRequest { Name = name });

        // Act
        var response = await _client.GetAsync("/api/v1/tags");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedApiResponse<TagDto>>();
        body.ShouldNotBeNull();
        body!.Success.ShouldBeTrue();
        body.Data.ShouldNotBeEmpty();
        body.Pagination.ShouldNotBeNull();
        body.Pagination.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region GetById

    [Fact]
    public async Task GetTag_Exists_Returns200()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/tags",
            new CreateTagRequest { Name = UniqueName("Get") });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<TagDto>>();

        // Act
        var response = await _client.GetAsync($"/api/v1/tags/{created!.Data!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TagDto>>();
        body.ShouldNotBeNull();
        body!.Data.ShouldNotBeNull();
        body.Data!.Id.ShouldBe(created.Data.Id);
    }

    [Fact]
    public async Task GetTag_Missing_Returns404()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/tags/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Update

    [Fact]
    public async Task UpdateTag_ValidRequest_Returns200()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/tags",
            new CreateTagRequest { Name = UniqueName("Update"), Color = "#000000" });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<TagDto>>();

        var updateRequest = new UpdateTagRequest
        {
            Name = UniqueName("Updated"),
            Color = "#FFFFFF",
            Category = "updated-cat",
            Description = "Updated description"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/tags/{created!.Data!.Id}", updateRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TagDto>>();
        body.ShouldNotBeNull();
        body!.Data.ShouldNotBeNull();
        body.Data!.Name.ShouldBe(updateRequest.Name);
        body.Data.Color.ShouldBe("#FFFFFF");
    }

    #endregion

    #region Delete

    [Fact]
    public async Task DeleteTag_Returns200()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/tags",
            new CreateTagRequest { Name = UniqueName("Delete") });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<TagDto>>();
        var tagId = created!.Data!.Id;

        // Act
        var response = await _client.DeleteAsync($"/api/v1/tags/{tagId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify it no longer appears
        var getResponse = await _client.GetAsync($"/api/v1/tags/{tagId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Assign / Unassign

    [Fact]
    public async Task AssignTags_ValidRequest_Returns200()
    {
        // Arrange — create a tag and a job
        var tagResponse = await _client.PostAsJsonAsync("/api/v1/tags",
            new CreateTagRequest { Name = UniqueName("Assign") });
        var tag = (await tagResponse.Content.ReadFromJsonAsync<ApiResponse<TagDto>>())!.Data!;

        var jobResponse = await _client.PostAsJsonAsync("/api/v1/jobs",
            new { name = UniqueName("AssignJob") });
        var job = (await jobResponse.Content.ReadFromJsonAsync<ApiResponse<JobIdHolder>>())!.Data!;

        var assignRequest = new BulkTagAssignmentRequest
        {
            Assignments =
            [
                new TagAssignment { TagId = tag.Id, EntityType = "job", EntityId = job.Id }
            ]
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tags/assign", assignRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify via list entities endpoint
        var entitiesResponse = await _client.GetAsync($"/api/v1/tags/{tag.Id}/entities");
        entitiesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var entities = await entitiesResponse.Content.ReadFromJsonAsync<PagedApiResponse<TagEntityDto>>();
        entities.ShouldNotBeNull();
        entities!.Data.ShouldContain(e => e.EntityId == job.Id);
    }

    [Fact]
    public async Task UnassignTags_ValidRequest_Returns200()
    {
        // Arrange — create tag, job, and assign them
        var tagResponse = await _client.PostAsJsonAsync("/api/v1/tags",
            new CreateTagRequest { Name = UniqueName("Unassign") });
        var tag = (await tagResponse.Content.ReadFromJsonAsync<ApiResponse<TagDto>>())!.Data!;

        var jobResponse = await _client.PostAsJsonAsync("/api/v1/jobs",
            new { name = UniqueName("UnassignJob") });
        var job = (await jobResponse.Content.ReadFromJsonAsync<ApiResponse<JobIdHolder>>())!.Data!;

        var assignRequest = new BulkTagAssignmentRequest
        {
            Assignments =
            [
                new TagAssignment { TagId = tag.Id, EntityType = "job", EntityId = job.Id }
            ]
        };
        await _client.PostAsJsonAsync("/api/v1/tags/assign", assignRequest);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tags/unassign", assignRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify removed
        var entitiesResponse = await _client.GetAsync($"/api/v1/tags/{tag.Id}/entities");
        var entities = await entitiesResponse.Content.ReadFromJsonAsync<PagedApiResponse<TagEntityDto>>();
        entities!.Data.ShouldNotContain(e => e.EntityId == job.Id);
    }

    #endregion

    /// <summary>
    /// Minimal record to deserialize Job responses (only need the Id).
    /// </summary>
    private record JobIdHolder
    {
        public Guid Id { get; init; }
    }
}

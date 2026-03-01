using System.Net;
using System.Text.Json;
using Courier.Features.AzureFunctions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Azure;

public class AzureFunctionClientTests
{
    private static AzureFunctionClient CreateClient(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AzureFunctions").Returns(new HttpClient(handler));
        var logger = Substitute.For<ILogger<AzureFunctionClient>>();
        return new AzureFunctionClient(factory, logger);
    }

    [Fact]
    public async Task TriggerAsync_Returns202_ReturnsSuccess()
    {
        // Arrange
        var handler = new FakeHttpHandler(HttpStatusCode.Accepted, "");
        var client = CreateClient(handler);

        // Act
        var result = await client.TriggerAsync(
            "myapp.azurewebsites.net", "ProcessFile", "master-key-123", null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.TriggerTimeUtc.ShouldNotBeNull();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task TriggerAsync_Returns202_SendsCorrectHeaders()
    {
        // Arrange
        var handler = new FakeHttpHandler(HttpStatusCode.Accepted, "");
        var client = CreateClient(handler);

        // Act
        await client.TriggerAsync(
            "myapp.azurewebsites.net", "ProcessFile", "master-key-123", null, CancellationToken.None);

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.RequestUri!.ToString().ShouldBe("https://myapp.azurewebsites.net/admin/functions/ProcessFile");
        handler.LastRequest.Headers.GetValues("x-functions-key").ShouldContain("master-key-123");
    }

    [Fact]
    public async Task TriggerAsync_SendsInputPayload()
    {
        // Arrange
        var handler = new FakeHttpHandler(HttpStatusCode.Accepted, "");
        var client = CreateClient(handler);
        var payload = "{\"fileId\": \"abc-123\"}";

        // Act
        await client.TriggerAsync(
            "myapp.azurewebsites.net", "ProcessFile", "key", payload, CancellationToken.None);

        // Assert
        handler.LastRequestBody.ShouldNotBeNull();
        var json = JsonDocument.Parse(handler.LastRequestBody!).RootElement;
        json.GetProperty("input").GetString().ShouldBe(payload);
    }

    [Fact]
    public async Task TriggerAsync_Returns500_ReturnsFailure()
    {
        // Arrange
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError, "Function not found");
        var client = CreateClient(handler);

        // Act
        var result = await client.TriggerAsync(
            "myapp.azurewebsites.net", "MissingFn", "key", null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.TriggerTimeUtc.ShouldBeNull();
        result.ErrorMessage!.ShouldContain("500");
    }

    [Fact]
    public async Task TriggerAsync_NetworkError_ReturnsFailure()
    {
        // Arrange
        var handler = new ThrowingHttpHandler(new HttpRequestException("Connection refused"));
        var client = CreateClient(handler);

        // Act
        var result = await client.TriggerAsync(
            "unreachable.example.com", "Fn", "key", null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Connection refused");
    }

    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public FakeHttpHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content)
            };
        }
    }

    private class ThrowingHttpHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            throw _exception;
        }
    }
}

using System.Net;
using System.Text.Json;
using Courier.Features.AzureFunctions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Azure;

public class AppInsightsQueryServiceTests
{
    private static AppInsightsQueryService CreateService(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("LogAnalytics").Returns(new HttpClient(handler));
        var logger = Substitute.For<ILogger<AppInsightsQueryService>>();
        return new AppInsightsQueryService(factory, logger);
    }

    [Fact]
    public async Task PollForCompletionAsync_FindsResult_ReturnsExecutionResult()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            tables = new[]
            {
                new
                {
                    rows = new object[]
                    {
                        new object[] { "2026-02-28T12:00:00Z", "ProcessFile", 1500.0, "True", "inv-123", "op-456" }
                    }
                }
            }
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(handler);

        // Act
        var result = await service.PollForCompletionAsync(
            "workspace-id", "token", "ProcessFile",
            DateTime.UtcNow.AddMinutes(-1),
            pollIntervalSec: 1,
            maxWaitSec: 10,
            initialDelaySec: 0,
            CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result!.Success.ShouldBeTrue();
        result.DurationMs.ShouldBe(1500.0);
        result.InvocationId.ShouldBe("inv-123");
        result.OperationId.ShouldBe("op-456");
    }

    [Fact]
    public async Task PollForCompletionAsync_EmptyResponse_ReturnsNullOnTimeout()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            tables = new[]
            {
                new { rows = Array.Empty<object[]>() }
            }
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(handler);

        // Act
        var result = await service.PollForCompletionAsync(
            "workspace-id", "token", "ProcessFile",
            DateTime.UtcNow,
            pollIntervalSec: 1,
            maxWaitSec: 2,
            initialDelaySec: 0,
            CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task PollForCompletionAsync_FunctionFailed_ReturnsSuccessFalse()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            tables = new[]
            {
                new
                {
                    rows = new object[]
                    {
                        new object[] { "2026-02-28T12:00:00Z", "ProcessFile", 500.0, "False", "inv-789", "op-101" }
                    }
                }
            }
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(handler);

        // Act
        var result = await service.PollForCompletionAsync(
            "workspace-id", "token", "ProcessFile",
            DateTime.UtcNow.AddMinutes(-1),
            pollIntervalSec: 1,
            maxWaitSec: 10,
            initialDelaySec: 0,
            CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result!.Success.ShouldBeFalse();
        result.InvocationId.ShouldBe("inv-789");
    }

    [Fact]
    public async Task GetTracesAsync_ReturnsTraceEntries()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            tables = new[]
            {
                new
                {
                    rows = new object[]
                    {
                        new object[] { "2026-02-28T12:00:00Z", "Starting function", 1 },
                        new object[] { "2026-02-28T12:00:01Z", "Processing item", 1 },
                        new object[] { "2026-02-28T12:00:02Z", "Error occurred", 3 }
                    }
                }
            }
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(handler);

        // Act
        var traces = await service.GetTracesAsync(
            "workspace-id", "token", "inv-123", CancellationToken.None);

        // Assert
        traces.Count.ShouldBe(3);
        traces[0].Message.ShouldBe("Starting function");
        traces[0].SeverityLevel.ShouldBe(1);
        traces[2].Message.ShouldBe("Error occurred");
        traces[2].SeverityLevel.ShouldBe(3);
    }

    [Fact]
    public async Task GetTracesAsync_ApiError_ReturnsEmptyList()
    {
        // Arrange
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError, "error");
        var service = CreateService(handler);

        // Act
        var traces = await service.GetTracesAsync(
            "workspace-id", "token", "inv-123", CancellationToken.None);

        // Assert
        traces.ShouldBeEmpty();
    }

    [Fact]
    public async Task PollForCompletionAsync_RespectsCancellation()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            tables = new[] { new { rows = Array.Empty<object[]>() } }
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await service.PollForCompletionAsync(
                "workspace-id", "token", "ProcessFile",
                DateTime.UtcNow,
                pollIntervalSec: 60,
                maxWaitSec: 3600,
                initialDelaySec: 0,
                cts.Token));
    }

    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public FakeHttpHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content)
            });
        }
    }
}

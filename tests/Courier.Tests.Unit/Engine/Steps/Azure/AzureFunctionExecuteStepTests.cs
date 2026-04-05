using System.Net;
using System.Text;
using System.Text.Json;
using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Features.Callbacks;
using Courier.Features.Engine.Steps.Azure;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Steps.Azure;

public class AzureFunctionExecuteStepTests
{
    private static CourierDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    private static ICredentialEncryptor CreateMockEncryptor()
    {
        var mock = Substitute.For<ICredentialEncryptor>();
        mock.Encrypt(Arg.Any<string>()).Returns(ci => Encoding.UTF8.GetBytes(ci.Arg<string>()));
        mock.Decrypt(Arg.Any<byte[]>()).Returns(ci => Encoding.UTF8.GetString(ci.Arg<byte[]>()));
        return mock;
    }

    private static IHttpClientFactory CreateMockHttpFactory(HttpStatusCode statusCode, string responseBody = "")
    {
        var handler = new FakeHttpHandler(statusCode, responseBody);
        var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AzureFunctions").Returns(client);
        return factory;
    }

    private static IConfiguration CreateConfig(string? baseUrl = "https://courier.test")
    {
        var dict = new Dictionary<string, string?>();
        if (baseUrl is not null)
            dict["Courier:BaseUrl"] = baseUrl;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static async Task<Guid> SeedConnection(CourierDbContext db)
    {
        var conn = new Domain.Entities.Connection
        {
            Id = Guid.CreateVersion7(),
            Name = "Test Azure Func",
            Protocol = "azure_function",
            Host = "myapp.azurewebsites.net",
            AuthMethod = "function_key",
            Username = "function_key",
            PasswordEncrypted = Encoding.UTF8.GetBytes("test-function-key"),
            Port = 443,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Connections.Add(conn);
        await db.SaveChangesAsync();
        return conn.Id;
    }

    [Fact]
    public async Task FireAndForget_Success_ReturnsOk()
    {
        using var db = CreateDb();
        var connId = await SeedConnection(db);
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(), new StepCallbackService(db),
            CreateMockHttpFactory(HttpStatusCode.OK), CreateConfig(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration(JsonSerializer.Serialize(new
        {
            connection_id = connId.ToString(),
            function_name = "MyFunc",
            wait_for_callback = false,
        }));

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Outputs!["function_success"].ShouldBe(true);
        result.Outputs["http_status"].ShouldBe(200);
    }

    [Fact]
    public async Task FireAndForget_HttpError_ReturnsFail()
    {
        using var db = CreateDb();
        var connId = await SeedConnection(db);
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(), new StepCallbackService(db),
            CreateMockHttpFactory(HttpStatusCode.InternalServerError, "boom"), CreateConfig(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration(JsonSerializer.Serialize(new
        {
            connection_id = connId.ToString(),
            function_name = "MyFunc",
            wait_for_callback = false,
        }));

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("500");
    }

    [Fact]
    public async Task Callback_MissingBaseUrl_ReturnsFail()
    {
        using var db = CreateDb();
        var connId = await SeedConnection(db);
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(), new StepCallbackService(db),
            CreateMockHttpFactory(HttpStatusCode.OK), CreateConfig(baseUrl: null),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration(JsonSerializer.Serialize(new
        {
            connection_id = connId.ToString(),
            function_name = "MyFunc",
            wait_for_callback = true,
        }));

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Courier:BaseUrl");
    }

    [Fact]
    public async Task Callback_HttpTriggerFails_DeletesCallbackAndFails()
    {
        using var db = CreateDb();
        var connId = await SeedConnection(db);
        var callbackService = new StepCallbackService(db);
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(), callbackService,
            CreateMockHttpFactory(HttpStatusCode.InternalServerError), CreateConfig(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration(JsonSerializer.Serialize(new
        {
            connection_id = connId.ToString(),
            function_name = "MyFunc",
        }));

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        // Callback should have been cleaned up
        var callbacks = await db.StepCallbacks.ToListAsync();
        callbacks.ShouldBeEmpty();
    }

    [Fact]
    public async Task Validate_MissingConnectionId_Fails()
    {
        using var db = CreateDb();
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(), new StepCallbackService(db),
            CreateMockHttpFactory(HttpStatusCode.OK), CreateConfig(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration(JsonSerializer.Serialize(new { function_name = "MyFunc" }));
        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("connection_id");
    }

    [Fact]
    public async Task Validate_MissingFunctionName_Fails()
    {
        using var db = CreateDb();
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(), new StepCallbackService(db),
            CreateMockHttpFactory(HttpStatusCode.OK), CreateConfig(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration(JsonSerializer.Serialize(new { connection_id = Guid.NewGuid().ToString() }));
        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("function_name");
    }

    [Fact]
    public async Task ConnectionNotFound_ReturnsFail()
    {
        using var db = CreateDb();
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(), new StepCallbackService(db),
            CreateMockHttpFactory(HttpStatusCode.OK), CreateConfig(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration(JsonSerializer.Serialize(new
        {
            connection_id = Guid.NewGuid().ToString(),
            function_name = "MyFunc",
            wait_for_callback = false,
        }));

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("not found");
    }

    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public FakeHttpHandler(HttpStatusCode statusCode, string body = "")
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body),
            });
        }
    }
}

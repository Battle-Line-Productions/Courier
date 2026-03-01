using System.Text.Json;
using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Features.AzureFunctions;
using Courier.Features.Engine.Steps.Azure;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Azure;

public class AzureFunctionExecuteStepTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    private static AzureFunctionClient CreateMockFunctionClient()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var logger = Substitute.For<ILogger<AzureFunctionClient>>();
        return Substitute.ForPartsOf<AzureFunctionClient>(factory, logger);
    }

    private static AppInsightsQueryService CreateMockAppInsights()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var logger = Substitute.For<ILogger<AppInsightsQueryService>>();
        return Substitute.ForPartsOf<AppInsightsQueryService>(factory, logger);
    }

    private static ICredentialEncryptor CreateMockEncryptor()
    {
        var encryptor = Substitute.For<ICredentialEncryptor>();
        encryptor.Encrypt(Arg.Any<string>()).Returns(ci => System.Text.Encoding.UTF8.GetBytes($"enc:{ci.Arg<string>()}"));
        encryptor.Decrypt(Arg.Any<byte[]>()).Returns(ci =>
        {
            var bytes = ci.Arg<byte[]>();
            var str = System.Text.Encoding.UTF8.GetString(bytes);
            return str.StartsWith("enc:") ? str[4..] : str;
        });
        return encryptor;
    }

    private static Courier.Domain.Entities.Connection CreateAzureFunctionConnection(ICredentialEncryptor encryptor)
    {
        var props = JsonSerializer.Serialize(new
        {
            workspace_id = "ws-123",
            tenant_id = "tenant-456",
            client_id = "client-789"
        });

        return new Courier.Domain.Entities.Connection
        {
            Id = Guid.NewGuid(),
            Name = "Test Azure Function",
            Protocol = "azure_function",
            Host = "myapp.azurewebsites.net",
            Port = 443,
            AuthMethod = "service_principal",
            Username = "app-name",
            PasswordEncrypted = encryptor.Encrypt("master-key"),
            ClientSecretEncrypted = encryptor.Encrypt("client-secret"),
            Properties = props,
            HostKeyPolicy = "always_trust",
            TlsCertPolicy = "system_trust",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public void TypeKey_IsAzureFunctionExecute()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(),
            CreateMockFunctionClient(),
            CreateMockAppInsights(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        // Assert
        step.TypeKey.ShouldBe("azure_function.execute");
    }

    [Fact]
    public async Task ValidateAsync_MissingConnectionId_ReturnsFail()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(),
            CreateMockFunctionClient(),
            CreateMockAppInsights(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration("""{"function_name":"Fn"}""");

        // Act
        var result = await step.ValidateAsync(config);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("connection_id");
    }

    [Fact]
    public async Task ValidateAsync_MissingFunctionName_ReturnsFail()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(),
            CreateMockFunctionClient(),
            CreateMockAppInsights(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration("""{"connection_id":"some-guid"}""");

        // Act
        var result = await step.ValidateAsync(config);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("function_name");
    }

    [Fact]
    public async Task ValidateAsync_AllRequiredFields_ReturnsOk()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(),
            CreateMockFunctionClient(),
            CreateMockAppInsights(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration("""{"connection_id":"some-guid","function_name":"Fn"}""");

        // Act
        var result = await step.ValidateAsync(config);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ConnectionNotFound_ReturnsFail()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(),
            CreateMockFunctionClient(),
            CreateMockAppInsights(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var connId = Guid.NewGuid();
        var config = new StepConfiguration($$"""{"connection_id":"{{connId}}","function_name":"Fn"}""");

        // Act
        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_WrongProtocol_ReturnsFail()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var encryptor = CreateMockEncryptor();

        var connection = new Courier.Domain.Entities.Connection
        {
            Id = Guid.NewGuid(),
            Name = "SFTP Connection",
            Protocol = "sftp",
            Host = "sftp.example.com",
            Port = 22,
            AuthMethod = "password",
            Username = "user",
            HostKeyPolicy = "always_trust",
            TlsCertPolicy = "system_trust",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Connections.Add(connection);
        await db.SaveChangesAsync();

        var step = new AzureFunctionExecuteStep(
            db, encryptor,
            CreateMockFunctionClient(),
            CreateMockAppInsights(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration($$"""{"connection_id":"{{connection.Id}}","function_name":"Fn"}""");

        // Act
        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("azure_function");
    }

    [Fact]
    public async Task ExecuteAsync_TriggerFails_ReturnsFail()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var encryptor = CreateMockEncryptor();
        var connection = CreateAzureFunctionConnection(encryptor);
        db.Connections.Add(connection);
        await db.SaveChangesAsync();

        var functionClient = CreateMockFunctionClient();
        functionClient.TriggerAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AzureFunctionTriggerResult(false, null, "Network error"));

        var step = new AzureFunctionExecuteStep(
            db, encryptor, functionClient,
            CreateMockAppInsights(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration($$"""{"connection_id":"{{connection.Id}}","function_name":"ProcessFile","initial_delay_sec":0,"poll_interval_sec":1,"max_wait_sec":5}""");

        // Act
        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("trigger failed");
    }

    [Fact]
    public async Task ExecuteAsync_PollTimeout_ReturnsFail()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var encryptor = CreateMockEncryptor();
        var connection = CreateAzureFunctionConnection(encryptor);
        db.Connections.Add(connection);
        await db.SaveChangesAsync();

        var triggerTime = DateTime.UtcNow;
        var functionClient = CreateMockFunctionClient();
        functionClient.TriggerAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AzureFunctionTriggerResult(true, triggerTime));

        var appInsights = CreateMockAppInsights();
        appInsights.AcquireTokenAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("fake-token");
        appInsights.PollForCompletionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((FunctionExecutionResult?)null);

        var step = new AzureFunctionExecuteStep(
            db, encryptor, functionClient, appInsights,
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration($$"""{"connection_id":"{{connection.Id}}","function_name":"ProcessFile","initial_delay_sec":0,"poll_interval_sec":1,"max_wait_sec":5}""");

        // Act
        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("did not complete");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulExecution_ReturnsOkWithOutputs()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var encryptor = CreateMockEncryptor();
        var connection = CreateAzureFunctionConnection(encryptor);
        db.Connections.Add(connection);
        await db.SaveChangesAsync();

        var triggerTime = DateTime.UtcNow;
        var functionClient = CreateMockFunctionClient();
        functionClient.TriggerAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AzureFunctionTriggerResult(true, triggerTime));

        var appInsights = CreateMockAppInsights();
        appInsights.AcquireTokenAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("fake-token");
        appInsights.PollForCompletionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new FunctionExecutionResult(true, 2500.0, "inv-abc", "op-def"));

        var step = new AzureFunctionExecuteStep(
            db, encryptor, functionClient, appInsights,
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration($$"""{"connection_id":"{{connection.Id}}","function_name":"ProcessFile","input_payload":"{\"fileId\":\"123\"}","initial_delay_sec":0,"poll_interval_sec":1,"max_wait_sec":60}""");

        // Act
        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Outputs.ShouldNotBeNull();
        result.Outputs!["invocation_id"].ShouldBe("inv-abc");
        result.Outputs["operation_id"].ShouldBe("op-def");
        result.Outputs["function_success"].ShouldBe(true);
        result.Outputs["function_duration_ms"].ShouldBe(2500.0);
    }

    [Fact]
    public async Task ExecuteAsync_FunctionReportsFailure_ReturnsFail()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var encryptor = CreateMockEncryptor();
        var connection = CreateAzureFunctionConnection(encryptor);
        db.Connections.Add(connection);
        await db.SaveChangesAsync();

        var triggerTime = DateTime.UtcNow;
        var functionClient = CreateMockFunctionClient();
        functionClient.TriggerAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AzureFunctionTriggerResult(true, triggerTime));

        var appInsights = CreateMockAppInsights();
        appInsights.AcquireTokenAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("fake-token");
        appInsights.PollForCompletionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new FunctionExecutionResult(false, 500.0, "inv-fail", "op-fail"));

        var step = new AzureFunctionExecuteStep(
            db, encryptor, functionClient, appInsights,
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration($$"""{"connection_id":"{{connection.Id}}","function_name":"ProcessFile","initial_delay_sec":0,"poll_interval_sec":1,"max_wait_sec":60}""");

        // Act
        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("reported failure");
    }
}

using Courier.Features.Callbacks;
using Courier.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Shouldly;

namespace Courier.Tests.Unit.Callbacks;

public class CallbacksControllerTests
{
    private static CourierDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    private static CallbacksController CreateController(StepCallbackService service, string? bearerToken = null)
    {
        var controller = new CallbacksController(service);
        var httpContext = new DefaultHttpContext();
        if (bearerToken is not null)
            httpContext.Request.Headers.Authorization = new StringValues($"Bearer {bearerToken}");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task ReceiveCallback_ValidKey_Returns200()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, key) = await service.CreateAsync(3600);
        var controller = CreateController(service, key);

        var result = await controller.ReceiveCallback(id, new CallbackRequest { Success = true }, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ReceiveCallback_MissingAuth_Returns401()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, _) = await service.CreateAsync(3600);
        var controller = CreateController(service, bearerToken: null);

        var result = await controller.ReceiveCallback(id, new CallbackRequest(), CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ReceiveCallback_WrongKey_Returns401()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, _) = await service.CreateAsync(3600);
        var controller = CreateController(service, "wrong-key");

        var result = await controller.ReceiveCallback(id, new CallbackRequest(), CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ReceiveCallback_NotFound_Returns404()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var controller = CreateController(service, "some-key");

        var result = await controller.ReceiveCallback(Guid.NewGuid(), new CallbackRequest(), CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ReceiveCallback_AlreadyCompleted_Returns409()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, key) = await service.CreateAsync(3600);
        var controller = CreateController(service, key);

        await controller.ReceiveCallback(id, new CallbackRequest(), CancellationToken.None);
        var result = await controller.ReceiveCallback(id, new CallbackRequest(), CancellationToken.None);

        result.ShouldBeOfType<ConflictObjectResult>();
    }
}

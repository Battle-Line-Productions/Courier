using System.Text.Json;
using Courier.Features.Callbacks;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Callbacks;

public class StepCallbackServiceTests
{
    private static CourierDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    [Fact]
    public async Task Create_ReturnsIdAndKey()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);

        var (id, key) = await service.CreateAsync(3600);

        id.ShouldNotBe(Guid.Empty);
        key.ShouldNotBeNullOrEmpty();

        var callback = await db.StepCallbacks.FindAsync(id);
        callback.ShouldNotBeNull();
        callback!.Status.ShouldBe("pending");
        callback.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow.AddSeconds(3500));
    }

    [Fact]
    public async Task ProcessCallback_ValidKey_SetsCompleted()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, key) = await service.CreateAsync(3600);

        var output = JsonDocument.Parse("""{"count": 42}""").RootElement;
        var request = new CallbackRequest { Success = true, Output = output };

        var (success, error) = await service.ProcessCallbackAsync(id, key, request);

        success.ShouldBeTrue();
        error.ShouldBeNull();

        var callback = await db.StepCallbacks.FindAsync(id);
        callback!.Status.ShouldBe("completed");
        callback.ResultPayload.ShouldNotBeNull();
        callback.ResultPayload.ShouldContain("count");
        callback.ResultPayload.ShouldContain("42");
        callback.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ProcessCallback_WrongKey_ReturnsUnauthorized()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, _) = await service.CreateAsync(3600);

        var (success, error) = await service.ProcessCallbackAsync(id, "wrong-key", new CallbackRequest());

        success.ShouldBeFalse();
        error.ShouldBe("unauthorized");
    }

    [Fact]
    public async Task ProcessCallback_AlreadyCompleted_ReturnsAlreadyCompleted()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, key) = await service.CreateAsync(3600);

        await service.ProcessCallbackAsync(id, key, new CallbackRequest());
        var (success, error) = await service.ProcessCallbackAsync(id, key, new CallbackRequest());

        success.ShouldBeFalse();
        error.ShouldBe("already_completed");
    }

    [Fact]
    public async Task ProcessCallback_Expired_ReturnsExpired()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, key) = await service.CreateAsync(0); // expires immediately

        await Task.Delay(50); // ensure expiry

        var (success, error) = await service.ProcessCallbackAsync(id, key, new CallbackRequest());

        success.ShouldBeFalse();
        error.ShouldBe("expired");
    }

    [Fact]
    public async Task ProcessCallback_NotFound_ReturnsNotFound()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);

        var (success, error) = await service.ProcessCallbackAsync(Guid.NewGuid(), "key", new CallbackRequest());

        success.ShouldBeFalse();
        error.ShouldBe("not_found");
    }

    [Fact]
    public async Task ProcessCallback_Failed_SetsFailedStatus()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, key) = await service.CreateAsync(3600);

        var request = new CallbackRequest { Success = false, ErrorMessage = "boom" };
        var (success, _) = await service.ProcessCallbackAsync(id, key, request);

        success.ShouldBeTrue();
        var callback = await db.StepCallbacks.FindAsync(id);
        callback!.Status.ShouldBe("failed");
        callback.ErrorMessage.ShouldBe("boom");
    }

    [Fact]
    public async Task Delete_RemovesCallback()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, _) = await service.CreateAsync(3600);

        await service.DeleteAsync(id);

        var callback = await db.StepCallbacks.FindAsync(id);
        callback.ShouldBeNull();
    }

    [Fact]
    public async Task MarkExpired_SetsFailed()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, _) = await service.CreateAsync(3600);

        await service.MarkExpiredAsync(id);

        var callback = await db.StepCallbacks.FindAsync(id);
        callback!.Status.ShouldBe("failed");
        callback.ErrorMessage!.ShouldContain("expired");
    }
}

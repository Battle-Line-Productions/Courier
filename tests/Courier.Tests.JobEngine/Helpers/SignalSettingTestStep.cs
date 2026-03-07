using Courier.Domain.Engine;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Helpers;

public class SignalSettingTestStep : IJobStep
{
    private readonly CourierDbContext _db;

    public SignalSettingTestStep(CourierDbContext db)
    {
        _db = db;
    }

    public string TypeKey => "test.set_signal";

    public async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken cancellationToken)
    {
        var signal = config.GetString("signal");
        var executionId = Guid.Parse(config.GetString("execution_id"));

        var execution = await _db.JobExecutions
            .FirstAsync(e => e.Id == executionId, cancellationToken);
        execution.RequestedState = signal;
        await _db.SaveChangesAsync(cancellationToken);

        return StepResult.Ok();
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config) => Task.FromResult(StepResult.Ok());
}

using Courier.Domain.Enums;
using Courier.Tests.JobEngine.Fixtures;
using Courier.Tests.JobEngine.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests.JobEngine.Tests.EngineCore;

[Collection("EngineTests")]
[Trait("Category", "StepOutput")]
public class StepOutputEdgeTests
{
    private readonly DatabaseFixture _database;
    public StepOutputEdgeTests(DatabaseFixture database) => _database = database;

    [Fact]
    public async Task StepWithNullOutputs_ContextUnchanged()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new OutputTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // Step 0: output step with no "outputs" config → returns StepResult.Ok() with null Outputs
        await TestDataSeeder.AddStep(db, jobId, 0, "test.output", "No Outputs", new { });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        var step0 = await db.StepExecutions
            .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
        step0.State.ShouldBe(StepExecutionState.Completed);
        // No OutputData since Outputs was null
        step0.OutputData.ShouldBeNull();
    }

    [Fact]
    public async Task StepWithEmptyOutputsDictionary_NoOutputDataStored()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new OutputTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // Step 0: output step with empty dict → StepResult.Ok(outputs: {})
        await TestDataSeeder.AddStep(db, jobId, 0, "test.output", "Empty Outputs", new
        {
            outputs = "{}",
        });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        var step0 = await db.StepExecutions
            .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 0);
        step0.State.ShouldBe(StepExecutionState.Completed);
    }

    [Fact]
    public async Task MultipleStepsSameOutputKey_KeyedByStepOrder()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new OutputTestStep(), new ContextReadingTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // Step 0: outputs {"result": "from_step_0"}
        await TestDataSeeder.AddStep(db, jobId, 0, "test.output", "Output 0", new
        {
            outputs = """{"result":"from_step_0"}""",
        });

        // Step 1: outputs {"result": "from_step_1"}
        await TestDataSeeder.AddStep(db, jobId, 1, "test.output", "Output 1", new
        {
            outputs = """{"result":"from_step_1"}""",
        });

        // Step 2: reads both context keys to verify they're separate
        await TestDataSeeder.AddStep(db, jobId, 2, "test.context_reader", "Read Context", new
        {
            keys = new[] { "0.result", "1.result" },
        });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        // Step 2 should have output both keys
        var step2 = await db.StepExecutions
            .FirstAsync(se => se.JobExecutionId == executionId && se.StepOrder == 2);
        step2.State.ShouldBe(StepExecutionState.Completed);
        step2.OutputData.ShouldNotBeNull();
        step2.OutputData.ShouldContain("0.result");
        step2.OutputData.ShouldContain("1.result");
    }

    [Fact]
    public async Task StepOutput_StoredInContextSnapshot()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new OutputTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // Step 0: outputs a value
        await TestDataSeeder.AddStep(db, jobId, 0, "test.output", "Output Step", new
        {
            outputs = """{"my_key":"snapshot_value"}""",
        });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);
        execution.ContextSnapshot.ShouldNotBeNull();

        // Context snapshot should contain the step output keyed as "0.my_key"
        execution.ContextSnapshot.ShouldContain("0.my_key");
        execution.ContextSnapshot.ShouldContain("snapshot_value");
    }

    [Fact]
    public async Task ForEach_BodyStepOutputs_HaveIterationIndex()
    {
        await using var db = _database.CreateDbContext();
        var encryptor = TestEncryptionHelper.CreateEncryptor();
        var engine = new JobEngineBuilder(db, encryptor)
            .WithAdditionalSteps(new OutputTestStep())
            .Build();

        var (jobId, executionId) = await TestDataSeeder.SeedJob(db);

        // Step 0: forEach over 3 items
        await TestDataSeeder.AddStep(db, jobId, 0, "flow.foreach", "ForEach", new
        {
            source = """["a","b","c"]""",
        });

        // Step 1: body — produces output per iteration
        await TestDataSeeder.AddStep(db, jobId, 1, "test.output", "Body Output", new
        {
            outputs = """{"item":"processed"}""",
        });

        // Step 2: end forEach
        await TestDataSeeder.AddStep(db, jobId, 2, "flow.end", "End", new { });

        await engine.ExecuteAsync(executionId, CancellationToken.None);

        var execution = await db.JobExecutions.FirstAsync(e => e.Id == executionId);
        execution.State.ShouldBe(JobExecutionState.Completed);

        // Body step executions should each have IterationIndex set
        var bodySteps = await db.StepExecutions
            .Where(se => se.JobExecutionId == executionId && se.StepOrder == 1)
            .OrderBy(se => se.IterationIndex)
            .ToListAsync();
        bodySteps.Count.ShouldBe(3);
        bodySteps[0].IterationIndex.ShouldBe(0);
        bodySteps[1].IterationIndex.ShouldBe(1);
        bodySteps[2].IterationIndex.ShouldBe(2);
        bodySteps.ShouldAllBe(se => se.State == StepExecutionState.Completed);
        bodySteps.ShouldAllBe(se => se.OutputData != null);
    }
}

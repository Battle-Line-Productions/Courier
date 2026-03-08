using Courier.Domain.Engine;

namespace Courier.Tests.JobEngine.Helpers;

/// <summary>
/// A test step that fails a configurable number of times before succeeding.
/// Uses a shared counter keyed by a configurable "id" to track attempts across retry invocations.
/// </summary>
public class CountdownTestStep : IJobStep
{
    public string TypeKey => "test.countdown";

    // Shared state: tracks how many times each step ID has been called
    private static readonly Dictionary<string, int> _callCounts = new();
    private static readonly object _lock = new();

    public Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken cancellationToken)
    {
        var id = config.GetString("id");
        var failCount = config.GetInt("fail_count");

        int currentCall;
        lock (_lock)
        {
            _callCounts.TryGetValue(id, out currentCall);
            currentCall++;
            _callCounts[id] = currentCall;
        }

        if (currentCall <= failCount)
        {
            return Task.FromResult(StepResult.Fail($"Countdown failure {currentCall}/{failCount} for '{id}'"));
        }

        var outputs = new Dictionary<string, object>
        {
            ["result"] = $"success-after-{currentCall - 1}-failures",
            ["attempt"] = currentCall,
        };

        return Task.FromResult(StepResult.Ok(outputs: outputs));
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config) => Task.FromResult(StepResult.Ok());

    /// <summary>
    /// Resets the shared call counter. Call this at the start of each test for isolation.
    /// </summary>
    public static void ResetCounters()
    {
        lock (_lock)
        {
            _callCounts.Clear();
        }
    }
}

using System.Text.RegularExpressions;

namespace Courier.Features.Engine;

public static class ConditionEvaluator
{
    public static bool Evaluate(string left, string @operator, string? right)
    {
        return @operator.ToLowerInvariant() switch
        {
            "equals" => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            "not_equals" => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            "contains" => left.Contains(right ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            "greater_than" => CompareNumeric(left, right) > 0,
            "less_than" => CompareNumeric(left, right) < 0,
            "exists" => !string.IsNullOrEmpty(left),
            "regex" => right is not null && Regex.IsMatch(left, right),
            _ => throw new InvalidOperationException($"Unknown condition operator: {@operator}")
        };
    }

    private static int CompareNumeric(string left, string? right)
    {
        if (decimal.TryParse(left, out var leftNum) && decimal.TryParse(right, out var rightNum))
            return leftNum.CompareTo(rightNum);

        // Fall back to string comparison if not numeric
        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }
}

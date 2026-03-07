using Courier.Features.Engine;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class ConditionEvaluatorEdgeTests
{
    // ── Equals edge cases ─────────────────────────────────────────────

    [Fact]
    public void Evaluate_Equals_EmptyLeftEmptyRight_ReturnsTrue()
    {
        ConditionEvaluator.Evaluate("", "equals", "").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_Equals_NullRight_ReturnsFalse()
    {
        ConditionEvaluator.Evaluate("hello", "equals", null).ShouldBeFalse();
    }

    // ── Contains edge cases ───────────────────────────────────────────

    [Fact]
    public void Evaluate_Contains_EmptyRight_ReturnsTrue()
    {
        // string.Contains("") is always true
        ConditionEvaluator.Evaluate("hello", "contains", "").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_Contains_NullRight_ReturnsTrue()
    {
        // null right coalesces to string.Empty, and Contains("") is true
        ConditionEvaluator.Evaluate("hello", "contains", null).ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_Contains_EmptyLeft_ReturnsFalse()
    {
        ConditionEvaluator.Evaluate("", "contains", "xyz").ShouldBeFalse();
    }

    // ── Numeric comparison edge cases ─────────────────────────────────

    [Fact]
    public void Evaluate_GreaterThan_NonNumericBothSides_UsesStringComparison()
    {
        // "banana" > "apple" in ordinal string comparison
        ConditionEvaluator.Evaluate("banana", "greater_than", "apple").ShouldBeTrue();
        ConditionEvaluator.Evaluate("apple", "greater_than", "banana").ShouldBeFalse();
    }

    [Fact]
    public void Evaluate_LessThan_DecimalValues_Works()
    {
        ConditionEvaluator.Evaluate("2.71", "less_than", "3.14").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_LessThan_EqualValues_ReturnsFalse()
    {
        ConditionEvaluator.Evaluate("5", "less_than", "5").ShouldBeFalse();
    }

    [Fact]
    public void Evaluate_GreaterThan_EqualValues_ReturnsFalse()
    {
        ConditionEvaluator.Evaluate("5", "greater_than", "5").ShouldBeFalse();
    }

    [Fact]
    public void Evaluate_GreaterThan_NullRight_FallsBackToStringComparison()
    {
        // decimal.TryParse(null) returns false, falls back to string.Compare
        // string.Compare("10", null) > 0
        ConditionEvaluator.Evaluate("10", "greater_than", null).ShouldBeTrue();
    }

    // ── Exists edge cases ─────────────────────────────────────────────

    [Fact]
    public void Evaluate_Exists_EmptyString_ReturnsFalse()
    {
        ConditionEvaluator.Evaluate("", "exists", null).ShouldBeFalse();
    }

    [Fact]
    public void Evaluate_Exists_WhitespaceOnly_ReturnsTrue()
    {
        // IsNullOrEmpty does not trim, so whitespace is "exists"
        ConditionEvaluator.Evaluate("   ", "exists", null).ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_Exists_NullRight_Ignored()
    {
        // Right operand is irrelevant for "exists"
        ConditionEvaluator.Evaluate("value", "exists", null).ShouldBeTrue();
    }

    // ── Regex edge cases ──────────────────────────────────────────────

    [Fact]
    public void Evaluate_Regex_EmptyPattern_MatchesAnything()
    {
        // Regex.IsMatch("anything", "") is true
        ConditionEvaluator.Evaluate("anything", "regex", "").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_Regex_EmptyLeft_MatchesEmptyPattern()
    {
        ConditionEvaluator.Evaluate("", "regex", "^$").ShouldBeTrue();
    }
}

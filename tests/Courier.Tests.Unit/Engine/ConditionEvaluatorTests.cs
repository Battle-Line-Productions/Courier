using Courier.Features.Engine;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class ConditionEvaluatorTests
{
    // ── Equals ─────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_Equals_MatchingStrings_ReturnsTrue()
    {
        ConditionEvaluator.Evaluate("hello", "equals", "hello").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_Equals_CaseInsensitive_ReturnsTrue()
    {
        ConditionEvaluator.Evaluate("Hello", "equals", "HELLO").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_Equals_DifferentStrings_ReturnsFalse()
    {
        ConditionEvaluator.Evaluate("hello", "equals", "world").ShouldBeFalse();
    }

    // ── Not Equals ─────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_NotEquals_DifferentStrings_ReturnsTrue()
    {
        ConditionEvaluator.Evaluate("hello", "not_equals", "world").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_NotEquals_MatchingStrings_ReturnsFalse()
    {
        ConditionEvaluator.Evaluate("hello", "not_equals", "hello").ShouldBeFalse();
    }

    // ── Contains ───────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_Contains_SubstringPresent_ReturnsTrue()
    {
        ConditionEvaluator.Evaluate("hello world", "contains", "world").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_Contains_CaseInsensitive_ReturnsTrue()
    {
        ConditionEvaluator.Evaluate("Hello World", "contains", "WORLD").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_Contains_SubstringAbsent_ReturnsFalse()
    {
        ConditionEvaluator.Evaluate("hello", "contains", "xyz").ShouldBeFalse();
    }

    // ── Greater Than ───────────────────────────────────────────────────

    [Fact]
    public void Evaluate_GreaterThan_NumericComparison_ReturnsTrue()
    {
        ConditionEvaluator.Evaluate("1048576", "greater_than", "1024").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_GreaterThan_NumericComparison_ReturnsFalse()
    {
        ConditionEvaluator.Evaluate("100", "greater_than", "1000").ShouldBeFalse();
    }

    [Fact]
    public void Evaluate_GreaterThan_DecimalValues_Works()
    {
        ConditionEvaluator.Evaluate("3.14", "greater_than", "2.71").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_GreaterThan_NonNumeric_FallsBackToStringComparison()
    {
        ConditionEvaluator.Evaluate("banana", "greater_than", "apple").ShouldBeTrue();
    }

    // ── Less Than ──────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_LessThan_NumericComparison_ReturnsTrue()
    {
        ConditionEvaluator.Evaluate("100", "less_than", "1000").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_LessThan_NumericComparison_ReturnsFalse()
    {
        ConditionEvaluator.Evaluate("1000", "less_than", "100").ShouldBeFalse();
    }

    // ── Exists ─────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_Exists_NonEmptyValue_ReturnsTrue()
    {
        ConditionEvaluator.Evaluate("something", "exists", null).ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_Exists_EmptyValue_ReturnsFalse()
    {
        ConditionEvaluator.Evaluate("", "exists", null).ShouldBeFalse();
    }

    [Fact]
    public void Evaluate_Exists_IgnoresRight()
    {
        ConditionEvaluator.Evaluate("value", "exists", "ignored").ShouldBeTrue();
    }

    // ── Regex ──────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_Regex_MatchingPattern_ReturnsTrue()
    {
        ConditionEvaluator.Evaluate("file_2024_report.csv", "regex", @"file_\d{4}_.*\.csv").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_Regex_NonMatchingPattern_ReturnsFalse()
    {
        ConditionEvaluator.Evaluate("report.txt", "regex", @"\.csv$").ShouldBeFalse();
    }

    [Fact]
    public void Evaluate_Regex_NullPattern_ReturnsFalse()
    {
        ConditionEvaluator.Evaluate("anything", "regex", null).ShouldBeFalse();
    }

    // ── Unknown operator ───────────────────────────────────────────────

    [Fact]
    public void Evaluate_UnknownOperator_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(
            () => ConditionEvaluator.Evaluate("a", "unknown_op", "b"));
    }

    // ── Operator case insensitivity ────────────────────────────────────

    [Fact]
    public void Evaluate_OperatorCaseInsensitive_Works()
    {
        ConditionEvaluator.Evaluate("hello", "EQUALS", "hello").ShouldBeTrue();
        ConditionEvaluator.Evaluate("hello", "Greater_Than", "apple").ShouldBeTrue();
    }
}

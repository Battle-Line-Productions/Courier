using Courier.Worker.Services;
using Shouldly;

namespace Courier.Tests.Unit.Maintenance;

public class PartitionMaintenanceServiceTests
{
    [Theory]
    [InlineData(2026, 3, 2, DayOfWeek.Monday, 7)]    // Monday → next Monday (7 days)
    [InlineData(2026, 3, 3, DayOfWeek.Tuesday, 6)]    // Tuesday → Monday (6 days)
    [InlineData(2026, 3, 4, DayOfWeek.Wednesday, 5)]   // Wednesday → Monday (5 days)
    [InlineData(2026, 3, 5, DayOfWeek.Thursday, 4)]    // Thursday → Monday (4 days)
    [InlineData(2026, 3, 6, DayOfWeek.Friday, 3)]      // Friday → Monday (3 days)
    [InlineData(2026, 3, 7, DayOfWeek.Saturday, 2)]    // Saturday → Monday (2 days)
    [InlineData(2026, 3, 8, DayOfWeek.Sunday, 1)]      // Sunday → Monday (1 day)
    public void GetDelayUntilNextRun_ReturnsCorrectDays(int year, int month, int day, DayOfWeek expectedDow, int expectedDays)
    {
        // Arrange
        var now = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        now.DayOfWeek.ShouldBe(expectedDow);

        // Act
        var delay = PartitionMaintenanceService.GetDelayUntilNextRun(now);

        // Assert
        delay.TotalDays.ShouldBe(expectedDays);
    }

    [Fact]
    public void GetDelayUntilNextRun_MidDayMonday_ReturnsLessThan7Days()
    {
        // Arrange — Monday at noon
        var now = new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var delay = PartitionMaintenanceService.GetDelayUntilNextRun(now);

        // Assert — should be 6.5 days (next Monday 00:00 minus Monday noon)
        delay.TotalDays.ShouldBe(6.5);
    }

    [Fact]
    public void GetDelayUntilNextRun_AlwaysPositive()
    {
        // Act & Assert — every day of the week produces 1-7 days
        for (var i = 0; i < 7; i++)
        {
            var now = new DateTime(2026, 3, 2 + i, 0, 0, 0, DateTimeKind.Utc);
            var delay = PartitionMaintenanceService.GetDelayUntilNextRun(now);

            delay.TotalDays.ShouldBeGreaterThan(0);
            delay.TotalDays.ShouldBeLessThanOrEqualTo(7);
        }
    }

    [Fact]
    public void GetDelayUntilNextRun_ResultAlwaysLandsOnMonday()
    {
        // Act & Assert — every day of the week results in a Monday
        for (var i = 0; i < 7; i++)
        {
            var now = new DateTime(2026, 3, 2 + i, 15, 30, 0, DateTimeKind.Utc);
            var delay = PartitionMaintenanceService.GetDelayUntilNextRun(now);
            var nextRun = now + delay;

            nextRun.DayOfWeek.ShouldBe(DayOfWeek.Monday);
            nextRun.TimeOfDay.ShouldBe(TimeSpan.Zero); // 00:00 UTC
        }
    }
}

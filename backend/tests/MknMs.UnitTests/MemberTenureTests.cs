using FluentAssertions;
using MknMs.Application.Common;
using Xunit;

namespace MknMs.UnitTests;

public class MemberTenureTests
{
    [Theory]
    [InlineData("2024-01-15", "2024-01-15", 0)]    // same day
    [InlineData("2024-01-15", "2024-01-16", 1)]    // next day
    [InlineData("2024-01-15", "2024-02-14", 30)]   // thirty days
    [InlineData("2024-01-15", "2025-01-15", 366)]  // one calendar year, leap year spans Feb 29
    [InlineData("2023-01-15", "2024-01-15", 365)]  // one calendar year, no leap day
    public void DaysSince_VariousSpans(string join, string today, int expected)
    {
        MemberTenure.DaysSince(DateOnly.Parse(join), DateOnly.Parse(today))
            .Should().Be(expected);
    }

    [Fact]
    public void DaysSince_NegativeWhenTodayBeforeJoin()
    {
        MemberTenure.DaysSince(new DateOnly(2024, 1, 15), new DateOnly(2024, 1, 14))
            .Should().Be(-1);
    }
}

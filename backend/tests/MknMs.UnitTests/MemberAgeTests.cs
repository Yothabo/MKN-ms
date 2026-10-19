using FluentAssertions;
using MknMs.Application.Common;
using Xunit;

namespace MknMs.UnitTests;

public class MemberAgeTests
{
    [Theory]
    [InlineData("1990-05-20", "2020-05-20", 30)]  // exact birthday
    [InlineData("1990-05-20", "2020-05-19", 29)]  // day before
    [InlineData("1990-05-20", "2020-05-21", 30)]  // day after
    [InlineData("1990-01-01", "2020-01-01", 30)]  // Jan 1
    [InlineData("1990-12-31", "2020-12-31", 30)]  // Dec 31
    public void CompletedYears_OrdinaryDates(string dob, string today, int expected)
    {
        MemberAge.CompletedYears(DateOnly.Parse(dob), DateOnly.Parse(today))
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("2000-02-29", "2019-02-28", 18)]  // leap-day anniversary in non-leap year observed on Feb 28
    [InlineData("2000-02-29", "2019-03-01", 19)]  // day after the observed anniversary
    [InlineData("2000-02-29", "2020-02-29", 20)]  // actual leap-day anniversary
    public void CompletedYears_LeapDay(string dob, string today, int expected)
    {
        MemberAge.CompletedYears(DateOnly.Parse(dob), DateOnly.Parse(today))
            .Should().Be(expected);
    }

    [Fact]
    public void CompletedYears_SameDay_IsZero()
    {
        MemberAge.CompletedYears(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 19))
            .Should().Be(0);
    }
}

using FluentAssertions;
using MknMs.Application.Common;
using Xunit;

namespace MknMs.UnitTests;

public class IsoDateTests
{
    // Note: DateOnly.TryParse is culture-sensitive. The inputs below
    // are ones whose parse result is the same under every supported
    // culture: unambiguous dates, an out-of-range month, and empty/null.
    // Culture-dependent separators (e.g. "2026/10/19") are excluded
    // because TryParse accepts them under InvariantCulture and the
    // adapter contract does not require stricter parsing than
    // DateOnly.TryParse provides.
    [Theory]
    [InlineData("2026-10-19", true)]
    [InlineData("2024-02-29", true)]  // leap day accepted
    [InlineData("2023-02-29", false)] // non-leap-day rejected
    [InlineData("2026-13-01", false)] // out-of-range month
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryParse_Date_RejectsMalformed(string? input, bool expected)
    {
        IsoDate.TryParse(input, out _).Should().Be(expected);
    }

    [Fact]
    public void TryParse_Date_ReturnsDate()
    {
        IsoDate.TryParse("2026-10-19", out var d).Should().BeTrue();
        d.Should().Be(new DateOnly(2026, 10, 19));
    }

    [Theory]
    [InlineData("2026-10-19T12:00:00+00:00", true)]
    [InlineData("not-a-timestamp", false)]
    [InlineData(null, false)]
    public void TryParseTimestamp_RejectsMalformed(string? input, bool expected)
    {
        IsoDate.TryParseTimestamp(input, out _).Should().Be(expected);
    }
}

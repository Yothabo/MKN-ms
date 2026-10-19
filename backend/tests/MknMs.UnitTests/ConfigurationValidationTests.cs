using FluentAssertions;
using MknMs.Application.Common;
using Xunit;

namespace MknMs.UnitTests;

public class ConfigurationValidationTests
{
    [Theory]
    [InlineData("Valid", true)]
    [InlineData("with space", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsValidName(string? input, bool expected)
    {
        ConfigurationValidation.IsValidName(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    public void IsNonNegative(int input, bool expected)
    {
        ConfigurationValidation.IsNonNegative(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    public void IsPositive(int input, bool expected)
    {
        ConfigurationValidation.IsPositive(input).Should().Be(expected);
    }
}

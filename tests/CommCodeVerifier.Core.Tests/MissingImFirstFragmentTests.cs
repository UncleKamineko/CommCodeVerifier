using CommCodeVerifier.Core;
using Xunit;

namespace CommCodeVerifier.Core.Tests;

public sealed class MissingImFirstFragmentTests
{
    [Theory]
    [InlineData("E53E-16-15", "E")]
    [InlineData("E53V-16-15", "E")]
    [InlineData("E55E-16-15", "E")]
    [InlineData("K02V-31-40", "K")]
    [InlineData("K02-31-40", "K")]
    [InlineData("40M2L100A0200", "40")]
    [InlineData("6512 6-1/8", "6512")]
    public void ExtractsFirstRequiredFragment(
        string value,
        string expected)
    {
        string? actual =
            MissingImFirstFragment.Extract(value);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(
        "E53E-16-15",
        "E53V-16-15",
        true)]
    [InlineData(
        "K02V-31-40",
        "K02-31-40",
        true)]
    [InlineData(
        "40M2L100A0200",
        "40M5X200B0100",
        true)]
    [InlineData(
        "E53E-16-15",
        "K02V-31-40",
        false)]
    [InlineData(
        "40M2L100A0200",
        "6512 6-1/8",
        false)]
    public void ComparesFirstRequiredFragments(
        string candidate,
        string reference,
        bool expected)
    {
        bool actual =
            MissingImFirstFragment.AreEqual(
                candidate,
                reference);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-40-20")]
    [InlineData("/40/20")]
    public void ReturnsNullWhenValueDoesNotStartWithLetterOrDigit(
        string value)
    {
        string? actual =
            MissingImFirstFragment.Extract(value);

        Assert.Null(actual);
    }
}

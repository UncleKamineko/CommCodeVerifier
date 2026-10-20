using CommCodeVerifier.Core;
using Xunit;

namespace CommCodeVerifier.Core.Tests;

public sealed class MissingImTests
{
    [Fact]
    public void SimilarNumericParameterCodesHaveHighSimilarity()
    {
        var candidate =
            MissingImCodeParser.Parse(
                "A-100",
                "40M2L100A0459");

        var reference =
            MissingImCodeParser.Parse(
                "A-101",
                "40M2L100A0200");

        var result =
            MissingImSimilarity.Compare(
                candidate,
                reference,
                articleSimilarity: 0.95,
                imPresenceInFamily: 0.90,
                new MissingImWeights());

        Assert.True(result.Score >= 0.70);
    }

    [Fact]
    public void DifferentLetterSegmentHasLowerSimilarity()
    {
        var sameLetterCandidate =
            MissingImCodeParser.Parse(
                "A-100",
                "40M2L100A0459");

        var sameLetterReference =
            MissingImCodeParser.Parse(
                "A-101",
                "40M2L100A0200");

        var differentLetterCandidate =
            MissingImCodeParser.Parse(
                "A-100",
                "40M2L100B0459");

        var sameLetterResult =
            MissingImSimilarity.Compare(
                sameLetterCandidate,
                sameLetterReference,
                0.95,
                0.90,
                new MissingImWeights());

        var differentLetterResult =
            MissingImSimilarity.Compare(
                differentLetterCandidate,
                sameLetterReference,
                0.95,
                0.90,
                new MissingImWeights());

        Assert.True(
            sameLetterResult.Score >
            differentLetterResult.Score);
    }

    [Fact]
    public void DifferentStructuresDoNotHaveSameSignature()
    {
        var first =
            MissingImCodeParser.Parse(
                "A-100",
                "40M2L100A0200");

        var second =
            MissingImCodeParser.Parse(
                "A-101",
                "6512 6-1/8");

        Assert.NotEqual(
            first.Signature,
            second.Signature);
    }

    [Fact]
    public void WeightsAreNormalizedToTwoDecimalPlaces()
    {
        var weights = new MissingImWeights
        {
            FragmentType = 0.567,
            CodeLength = 2.00,
            Separators = -0.10
        };

        weights.Normalize();

        Assert.Equal(0.57, weights.FragmentType);
        Assert.Equal(1.00, weights.CodeLength);
        Assert.Equal(0.00, weights.Separators);
    }

    [Fact]
    public void OldSettingsWithoutMissingImUseDefaults()
    {
        const string json = """
    {
      "EnabledRules": [
        "TrimSpaces"
      ]
    }
    """;

        var settings =
            ProcessingSettings.FromJson(json);

        Assert.NotNull(settings.MissingIm);
        Assert.NotNull(settings.MissingIm.Weights);
        Assert.Equal(
            0.80,
            settings.MissingIm.MinimumCodeSimilarity);
    }
    [Fact]
    public void LocalGroupRulesUseSpecifiedSizeBands()
    {
        var settings = new MissingImSettings();

        Assert.Equal(
            1,
            settings.GetThresholdForGroupSize(5)
                .MinimumPositiveReferences);
        Assert.Equal(
            0.20,
            settings.GetThresholdForGroupSize(5)
                .MinimumPositiveRatio);

        Assert.Equal(
            2,
            settings.GetThresholdForGroupSize(6)
                .MinimumPositiveReferences);
        Assert.Equal(
            0.50,
            settings.GetThresholdForGroupSize(20)
                .MinimumPositiveRatio);

        Assert.Equal(
            3,
            settings.GetThresholdForGroupSize(21)
                .MinimumPositiveReferences);
        Assert.Equal(
            0.75,
            settings.GetThresholdForGroupSize(100)
                .MinimumPositiveRatio);

        Assert.Equal(
            5,
            settings.GetThresholdForGroupSize(101)
                .MinimumPositiveReferences);
        Assert.Equal(
            0.85,
            settings.GetThresholdForGroupSize(101)
                .MinimumPositiveRatio);
    }
}

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
        Assert.Equal(0.30, settings.MissingIm.MinimumCodeSimilarity);
        Assert.Equal(0.75, settings.MissingIm.PositionDecay);
        Assert.Equal(0.30, settings.MissingIm.ArticlePositionDecay);
        Assert.Equal(0.30, settings.MissingIm.MinimumArticleSimilarityForLocalGroup);
    }
    [Fact]
    public void LocalGroupRulesUseSpecifiedSizeBands()
    {
        var settings = new MissingImSettings();

        Assert.Equal(
            1,
            settings.GetThresholdForGroupSize(5)
                .MinimumPositiveReferences);
        Assert.Equal(0.20,
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
            0.60,
            settings.GetThresholdForGroupSize(50)
                .MinimumPositiveRatio);

        Assert.Equal(
            4,
            settings.GetThresholdForGroupSize(51)
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
    [Fact]
    public void ArticleSimilarityThresholdRejectsDifferentProductsAndAllowsManualCodeErrors()
    {
        var settings = new MissingImSettings
        {
            ArticlePositionDecay = 0.75,
            MinimumArticleSimilarityForLocalGroup = 0.3
        };

        bool differentProductsPass =
            MissingImChecker.PassesArticleSimilarityThreshold(
                "SRF20-3V10-0001",
                "SRF35-2X22-8003D1",
                settings,
                out double differentProductsSimilarity);

        Assert.True(differentProductsSimilarity < 0.35);
        Assert.False(differentProductsPass);

        bool mistypedPrefixPasses =
            MissingImChecker.PassesArticleSimilarityThreshold(
                "SRF20-3V10-0001",
                "SRF70-3V10-0001",
                settings,
                out double mistypedPrefixSimilarity);

        Assert.True(mistypedPrefixSimilarity >= 0.35);
        Assert.True(mistypedPrefixPasses);

        settings.MinimumArticleSimilarityForLocalGroup =
            mistypedPrefixSimilarity;

        Assert.True(MissingImChecker.PassesArticleSimilarityThreshold(
            "SRF20-3V10-0001",
            "SRF70-3V10-0001",
            settings,
            out _));
    }

    [Fact]
    public void MinimumArticleSimilarityForLocalGroupIsClonedNormalizedAndSerialized()
    {
        var settings = new MissingImSettings
        {
            MinimumArticleSimilarityForLocalGroup = 0.47
        };

        Assert.Equal(
            0.47,
            settings.Clone().MinimumArticleSimilarityForLocalGroup);

        settings.MinimumArticleSimilarityForLocalGroup = 1.50;
        settings.Normalize();

        Assert.Equal(
            1.00,
            settings.MinimumArticleSimilarityForLocalGroup);

        var roundTrip = ProcessingSettings.FromJson(
            new ProcessingSettings
            {
                MissingIm = new MissingImSettings
                {
                    MinimumArticleSimilarityForLocalGroup = 0.47
                }
            }.ToJson());

        Assert.Equal(
            0.47,
            roundTrip.MissingIm.MinimumArticleSimilarityForLocalGroup);
    }
    [Fact]
    public void FirstNumericArticleFragmentRequiresExactMatch()
    {
        double exactMatch = MissingImSimilarity.CompareArticles(
            "SRF50",
            "SRF50");

        double differentFirstNumericFragment =
            MissingImSimilarity.CompareArticles(
                "SRF50",
                "SRF30");

        Assert.Equal(1.00, exactMatch);
        Assert.Equal(0.00, differentFirstNumericFragment);
    }
}

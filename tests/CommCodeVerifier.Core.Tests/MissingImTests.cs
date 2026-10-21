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
        Assert.Equal(0.75, settings.MissingIm.PositionDecay);
        Assert.Equal(0.75, settings.MissingIm.ArticlePositionDecay);
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
    [Fact]
    public void PositionDecayIsReportedForEachBlockAndAffectsStructuralScore()
    {
        var candidate =
            MissingImCodeParser.Parse("A-1", "19A1B");

        var reference =
            MissingImCodeParser.Parse("A-1", "19C1B");

        double decayed = MissingImSimilarity.CompareCodeBlocks(
            candidate,
            reference,
            positionDecay: 0.75);

        double noDecay = MissingImSimilarity.CompareCodeBlocks(
            candidate,
            reference,
            positionDecay: 1.00);

        Assert.True(decayed < noDecay);

        var detailed = MissingImSimilarity.Compare(
            candidate,
            reference,
            articleSimilarity: 1.00,
            imPresenceInFamily: 1.00,
            weights: new MissingImWeights(),
            positionDecay: 0.75,
            includeDiagnostics: true);

        Assert.Equal(3, detailed.BlockDiagnostics.Count);
        Assert.Equal(
            1.00,
            detailed.BlockDiagnostics[0].PositionWeight);
        Assert.Equal(
            0.75,
            detailed.BlockDiagnostics[1].PositionWeight);
        Assert.Equal(
            0.5625,
            detailed.BlockDiagnostics[2].PositionWeight);

    }
    [Fact]
    public void MissingLetterAndNumericFeaturesAreNotApplicableAndExcludedFromScore()
    {
        var candidate =
            MissingImCodeParser.Parse("A-1", "2003 1");

        var reference =
            MissingImCodeParser.Parse("A-2", "2003 1/2");

        var result = MissingImSimilarity.Compare(
            candidate,
            reference,
            articleSimilarity: 0.50,
            imPresenceInFamily: 0.50,
            weights: new MissingImWeights(),
            includeDiagnostics: true);

        var letterSegments = result.FeatureDiagnostics
            .Single(item => item.Name == "Буквенные сегменты");

        var letterPositions = result.FeatureDiagnostics
            .Single(item => item.Name == "Позиции буквенных сегментов");

        Assert.False(letterSegments.IsApplicable);
        Assert.False(letterPositions.IsApplicable);

        Assert.Equal(0.00, letterSegments.WeightSharePercent);
        Assert.Equal(0.00, letterSegments.ContributionPercent);
        Assert.Equal(0.00, letterSegments.EffectOnScorePercentagePoints);

        Assert.DoesNotContain(
            "Буквенные сегменты",
            result.MatchedSegments);

        Assert.DoesNotContain(
            "Буквенные сегменты",
            result.DifferentSegments);

        // Для кодов без цифровых фрагментов числовые характеристики тоже Н/П.
        var lettersOnlyCandidate =
            MissingImCodeParser.Parse("A-3", "AB-X");

        var lettersOnlyReference =
            MissingImCodeParser.Parse("A-4", "AB-Y");

        var numericResult = MissingImSimilarity.Compare(
            lettersOnlyCandidate,
            lettersOnlyReference,
            articleSimilarity: 0.50,
            imPresenceInFamily: 0.50,
            weights: new MissingImWeights(),
            includeDiagnostics: true);

        Assert.False(
            numericResult.FeatureDiagnostics
                .Single(item => item.Name == "Числовые блоки")
                .IsApplicable);

        Assert.False(
            numericResult.FeatureDiagnostics
                .Single(item => item.Name == "Позиции числовых блоков")
                .IsApplicable);

        // Вклад неприменимых характеристик не попадает в общую оценку.
        Assert.Equal(
            numericResult.ScorePercent,
            numericResult.FeatureDiagnostics
                .Sum(item => item.ContributionPercent),
            precision: 8);
    }

    [Fact]
    public void ArticleAndCodePositionDecayAreIndependentSettings()
    {
        var settings = new MissingImSettings
        {
            PositionDecay = 0.40,
            ArticlePositionDecay = 0.85
        };

        var clone = settings.Clone();

        Assert.Equal(0.40, clone.PositionDecay);
        Assert.Equal(0.85, clone.ArticlePositionDecay);

        string json = new ProcessingSettings
        {
            MissingIm = settings
        }.ToJson();

        var roundTrip = ProcessingSettings.FromJson(json);

        Assert.Equal(0.40, roundTrip.MissingIm.PositionDecay);
        Assert.Equal(0.85, roundTrip.MissingIm.ArticlePositionDecay);

        double articleScore = MissingImSimilarity.CompareArticles(
            "SRF50-X",
            "SRF50-Y",
            settings.ArticlePositionDecay);

        double articleScoreWithoutDecay = MissingImSimilarity.CompareArticles(
            "SRF50-X",
            "SRF50-Y",
            positionDecay: 1.00);

        Assert.True(articleScore > articleScoreWithoutDecay);
    }

    [Fact]
    public void DiagnosticRowsWithDifferentFirstNumericFragmentsAreYellow()
    {
        string folder = Path.Combine(
            Path.GetTempPath(),
            "CommCodeVerifier-YellowRows-" +
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(folder);

        try
        {
            var diagnostic = new MissingImPairDiagnostic
            {
                CandidateArticle = "A-1",
                CandidateCode = "SRF70-ABC",
                ReferenceArticle = "A-2",
                ReferenceCode = "SRF50-ABC",
                FirstCandidateNumericFragment = "70",
                FirstReferenceNumericFragment = "50",
                FirstNumericFragmentDiffers = true,
                SimilarityPercent = 80.00,
                Features = new List<MissingImFeatureDiagnostic>
            {
                new()
                {
                    Name = "Буквенные сегменты",
                    IsApplicable = false,
                    Weight = 1.00,
                    WeightSharePercent = 0.00,
                    ContributionPercent = 0.00,
                    EffectOnScorePercentagePoints = 0.00
                }
            }
            };

            string output = MissingImReportWriter.Write(
                folder,
                Array.Empty<MissingImCandidate>(),
                new[] { diagnostic });

            using var workbook =
                new ClosedXML.Excel.XLWorkbook(output);

            var sheet = workbook.Worksheet("Диагностика сходства");

            // Строка 2 — сводная строка пары.
            Assert.Equal("70", sheet.Cell(2, 29).GetString());
            Assert.Equal("50", sheet.Cell(2, 30).GetString());

            Assert.Equal(
                ClosedXML.Excel.XLColor.Yellow.Color,
                sheet.Range(2, 1, 2, 30)
                    .Style.Fill.BackgroundColor.Color);

            // Строка 3 — характеристика «Буквенные сегменты».
            Assert.Equal("Н/П", sheet.Cell(3, 17).GetString());

            Assert.Equal(
                ClosedXML.Excel.XLColor.Yellow.Color,
                sheet.Range(3, 1, 3, 30)
                    .Style.Fill.BackgroundColor.Color);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void MissingImCandidatesMatchingConfiguredImRulesAreExcluded()
    {
        string folder = Path.Combine(
            Path.GetTempPath(),
            "CommCodeVerifier-ImRuleFilter-" +
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(folder);

        string sourcePath = Path.Combine(folder, "input.xlsx");
        string codeRulesPath = Path.Combine(folder, "code-rules.xlsx");
        string articleRulesPath = Path.Combine(folder, "article-rules.xlsx");
        string outputFolder = Path.Combine(folder, "output");

        try
        {
            // RF** — правило окончания для коммерческого кода.
            CreateRuleWorkbook(
                codeRulesPath,
                ImRuleTarget.Code,
                ImRuleKind.EndsWith,
                "RF**");

            // BLOCK* — правило начала артикула.
            CreateRuleWorkbook(
                articleRulesPath,
                ImRuleTarget.Article,
                ImRuleKind.StartsWith,
                "BLOCK*");

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("Данные");

                sheet.Cell(1, 1).Value =
                    MissingImChecker.SourceHeaderArticle;

                sheet.Cell(1, 2).Value =
                    MissingImChecker.SourceHeaderCode;

                sheet.Cell(1, 3).Value =
                    MissingImChecker.SourceHeaderGroup;

                string[,] rows =
                {
                // Положительные записи-референсы.
                { "A-REF", "1254 1/2", "R1" },
                { "A-REF2", "1254 1/2-X1", "R1" },

                // Совпадает с правилом окончания коммерческого кода RF**.
                { "A-CODE-BLOCKED", "1254 1/2-RF01", "" },

                // Совпадает с правилом артикула BLOCK*.
                { "BLOCK-ARTICLE", "1254 1/2-X2", "" },

                // Контрольная строка: не совпадает ни с одним правилом.
                { "A-ALLOWED", "1254 1/2-X3", "" }
            };

                for (int row = 0; row < rows.GetLength(0); row++)
                {
                    for (int column = 0;
                         column < rows.GetLength(1);
                         column++)
                    {
                        sheet.Cell(row + 2, column + 1).Value =
                            rows[row, column];
                    }
                }

                workbook.SaveAs(sourcePath);
            }

            ConfigRepository.Reload(
                new ProcessingSettings
                {
                    ImRulesFilePath = codeRulesPath,
                    ImArticleRulesFilePath = articleRulesPath
                });

            var settings = new MissingImSettings
            {
                MinimumCodeSimilarity = 0.00
            };

            settings.LocalGroupThresholds[0]
                .MinimumPositiveReferences = 1;

            settings.LocalGroupThresholds[0]
                .MinimumPositiveRatio = 0.00;

            settings.Normalize();

            var report = MissingImChecker.Run(
                sourcePath,
                settings,
                progress: null,
                cancellationToken: CancellationToken.None,
                outputFolder: outputFolder);

            Assert.DoesNotContain(
                report.Candidates,
                item => item.CommercialCode == "1254 1/2-RF01");

            Assert.DoesNotContain(
                report.Candidates,
                item => item.Article == "BLOCK-ARTICLE");

            Assert.Contains(
                report.Candidates,
                item => item.Article == "A-ALLOWED");

            Assert.Contains(
                report.SimilarityDiagnostics,
                item =>
                    item.CandidateCode == "1254 1/2-RF01" &&
                    item.CandidateDecision.Contains(
                        "RF**",
                        StringComparison.Ordinal));

            Assert.Contains(
                report.SimilarityDiagnostics,
                item =>
                    item.CandidateArticle == "BLOCK-ARTICLE" &&
                    item.CandidateDecision.Contains(
                        "BLOCK*",
                        StringComparison.Ordinal));
        }
        finally
        {
            // Возвращаем конфигурацию тестового окружения.
            ConfigRepository.Reload(new ProcessingSettings());

            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static void CreateRuleWorkbook(
        string path,
        ImRuleTarget target,
        ImRuleKind kind,
        string pattern)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();

        string startsHeader = target == ImRuleTarget.Code
            ? "Префиксы комм. кода для исключения"
            : "Начало артикула для исключения";

        string endsHeader = target == ImRuleTarget.Code
            ? "Окончания комм. кода для исключения"
            : "Окончание артикула для исключения";

        string containsHeader = target == ImRuleTarget.Code
            ? "Части значений для исключения"
            : "Символы в артикуле для исключения";

        var starts = workbook.AddWorksheet(ImRuleSet.SheetStarts);
        starts.Cell(1, 1).Value = startsHeader;

        var ends = workbook.AddWorksheet(ImRuleSet.SheetEnds);
        ends.Cell(1, 1).Value = endsHeader;

        var contains = workbook.AddWorksheet(ImRuleSet.SheetContains);
        contains.Cell(1, 1).Value = containsHeader;

        var targetSheet = kind switch
        {
            ImRuleKind.StartsWith => starts,
            ImRuleKind.EndsWith => ends,
            _ => contains
        };

        targetSheet.Cell(2, 1).Value = pattern;
        workbook.SaveAs(path);
    }
}

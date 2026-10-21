using ClosedXML.Excel;

namespace CommCodeVerifier.Core;

public static class MissingImChecker
{
    // В локальную группу входят соседи, структурная оценка которых
    // отстаёт от лучшего найденного совпадения не более чем на 2 п.п.
    private const double LocalGroupScoreWindow = 0.02;

    public const string HeaderArticle = "Номенклатура.Артикул";
    public const string HeaderCode = "Коммерческий код";
    public const string HeaderGroup =
        "Номенклатура.Группа выгрузки ИМ (Общие)";

    // Заголовок «Признак ИМ» из шаблона не нужен: наличие признака
    // определяется по значению предыдущего столбца.
    public const string SourceHeaderArticle = "Номенклатура.Артикул";
    public const string SourceHeaderCode = "Коммерческий код";
    public const string SourceHeaderGroup =
        "Номенклатура.Группа выгрузки ИМ (Общие)";

    public static bool ValidateInputTemplate(
        string sourcePath,
        out string error)
    {
        error = "";

        try
        {
            using var workbook = new XLWorkbook(sourcePath);
            var worksheet = workbook.Worksheet(1);

            var expectedHeaders = new[]
            {
                SourceHeaderArticle,
                SourceHeaderCode,
                SourceHeaderGroup
            };

            for (int column = 0;
                 column < expectedHeaders.Length;
                 column++)
            {
                string actualHeader = worksheet
                    .Cell(1, column + 1)
                    .GetFormattedString()
                    .Trim();

                if (actualHeader.Equals(
                        expectedHeaders[column],
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                error =
                    $"заголовок {column + 1}-го столбца " +
                    $"«{actualHeader}» вместо " +
                    $"«{expectedHeaders[column]}»";

                return false;
            }

            var lastRow = worksheet.LastRowUsed();

            if (lastRow == null || lastRow.RowNumber() < 2)
            {
                error = "в файле отсутствуют строки с данными";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error =
                "файл не удалось открыть (" +
                ex.Message +
                ")";

            return false;
        }
    }

    public static MissingImReport Run(
        string sourcePath,
        MissingImSettings settings,
        IProgress<int>? progress,
        CancellationToken cancellationToken,
        string outputFolder)
    {
        settings ??= new MissingImSettings();
        settings.Normalize();

        var rows = ReadRows(
            sourcePath,
            progress,
            cancellationToken);

        var structures = rows
            .Select(row => new MissingImFamilyItem
            {
                Article = row.Article,
                CommercialCode = row.CommercialCode,
                Group = row.Group,
                HasIm = row.HasIm,
                Structure = MissingImCodeParser.Parse(
                    row.Article,
                    row.CommercialCode)
            })
            .ToList();

        var imConfiguration = ConfigRepository.Instance;

        var imRuleMatches = structures.ToDictionary(
            item => item,
            item => (
                Code: imConfiguration.ImRules.Check(item.CommercialCode),
                Article: imConfiguration.ImArticleRules.Check(item.Article)));

        var itemsByFirstFragment = structures
            .Where(item => !imRuleMatches[item].Code.IsMatch && !imRuleMatches[item].Article.IsMatch)
            .Where(item => !string.IsNullOrWhiteSpace(item.Structure.FirstRequiredFragment))
            .GroupBy(item => item.Structure.FirstRequiredFragment!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var candidates = new List<MissingImCandidate>();
        var similarityDiagnostics =
            new List<MissingImPairDiagnostic>();

        foreach (var candidate in structures.Where(item => !item.HasIm))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ruleMatches = imRuleMatches[candidate];
            var codeRuleMatch = ruleMatches.Code;
            var articleRuleMatch = ruleMatches.Article;

            if (codeRuleMatch.IsMatch || articleRuleMatch.IsMatch)
            {
                if (settings.EnableSimilarityDiagnostics)
                {
                    var matchedRules = new List<string>();

                    if (codeRuleMatch.IsMatch)
                        matchedRules.Add(codeRuleMatch.Description);

                    if (articleRuleMatch.IsMatch)
                        matchedRules.Add(articleRuleMatch.Description);

                    similarityDiagnostics.Add(
                        new MissingImPairDiagnostic
                        {
                            CandidateArticle = candidate.Article,
                            CandidateCode = candidate.CommercialCode,
                            FirstCandidateNumericFragment = GetFirstNumericFragment(candidate.Structure),
                            CandidateDecision =
                                "Отклонён по правилам признака ИМ: " +
                                string.Join("; ", matchedRules)
                        });
                }

                continue;
            }

            string? firstFragment =
                candidate.Structure.FirstRequiredFragment;

            if (string.IsNullOrWhiteSpace(firstFragment) ||
                !itemsByFirstFragment.TryGetValue(
                    firstFragment,
                    out var prefixItems))
            {
                if (settings.EnableSimilarityDiagnostics)
                {
                    similarityDiagnostics.Add(
                        new MissingImPairDiagnostic
                        {
                            CandidateArticle = candidate.Article,
                            CandidateCode = candidate.CommercialCode,
                            CandidateDecision =
                                "Отклонён: не найден обязательный " +
                                "первый фрагмент кода."
                        });
                }

                continue;
            }

            var rankedNeighbors =
                new List<(MissingImFamilyItem Item, double Score)>();

            foreach (var item in prefixItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ReferenceEquals(item, candidate))
                    continue;

                double blockSimilarity =
                    MissingImSimilarity.CompareCodeBlocks(
                        candidate.Structure,
                        item.Structure,
                        settings.PositionDecay);

                rankedNeighbors.Add((item, blockSimilarity));
            }

            var localGroup =
                new List<MissingImFamilyItem> { candidate };

            if (rankedNeighbors.Count > 0)
            {
                double bestStructuralScore =
                    rankedNeighbors.Max(item => item.Score);

                localGroup.AddRange(
                    rankedNeighbors
                        .Where(item =>
                            item.Score >=
                            bestStructuralScore - LocalGroupScoreWindow)
                        .Select(item => item.Item));
            }

            int groupSize = localGroup.Count;
            int positiveCount =
                localGroup.Count(item => item.HasIm);

            double positiveRatio = groupSize == 0
                ? 0.00
                : (double)positiveCount / groupSize;

            var threshold =
                settings.GetThresholdForGroupSize(groupSize);

            bool passesGroupThreshold =
                positiveCount >=
                    threshold.MinimumPositiveReferences &&
                positiveRatio >=
                    threshold.MinimumPositiveRatio;

            string groupFailureReason =
                "Отклонён по локальной группе: положительных " +
                $"референсов {positiveCount}/" +
                $"{threshold.MinimumPositiveReferences}, " +
                $"доля {positiveRatio:P2}/" +
                $"{threshold.MinimumPositiveRatio:P2}.";

            var references = localGroup
                .Where(item => item.HasIm)
                .ToList();

            MissingImCandidate? best = null;
            double bestScore = -1.00;

            var candidatePairDiagnostics =
                new List<MissingImPairDiagnostic>();

            foreach (var reference in references)
            {
                if (!MissingImFirstFragment.AreEqual(
                        candidate.Structure.CommercialCode,
                        reference.Structure.CommercialCode))
                {
                    continue;
                }

                double articleSimilarity = MissingImSimilarity.CompareArticles(
                       candidate.Article,
                       reference.Article,
                       settings.ArticlePositionDecay);

                var similarity =
                    MissingImSimilarity.Compare(
                        candidate.Structure,
                        reference.Structure,
                        articleSimilarity,
                        positiveRatio,
                        settings.Weights,
                        positionDecay: settings.PositionDecay,
                        includeDiagnostics:
                            settings.EnableSimilarityDiagnostics);

                if (settings.EnableSimilarityDiagnostics)
                {
                    candidatePairDiagnostics.Add(
                        new MissingImPairDiagnostic
                        {
                            CandidateArticle = candidate.Article,
                            CandidateCode = candidate.CommercialCode,
                            ReferenceArticle = reference.Article,
                            ReferenceCode = reference.CommercialCode,

                            LocalGroupSize = groupSize,
                            PositiveReferenceCount = positiveCount,
                            MinimumPositiveReferences =
                                threshold.MinimumPositiveReferences,
                            PositiveRatioPercent =
                                positiveRatio * 100.00,
                            MinimumPositiveRatioPercent =
                                threshold.MinimumPositiveRatio * 100.00,

                            NeighborhoodSimilarityPercent =
                                MissingImSimilarity.CompareCodeBlocks(
                                    candidate.Structure,
                                    reference.Structure,
                                    settings.PositionDecay) * 100.00,

                            SimilarityPercent = similarity.ScorePercent,
                            MinimumSimilarityPercent = settings.MinimumCodeSimilarity * 100.00,
                            FirstCandidateNumericFragment = GetFirstNumericFragment(candidate.Structure),
                            FirstReferenceNumericFragment = GetFirstNumericFragment(reference.Structure),
                            FirstNumericFragmentDiffers = FirstNumericFragmentDiffers(candidate.Structure, reference.Structure),

                            PairDecision =
                                similarity.Score >=
                                settings.MinimumCodeSimilarity
                                    ? "Пара проходит порог сходства."
                                    : "Пара ниже порога сходства.",

                            Features = similarity.FeatureDiagnostics,
                            Blocks = similarity.BlockDiagnostics
                        });
                }

                if (similarity.Score <= bestScore)
                    continue;

                bestScore = similarity.Score;

                best = new MissingImCandidate
                {
                    Article = candidate.Article,
                    CommercialCode = candidate.CommercialCode,
                    ReferenceCode = reference.CommercialCode,
                    ReferenceArticle = reference.Article,
                    SimilarityPercent = Math.Round(
                        similarity.Score * 100.0,
                        2),
                    Basis =
                        $"Первый фрагмент «{firstFragment}» совпадает; " +
                        $"локальная группа: {positiveCount} " +
                        $"положительных из {groupSize} кодов " +
                        $"({positiveRatio:P2}); " +
                        $"{similarity.Basis}",
                    Signature = similarity.Signature,
                    MatchedSegments = similarity.MatchedSegments,
                    DifferentSegments = similarity.DifferentSegments,
                    PositiveFamilyCount = positiveCount,
                    NegativeFamilyCount = groupSize - positiveCount,
                    ImPresencePercent = Math.Round(
                        positiveRatio * 100.0,
                        2)
                };
            }

            string candidateDecision;

            if (!passesGroupThreshold)
            {
                candidateDecision = groupFailureReason;
            }
            else if (best == null)
            {
                candidateDecision =
                    "Отклонён: в локальной группе нет " +
                    "положительного референса.";
            }
            else if (best.SimilarityPercent <
                     settings.MinimumCodeSimilarity * 100.0)
            {
                candidateDecision =
                    $"Отклонён: максимальное сходство " +
                    $"{best.SimilarityPercent:F2}% ниже порога " +
                    $"{settings.MinimumCodeSimilarity * 100.0:F2}%.";
            }
            else
            {
                candidateDecision =
                    "Прошёл отбор и добавлен в список кандидатов.";
            }

            foreach (var pairDiagnostic in candidatePairDiagnostics)
            {
                pairDiagnostic.CandidateDecision = candidateDecision;
            }

            if (settings.EnableSimilarityDiagnostics)
            {
                if (candidatePairDiagnostics.Count == 0)
                {
                    similarityDiagnostics.Add(
                        new MissingImPairDiagnostic
                        {
                            CandidateArticle = candidate.Article,
                            CandidateCode = candidate.CommercialCode,
                            LocalGroupSize = groupSize,
                            PositiveReferenceCount = positiveCount,
                            MinimumPositiveReferences =
                                threshold.MinimumPositiveReferences,
                            PositiveRatioPercent =
                                positiveRatio * 100.00,
                            MinimumPositiveRatioPercent =
                                threshold.MinimumPositiveRatio * 100.00,
                            MinimumSimilarityPercent =
                                settings.MinimumCodeSimilarity * 100.00,
                            CandidateDecision = candidateDecision
                        });
                }
                else
                {
                    similarityDiagnostics.AddRange(
                        candidatePairDiagnostics);
                }
            }

            if (!passesGroupThreshold ||
                best == null ||
                best.SimilarityPercent <
                settings.MinimumCodeSimilarity * 100.0)
            {
                continue;
            }

            candidates.Add(best);
        }

        string outputFile = "";

        if (candidates.Count > 0 ||
            settings.EnableSimilarityDiagnostics)
        {
            outputFile = MissingImReportWriter.Write(
                outputFolder,
                candidates,
                settings.EnableSimilarityDiagnostics
                    ? similarityDiagnostics
                    : null);

            if (!File.Exists(outputFile))
            {
                throw new IOException(
                    "Файл результатов не найден после сохранения: " +
                    outputFile);
            }
        }

        var report = new MissingImReport
        {
            OutputFolder = outputFolder,
            OutputFile = outputFile,
            ScannedCount = rows.Count,
            CandidateCount = candidates.Count
        };

        report.Candidates.AddRange(candidates);
        report.SimilarityDiagnostics.AddRange(similarityDiagnostics);

        return report;
    }

    private static string GetFirstNumericFragment(
    MissingImCodeStructure structure) =>
    structure.Fragments
        .FirstOrDefault(fragment =>
            fragment.Type == MissingImFragmentType.Digits)
        ?.Text ?? "";

    private static bool FirstNumericFragmentDiffers(
        MissingImCodeStructure candidate,
        MissingImCodeStructure reference)
    {
        string candidateFragment =
            GetFirstNumericFragment(candidate);

        string referenceFragment =
            GetFirstNumericFragment(reference);

        return candidateFragment.Length > 0 && referenceFragment.Length > 0 && !string.Equals(
                   candidateFragment,
                   referenceFragment,
                   StringComparison.Ordinal);
    }
    private static List<MissingImInputRow> ReadRows(
        string sourcePath,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(sourcePath);
        var worksheet = workbook.Worksheet(1);
        var last = worksheet.LastRowUsed();

        var rows = new List<MissingImInputRow>();

        if (last == null)
            return rows;

        for (int row = 2;
             row <= last.RowNumber();
             row++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string article = worksheet
                .Cell(row, 1)
                .GetFormattedString()
                .Trim();

            string commercialCode = worksheet
                .Cell(row, 2)
                .GetFormattedString()
                .Trim();

            string group = worksheet
                .Cell(row, 3)
                .GetFormattedString()
                .Trim();

            bool hasIm = group.Length > 0;

            if (article.Length == 0 &&
                commercialCode.Length == 0)
            {
                continue;
            }

            // Непустая группа выгрузки ИМ означает, что признак ИМ установлен.
            // Пустая группа означает отсутствие признака.
            rows.Add(
                new MissingImInputRow
                {
                    Article = article,
                    CommercialCode = commercialCode,
                    Group = group,
                    HasIm = hasIm
                });

            progress?.Report(
                last.RowNumber() <= 1
                    ? 100
                    : (row - 1) * 100 /
                      (last.RowNumber() - 1));
        }

        return rows;
    }

    private sealed class MissingImInputRow
    {
        public string Article { get; init; } = "";

        public string CommercialCode { get; init; } = "";

        public string Group { get; init; } = "";

        public bool HasIm { get; init; }
    }
}

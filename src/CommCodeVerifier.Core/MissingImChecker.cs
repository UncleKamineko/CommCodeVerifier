using ClosedXML.Excel;

namespace CommCodeVerifier.Core;

public static class MissingImChecker
{
    public static bool ValidateInputTemplate(
    string sourcePath,
    out string error)
    {
        error = "";

        try
        {
            using var workbook =
                new XLWorkbook(sourcePath);

            var worksheet =
                workbook.Worksheet(1);

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
                string actualHeader =
                    worksheet.Cell(1, column + 1)
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

            var lastRow =
                worksheet.LastRowUsed();

            if (lastRow == null ||
                lastRow.RowNumber() < 2)
            {
                error =
                    "в файле отсутствуют строки с данными";

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
    public const string HeaderArticle = "Номенклатура.Артикул";
    public const string HeaderCode = "Коммерческий код";
    public const string HeaderGroup = "Номенклатура.Группа выгрузки ИМ (Общие)";
    // public const string HeaderIm = "Признак ИМ"; - лишний заголовок, признак ИМ фактически расположен в предыдущем столбце
    public const string SourceHeaderArticle = "Номенклатура.Артикул";
    public const string SourceHeaderCode = "Коммерческий код";
    public const string SourceHeaderGroup = "Номенклатура.Группа выгрузки ИМ (Общие)";
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

        var families =
            MissingImFamilyAnalyzer.BuildFamilies(
                structures);

        var candidates = new List<MissingImCandidate>();

        foreach (var family in families)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (family.PositiveCount <
                settings.MinimumPositiveReferences)
            {
                continue;
            }

            if (family.ImPresenceRatio <
                settings.MinimumImPresenceInFamily)
            {
                continue;
            }

            var references = family.Items
                .Where(item => item.HasIm)
                .ToList();
            
            var missing = family.Items
                .Where(item => !item.HasIm)
                .ToList();

            foreach (var candidate in missing)
            {
                if (string.IsNullOrWhiteSpace(
                   candidate.Structure.FirstRequiredFragment))
                {
                    continue;
                }
                cancellationToken.ThrowIfCancellationRequested();

                MissingImCandidate? best = null;
                double bestScore = -1.00;

                foreach (var reference in references)
                {
                    if (!MissingImFirstFragment.AreEqual(
                       candidate.Structure.CommercialCode,
                       reference.Structure.CommercialCode))
                    {
                        continue;
                    }
                    double articleSimilarity =
                        MissingImSimilarity.CompareArticles(
                            candidate.Article,
                            reference.Article);

                    var similarity =
                        MissingImSimilarity.Compare(
                            candidate.Structure,
                            reference.Structure,
                            articleSimilarity,
                            family.ImPresenceRatio,
                            settings.Weights);

                    if (similarity.Score <= bestScore)
                        continue;

                    bestScore = similarity.Score;

                    best = new MissingImCandidate
                    {
                        Article = candidate.Article,
                        CommercialCode =
                            candidate.CommercialCode,
                        ReferenceCode =
                            reference.CommercialCode,
                        ReferenceArticle =
                            reference.Article,
                        SimilarityPercent =
                            Math.Round(
                                similarity.Score * 100.0,
                                2),
                        Basis =
                            $"Первый фрагмент " +
                            $"«{candidate.Structure.FirstRequiredFragment}» " +
                            $"совпадает с референсом; " +
                            $"{similarity.Basis}; " +
                            $"признак установлен у " +
                            $"{family.PositiveCount} из " +
                            $"{family.TotalCount} кодов семейства",
                        Signature =
                            similarity.Signature,
                        MatchedSegments =
                            similarity.MatchedSegments,
                        DifferentSegments =
                            similarity.DifferentSegments,
                        PositiveFamilyCount =
                            family.PositiveCount,
                        NegativeFamilyCount =
                            family.NegativeCount,
                        ImPresencePercent =
                            Math.Round(
                                family.ImPresenceRatio * 100.0,
                                2)
                    };
                }

                if (best == null)
                    continue;

                if (best.SimilarityPercent <
                    settings.MinimumCodeSimilarity * 100.0)
                {
                    continue;
                }

                candidates.Add(best);
            }
        }

        string outputFile = "";

        if (candidates.Count > 0)
        {
            outputFile =
                MissingImReportWriter.Write(
                    outputFolder,
                    candidates);
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

        return report;
    }

    private static List<MissingImInputRow> ReadRows(
        string sourcePath,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        using var workbook =
            new XLWorkbook(sourcePath);

        var worksheet =
            workbook.Worksheet(1);

        var last =
            worksheet.LastRowUsed();

        var rows = new List<MissingImInputRow>();

        if (last == null)
            return rows;

        for (int row = 2;
             row <= last.RowNumber();
             row++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string article =
                worksheet.Cell(row, 1)
                    .GetFormattedString()
                    .Trim();

            string commercialCode =
                worksheet.Cell(row, 2)
                    .GetFormattedString()
                    .Trim();

            string group =
                worksheet.Cell(row, 3)
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

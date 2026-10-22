using ClosedXML.Excel;

namespace CommCodeVerifier.Core;

public static class MissingImReportWriter
{
    public const string BaseFileName = "Коды для простановки признака ИМ.xlsx";
    private static string CreateFileName()
    {
        string prefix =
            DateTime.Now.ToString(
                "dd.MM.yyyy_HH.mm",
                System.Globalization.CultureInfo.InvariantCulture);

        return
            $"{prefix}_{BaseFileName}.xlsx";
    }
    public static string Write(
        string outputFolder,
        IReadOnlyCollection<MissingImCandidate> candidates,
        IReadOnlyCollection<MissingImPairDiagnostic>? diagnostics = null)
    {
        Directory.CreateDirectory(outputFolder);
        string FileName = CreateFileName();
        string path = Path.Combine(outputFolder, FileName);

        if (File.Exists(path))
        {
            string nameWithoutExtension =
                Path.GetFileNameWithoutExtension(path);

            string extension =
                Path.GetExtension(path);

            int suffix = 2;

            string candidatePath;

            do
            {
                candidatePath = Path.Combine(
                    outputFolder,
                    $"{nameWithoutExtension}_{suffix}{extension}");

                suffix++;
            }
            while (File.Exists(candidatePath));

            path = candidatePath;
        }
        using var workbook = new XLWorkbook();

        WriteCandidatesSheet(workbook, candidates);

        if (diagnostics != null)
        {
            WriteDetailsSheet(workbook, candidates);
            WriteSimilarityDiagnosticsSheet(workbook, diagnostics);
        }

        workbook.SaveAs(path);
            if (!File.Exists(path))
            {
            throw new IOException(
            "Не удалось сохранить XLSX-файл результатов: " +
            path);
            }

        return path;
    }

    private static void WriteCandidatesSheet(
        XLWorkbook workbook,
        IReadOnlyCollection<MissingImCandidate> candidates)
    {
        var worksheet = workbook.AddWorksheet("Кандидаты");

        worksheet.Cell(1, 1).Value = "Артикул";
        worksheet.Cell(1, 2).Value = "Коммерческий код";
        worksheet.Cell(1, 3).Value = "Группа выгрузки ИМ";
        worksheet.Cell(1, 4).Value = "Референс-код";
        worksheet.Cell(1, 5).Value = "Сходство, %";
        worksheet.Cell(1, 6).Value = "Основание";

        worksheet.Row(1).Style.Font.Bold = true;

        var ordered = candidates
            .OrderByDescending(item => item.SimilarityPercent)
            .ThenByDescending(item => item.ImPresencePercent)
            .ThenByDescending(item => item.PositiveFamilyCount)
            .ThenBy(item => item.Article)
            .ThenBy(item => item.CommercialCode)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            int row = i + 2;

            worksheet.Cell(row, 1).Value = item.Article;
            worksheet.Cell(row, 2).Value = item.CommercialCode;

            // Столбец предназначен для ручного заполнения.
            worksheet.Cell(row, 3).Value = "";

            worksheet.Cell(row, 4).Value = item.ReferenceCode;
            worksheet.Cell(row, 5).Value = item.SimilarityPercent;
            worksheet.Cell(row, 6).Value = item.Basis;
        }

        FormatWorksheet(
            worksheet,
            ordered.Count + 1,
            6);

        worksheet.Column(1).Width = 28;
        worksheet.Column(2).Width = 34;
        worksheet.Column(3).Width = 24;
        worksheet.Column(4).Width = 34;
        worksheet.Column(5).Width = 16;
        worksheet.Column(6).Width = 100;

        worksheet.Column(5).Style.NumberFormat.Format =
            "0.00";

        worksheet.Column(6).Style.Alignment.WrapText = true;
    }

    private static void WriteDetailsSheet(
        XLWorkbook workbook,
        IReadOnlyCollection<MissingImCandidate> candidates)
    {
        var worksheet = workbook.AddWorksheet("Расшифровка");

        string[] headers =
        {
            "Коммерческий код",
            "Референс-код",
            "Тип несоответствия",
            "Оценка сходства, %",
            "Общая сигнатура",
            "Совпавшие сегменты",
            "Различающиеся сегменты",
            "Положительных кодов в локальной группе",
            "Кандидат и отрицательные коды в локальной группе",
            "Доля положительных кодов в локальной группе, %",
            "Артикул",
            "Артикул референса"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        worksheet.Row(1).Style.Font.Bold = true;

        var ordered = candidates
            .OrderByDescending(item => item.SimilarityPercent)
            .ThenBy(item => item.CommercialCode)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            int row = i + 2;

            worksheet.Cell(row, 1).Value = item.CommercialCode;
            worksheet.Cell(row, 2).Value = item.ReferenceCode;
            worksheet.Cell(row, 3).Value =
                "Возможное отсутствие признака ИМ";
            worksheet.Cell(row, 4).Value =
                item.SimilarityPercent;
            worksheet.Cell(row, 5).Value = item.Signature;
            worksheet.Cell(row, 6).Value = item.MatchedSegments;
            worksheet.Cell(row, 7).Value = item.DifferentSegments;
            worksheet.Cell(row, 8).Value =
                item.PositiveFamilyCount;
            worksheet.Cell(row, 9).Value =
                item.NegativeFamilyCount;
            worksheet.Cell(row, 10).Value =
                item.ImPresencePercent;
            worksheet.Cell(row, 11).Value = item.Article;
            worksheet.Cell(row, 12).Value =
                item.ReferenceArticle;
        }

        FormatWorksheet(
            worksheet,
            ordered.Count + 1,
            headers.Length);

        worksheet.Column(4).Style.NumberFormat.Format =
            "0.00";

        worksheet.Column(10).Style.NumberFormat.Format =
            "0.00";

        worksheet.Column(5).Width = 34;
        worksheet.Column(6).Width = 55;
        worksheet.Column(7).Width = 55;
        worksheet.Column(11).Width = 28;
        worksheet.Column(12).Width = 28;

        worksheet.Range(
                1,
                1,
                Math.Max(2, ordered.Count + 1),
                headers.Length)
            .Style.Alignment.WrapText = true;
    }
    private static void WriteSimilarityDiagnosticsSheet(
    XLWorkbook workbook,
    IReadOnlyCollection<MissingImPairDiagnostic> diagnostics)
    {
        var worksheet =
            workbook.AddWorksheet("Диагностика сходства");

        string[] headers =
        {
        "Тип строки",
        "Артикул кандидата",
        "Код кандидата",
        "Артикул референса",
        "Код референса",
        "Размер локальной группы",
        "Положительных референсов",
        "Минимум положительных референсов",
        "Доля положительных, %",
        "Минимальная доля, %",
        "Структурная близость локальной группы, %",
        "Сходство пары, %",
        "Порог сходства, %",
        "Решение по паре",
        "Решение по кандидату",
        "Характеристика",
        "Оценка характеристики, %",
        "Вес характеристики",
        "Доля веса, %",
        "Вклад в итог, п.п.",
        "Эффект включения (п.п.; + повышает, − снижает)",
        "Позиция после префикса",
        "Блок кандидата",
        "Блок референса",
        "Тип блока кандидата",
        "Тип блока референса",
        "Сходство блоков, %",
        "Позиционный коэффициент",
        "Первый числовой фрагмент кандидата",
        "Первый числовой фрагмент референса",
        "Сходство артикулов, %",
        "Порог сходства артикулов, %"
    };

        for (int column = 0; column < headers.Length; column++)
            worksheet.Cell(1, column + 1).Value = headers[column];

        worksheet.Row(1).Style.Font.Bold = true;

        int row = 2;

        foreach (var diagnostic in diagnostics
                     .OrderBy(
                         item => item.CandidateCode,
                         StringComparer.Ordinal)
                     .ThenBy(
                         item => item.ReferenceCode,
                         StringComparer.Ordinal))
        {
            WriteDiagnosticBaseRow(
                worksheet,
                row++,
                "Итог пары",
                diagnostic);

            foreach (var feature in diagnostic.Features)
            {
                int featureRow = row++;

                WriteDiagnosticBaseRow(
                    worksheet,
                    featureRow,
                    "Характеристика",
                    diagnostic);

                worksheet.Cell(featureRow, 16).Value = feature.Name;
                if (feature.IsApplicable)
                {
                    worksheet.Cell(featureRow, 17).Value =
                        feature.Similarity * 100.00;
                }
                else
                {
                    worksheet.Cell(featureRow, 17).Value = "Н/П";
                }
                worksheet.Cell(featureRow, 18).Value = feature.Weight;
                worksheet.Cell(featureRow, 19).Value =
                    feature.WeightSharePercent;
                worksheet.Cell(featureRow, 20).Value =
                    feature.ContributionPercent;
                worksheet.Cell(featureRow, 21).Value =
                    feature.EffectOnScorePercentagePoints;
            }

            foreach (var block in diagnostic.Blocks)
            {
                int blockRow = row++;

                WriteDiagnosticBaseRow(
                    worksheet,
                    blockRow,
                    "Блок",
                    diagnostic);

                worksheet.Cell(blockRow, 22).Value =
                    block.PositionAfterRequiredFragment;
                worksheet.Cell(blockRow, 23).Value =
                    block.CandidateBlock;
                worksheet.Cell(blockRow, 24).Value =
                    block.ReferenceBlock;
                worksheet.Cell(blockRow, 25).Value =
                    block.CandidateType;
                worksheet.Cell(blockRow, 26).Value =
                    block.ReferenceType;
                worksheet.Cell(blockRow, 27).Value =
                    block.BlockSimilarity * 100.00;
                worksheet.Cell(blockRow, 28).Value =
                    block.PositionWeight;
                worksheet.Cell(row, 29).Value =
                    diagnostic.FirstCandidateNumericFragment;
                worksheet.Cell(row, 30).Value =
                    diagnostic.FirstReferenceNumericFragment;
                worksheet.Cell(row, 31).Value =
                    diagnostic.ArticleSimilarityPercent;
                worksheet.Cell(row, 32).Value =
                    diagnostic.MinimumArticleSimilarityPercent;

                if (diagnostic.FirstNumericFragmentDiffers)
                {
                    worksheet.Range(row, 1, row, 30)
                        .Style.Fill.BackgroundColor = XLColor.Yellow;
                }
            }
        }

        FormatWorksheet(
            worksheet,
            Math.Max(1, row - 1),
            headers.Length);

        worksheet.Column(1).Width = 18;
        worksheet.Column(2).Width = 28;
        worksheet.Column(3).Width = 28;
        worksheet.Column(4).Width = 28;
        worksheet.Column(5).Width = 28;
        worksheet.Column(11).Width = 20;
        worksheet.Column(14).Width = 30;
        worksheet.Column(15).Width = 48;
        worksheet.Column(16).Width = 42;
        worksheet.Column(23).Width = 22;
        worksheet.Column(24).Width = 22;
        worksheet.Column(31).Width = 24;
        worksheet.Column(32).Width = 28;

        foreach (int column in new[]
                 {
                 9, 10, 11, 12, 13, 17, 19, 20, 21, 27, 31, 32
             })
        {
            worksheet.Column(column)
                .Style.NumberFormat.Format = "0.00";
        }

        foreach (int column in new[] { 18, 28 })
        {
            worksheet.Column(column)
                .Style.NumberFormat.Format = "0.0000";
        }

        worksheet.Range(
                1,
                1,
                Math.Max(2, row - 1),
                headers.Length)
            .Style.Alignment.WrapText = true;
    }

    private static void WriteDiagnosticBaseRow(
    IXLWorksheet worksheet,
    int row,
    string rowType,
    MissingImPairDiagnostic diagnostic)
    {
        worksheet.Cell(row, 1).Value = rowType;
        worksheet.Cell(row, 2).Value = diagnostic.CandidateArticle;
        worksheet.Cell(row, 3).Value = diagnostic.CandidateCode;
        worksheet.Cell(row, 4).Value = diagnostic.ReferenceArticle;
        worksheet.Cell(row, 5).Value = diagnostic.ReferenceCode;
        worksheet.Cell(row, 6).Value = diagnostic.LocalGroupSize;
        worksheet.Cell(row, 7).Value = diagnostic.PositiveReferenceCount;
        worksheet.Cell(row, 8).Value =
            diagnostic.MinimumPositiveReferences;
        worksheet.Cell(row, 9).Value =
            diagnostic.PositiveRatioPercent;
        worksheet.Cell(row, 10).Value =
            diagnostic.MinimumPositiveRatioPercent;
        worksheet.Cell(row, 11).Value =
            diagnostic.NeighborhoodSimilarityPercent;
        worksheet.Cell(row, 12).Value =
            diagnostic.SimilarityPercent;
        worksheet.Cell(row, 13).Value =
            diagnostic.MinimumSimilarityPercent;
        worksheet.Cell(row, 14).Value =
            diagnostic.PairDecision;
        worksheet.Cell(row, 15).Value =
            diagnostic.CandidateDecision;

        // Колонки 29 и 30 уже есть в заголовках листа диагностики.
        worksheet.Cell(row, 29).Value =
            diagnostic.FirstCandidateNumericFragment;
        worksheet.Cell(row, 30).Value =
            diagnostic.FirstReferenceNumericFragment;

        if (diagnostic.FirstNumericFragmentDiffers)
        {
            worksheet.Range(row, 1, row, 32)
                .Style.Fill.BackgroundColor = XLColor.Yellow;
        }
    }
    private static void FormatWorksheet(
        IXLWorksheet worksheet,
        int lastRow,
        int lastColumn)
    {
        worksheet.SheetView.FreezeRows(1);

        worksheet.Range(
                1,
                1,
                Math.Max(2, lastRow),
                lastColumn)
            .SetAutoFilter();

        worksheet.Row(1).Style.Alignment.WrapText = true;
    }
}

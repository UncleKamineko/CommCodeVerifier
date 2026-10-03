using ClosedXML.Excel;

namespace CommCodeVerifier.Core;

public sealed record ImRow(string Article, string Code, string Group, string SourceFile);

/// <summary>Строка вместе с результатами проверки по обоим наборам правил.</summary>
public sealed record ImChecked(ImRow Row, ImMatch CodeMatch, ImMatch ArticleMatch)
{
    public bool IsWrong => CodeMatch.IsMatch || ArticleMatch.IsMatch;

    public string Description
    {
        get
        {
            var parts = new List<string>();
            if (CodeMatch.IsMatch) parts.Add(CodeMatch.Description);
            if (ArticleMatch.IsMatch) parts.Add(ArticleMatch.Description);
            return string.Join("; ", parts);
        }
    }
}

public sealed class ImReport
{
    public string OutputFolder { get; set; } = "";
    public string ScannedTarget { get; set; } = "";
    public List<string> ScannedFiles { get; } = new();
    public int TotalRows { get; set; }
    public int SkippedNoGroup { get; set; }
    public int Correct { get; set; }
    public int Wrong { get; set; }
    public int WrongByCode { get; set; }
    public int WrongByArticle { get; set; }
    public int CodeRulesCount { get; set; }
    public int ArticleRulesCount { get; set; }
    public List<string> Warnings { get; } = new();
}

public static class ImChecker
{
    public const string FileCorrect = "Корректный признак ИМ.xlsx";
    public const string FileWrong = "Ошибочный признак ИМ.xlsx";

    /// <summary>Подпапка для результатов варианта 2.</summary>
    public const string SingleRunSubfolder = "Анализ признака ИМ";

    /// <summary>Служебные значения столбца «исправленный» — это не коды.</summary>
    private static readonly string[] ServiceValues =
        { "Пустое значение", "Обработка не выполнена" };

    public static string ResultsRoot => AppPaths.ResultsFolder;

    // ================= ВАРИАНТ 2: отдельный файл =================
    public const string HeaderArticle = "Номенклатура.Артикул";
    public const string HeaderCode = "Номенклатура.Коммерческий код (Общие)";

    public static bool ValidateSingleTemplate(string path, out string error)
    {
        error = "";
        try
        {
            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet(1);

            string h1 = (ws.Cell(1, 1).GetFormattedString() ?? "").Trim();
            string h2 = (ws.Cell(1, 2).GetFormattedString() ?? "").Trim();

            if (h1.Length == 0 && h2.Length == 0)
            {
                error = "первая строка файла пуста, заголовки столбцов не найдены";
                return false;
            }
            if (!h1.Equals(HeaderArticle, StringComparison.OrdinalIgnoreCase))
            {
                error = $"заголовок 1-го столбца «{h1}» вместо «{HeaderArticle}»";
                return false;
            }
            if (!h2.Equals(HeaderCode, StringComparison.OrdinalIgnoreCase))
            {
                error = $"заголовок 2-го столбца «{h2}» вместо «{HeaderCode}»";
                return false;
            }

            var last = ws.LastRowUsed();
            if (last == null || last.RowNumber() < 2)
            {
                error = "в файле нет строк с данными";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = "файл не удалось открыть (" + ex.Message + ")";
            return false;
        }
    }

    public static ImReport RunSingleFile(string sourcePath, ImRuleSet codeRules, ImRuleSet articleRules,
                                         IProgress<int>? progress, CancellationToken ct)
    {
        var rows = new List<ImRow>();
        string name = Path.GetFileName(sourcePath);

        using (var wb = new XLWorkbook(sourcePath))
        {
            var ws = wb.Worksheet(1);
            var last = ws.LastRowUsed();
            if (last != null)
                for (int r = 2; r <= last.RowNumber(); r++)
                {
                    string a = (ws.Cell(r, 1).GetFormattedString() ?? "").Trim();
                    string c = (ws.Cell(r, 2).GetFormattedString() ?? "").Trim();
                    if (a.Length == 0 && c.Length == 0) continue;
                    rows.Add(new ImRow(a, c, "", name));
                }
        }

        var report = new ImReport { OutputFolder = CreateSingleRunFolder(), ScannedTarget = name };
        report.ScannedFiles.Add(name);

        Analyze(rows, codeRules, articleRules, report, progress, ct);
        return report;
    }

    private static string CreateSingleRunFolder()
    {
        string root = Path.Combine(ResultsRoot, SingleRunSubfolder);
        string name = DateTime.Now.ToString("dd.MM.yyyy_HH.mm");
        string path = Path.Combine(root, name);
        int n = 2;
        while (Directory.Exists(path)) path = Path.Combine(root, $"{name}_{n++}");
        Directory.CreateDirectory(path);
        return path;
    }

    // ================= ВАРИАНТ 1: папка результатов =================
    public static string? FindLatestResultsFolder()
    {
        if (!Directory.Exists(ResultsRoot)) return null;

        return new DirectoryInfo(ResultsRoot).GetDirectories()
            .Where(d => !string.Equals(d.Name, SingleRunSubfolder, StringComparison.OrdinalIgnoreCase))
            .Where(HasResultFiles)
            .OrderByDescending(d => d.CreationTime)
            .FirstOrDefault()?.FullName;
    }

    private static bool HasResultFiles(DirectoryInfo d) =>
        File.Exists(Path.Combine(d.FullName, BatchProcessor.File1C)) ||
        File.Exists(Path.Combine(d.FullName, BatchProcessor.FileManual)) ||
        File.Exists(Path.Combine(d.FullName, BatchProcessor.FileNoChange));

    public static bool ValidateResultsFolder(string folder, out string error)
    {
        error = "";
        if (!Directory.Exists(folder)) { error = "папка не найдена"; return false; }
        if (!HasResultFiles(new DirectoryInfo(folder)))
        {
            error = "в папке нет файлов результатов групповой обработки " +
                    $"(«{BatchProcessor.File1C}», «{BatchProcessor.FileManual}», " +
                    $"«{BatchProcessor.FileNoChange}»)";
            return false;
        }
        return true;
    }

    public static ImReport RunResultsFolder(string folder, ImRuleSet codeRules, ImRuleSet articleRules,
                                            IProgress<int>? progress, CancellationToken ct)
    {
        var report = new ImReport { OutputFolder = folder, ScannedTarget = folder };
        var rows = new List<ImRow>();

        // Код — столбец 2, группа выгрузки — столбец 4.
        ReadResultFile(Path.Combine(folder, BatchProcessor.File1C), 2, 4, rows, report);
        ReadResultFile(Path.Combine(folder, BatchProcessor.FileNoChange), 2, 4, rows, report);

        // В файле ручной проверки анализируется ИСПРАВЛЕННЫЙ код (столбец 3), группа — столбец 6.
        ReadResultFile(Path.Combine(folder, BatchProcessor.FileManual), 3, 6, rows, report);

        Analyze(rows, codeRules, articleRules, report, progress, ct);
        return report;
    }

    private static void ReadResultFile(string path, int codeColumn, int groupColumn,
                                       List<ImRow> rows, ImReport report)
    {
        if (!File.Exists(path))
        {
            report.Warnings.Add($"Файл «{Path.GetFileName(path)}» в папке отсутствует — пропущен.");
            return;
        }

        string name = Path.GetFileName(path);
        try
        {
            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet(1);
            var last = ws.LastRowUsed();
            if (last == null) return;

            report.ScannedFiles.Add(name);

            for (int r = 2; r <= last.RowNumber(); r++)
            {
                string article = (ws.Cell(r, 1).GetFormattedString() ?? "").Trim();
                string code = (ws.Cell(r, codeColumn).GetFormattedString() ?? "").Trim();
                string group = (ws.Cell(r, groupColumn).GetFormattedString() ?? "").Trim();

                if (article.Length == 0 && code.Length == 0) continue;

                // Требование ТЗ: анализируются только строки с непустой группой выгрузки.
                if (group.Length == 0) { report.SkippedNoGroup++; continue; }

                // Служебные пометки вместо кода анализировать бессмысленно.
                if (code.Length == 0 ||
                    ServiceValues.Contains(code, StringComparer.OrdinalIgnoreCase))
                {
                    report.SkippedNoGroup++;
                    continue;
                }

                rows.Add(new ImRow(article, code, group, name));
            }
        }
        catch (Exception ex)
        {
            report.Warnings.Add($"Файл «{name}» прочитать не удалось: {ex.Message}");
        }
    }

    // ================= общий анализ и запись =================
    private static void Analyze(List<ImRow> rows, ImRuleSet codeRules, ImRuleSet articleRules,
                                ImReport report, IProgress<int>? progress, CancellationToken ct)
    {
        report.TotalRows = rows.Count;
        report.CodeRulesCount = codeRules.Rules.Count;
        report.ArticleRulesCount = articleRules.Rules.Count;
        report.Warnings.AddRange(codeRules.Warnings);
        report.Warnings.AddRange(articleRules.Warnings);

        var correct = new List<ImChecked>();
        var wrong = new List<ImChecked>();

        for (int i = 0; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var item = new ImChecked(rows[i],
                codeRules.Check(rows[i].Code),
                articleRules.Check(rows[i].Article));

            if (item.IsWrong)
            {
                wrong.Add(item);
                if (item.CodeMatch.IsMatch) report.WrongByCode++;
                if (item.ArticleMatch.IsMatch) report.WrongByArticle++;
            }
            else correct.Add(item);

            if (progress != null && (i % 25 == 0 || i == rows.Count - 1))
                progress.Report(rows.Count == 0 ? 100 : (i + 1) * 100 / rows.Count);
        }

        report.Correct = correct.Count;
        report.Wrong = wrong.Count;

        Write(Path.Combine(report.OutputFolder, FileCorrect), correct, highlight: false);
        Write(Path.Combine(report.OutputFolder, FileWrong), wrong, highlight: true);
    }

    private static void Write(string path, List<ImChecked> data, bool highlight)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Данные");
        ws.Cell(1, 1).Value = "Артикул";
        ws.Cell(1, 2).Value = "Коммерческий код";
        ws.Row(1).Style.Font.Bold = true;
        ws.Columns(1, 2).Style.NumberFormat.Format = "@";

        for (int i = 0; i < data.Count; i++)
        {
            var item = data[i];
            int row = i + 2;

            // Подсвечиваются символы в том столбце, правило которого сработало.
            if (highlight && item.ArticleMatch.Chars.Count > 0)
                SetWithHighlight(ws.Cell(row, 1), item.Row.Article, item.ArticleMatch.Chars);
            else
                ws.Cell(row, 1).Value = item.Row.Article;

            if (highlight && item.CodeMatch.Chars.Count > 0)
                SetWithHighlight(ws.Cell(row, 2), item.Row.Code, item.CodeMatch.Chars);
            else
                ws.Cell(row, 2).Value = item.Row.Code;
        }

        int lastRow = Math.Max(1, data.Count + 1);
        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, lastRow, 2).SetAutoFilter();
        ws.Range(1, 1, lastRow, 2).Style.Alignment.WrapText = true;
        ws.Column(1).Width = 26;
        ws.Column(2).Width = 44;

        if (highlight && data.Count > 0) WriteDetailsSheet(wb, data);
        wb.SaveAs(path);
    }

    /// <summary>Расшифровка: по какому правилу строка признана ошибочной.</summary>
    private static void WriteDetailsSheet(XLWorkbook wb, List<ImChecked> data)
    {
        var ws = wb.AddWorksheet("Причины");
        ws.Cell(1, 1).Value = "Артикул";
        ws.Cell(1, 2).Value = "Коммерческий код";
        ws.Cell(1, 3).Value = "Источник несоответствия";
        ws.Cell(1, 4).Value = "Сработавшие правила";
        ws.Cell(1, 5).Value = "Файл-источник";
        ws.Row(1).Style.Font.Bold = true;

        for (int i = 0; i < data.Count; i++)
        {
            var item = data[i];
            int row = i + 2;

            ws.Cell(row, 1).Value = item.Row.Article;
            ws.Cell(row, 2).Value = item.Row.Code;
            ws.Cell(row, 3).Value =
                item.CodeMatch.IsMatch && item.ArticleMatch.IsMatch ? "комм. код и артикул"
                : item.CodeMatch.IsMatch ? "комм. код"
                : "артикул";
            ws.Cell(row, 4).Value = item.Description;
            ws.Cell(row, 5).Value = item.Row.SourceFile;
        }

        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, data.Count + 1, 5).SetAutoFilter();
        ws.Column(1).Width = 26; ws.Column(2).Width = 38;
        ws.Column(3).Width = 22; ws.Column(4).Width = 50; ws.Column(5).Width = 30;
        ws.Column(4).Style.Alignment.WrapText = true;
    }

    private static void SetWithHighlight(IXLCell cell, string text, HashSet<int> marks)
    {
        if (text.Length == 0 || marks.Count == 0) { cell.Value = text; return; }

        // cell.Value не присваиваем: CreateRichText() засеял бы первый run
        // текущим значением и текст продублировался бы.
        cell.Clear(XLClearOptions.Contents);

        var rt = cell.CreateRichText();
        rt.ClearText();

        foreach (var (start, len, marked) in CodeProcessor.Segments(text, marks))
        {
            var run = rt.AddText(text.Substring(start, len));
            if (marked) { run.SetFontColor(XLColor.Red); run.SetBold(true); }
        }
    }
}

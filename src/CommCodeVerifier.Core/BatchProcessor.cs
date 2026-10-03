using ClosedXML.Excel;

namespace CommCodeVerifier.Core;

public sealed record SourceRow(string Article, string Code, string Group);

public sealed class BatchReport
{
    public string SourceFileName { get; set; } = "";
    public string OutputFolder { get; set; } = "";
    public int TotalRows { get; set; }
    public int Count1C { get; set; }
    public int CountManual { get; set; }
    public int CountUnchanged { get; set; }
    public int Failures { get; set; }

    /// Строк с одинаковым комм. кодом при разных артикулах среди кодов для 1С.
    /// В файл для 1С они не попадают — только в «Дубликаты в кодах для переноса в 1С.xlsx».
    public int Duplicates1C { get; set; }

    /// <summary>То же для файла ручной проверки (по исправленному коду).</summary>
    public int DuplicatesManual { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.Now;
    public List<RuleId> EnabledRules { get; set; } = new();
    public string? ReportWriteError { get; set; }

    public Dictionary<RuleId, int> RuleCounts { get; } =
        RuleCatalog.DisplayOrder.ToDictionary(r => r, _ => 0);

    /// <summary>Имена выходных файлов, в которых найдены дубликаты.</summary>
    /// Имена выходных файлов, для которых найдены дубликаты.
    public IEnumerable<string> FilesWithDuplicates
    {
        get
        {
            if (Duplicates1C > 0) yield return BatchProcessor.File1C;
            if (DuplicatesManual > 0) yield return BatchProcessor.FileManual;
        }
    }
}

public static class BatchProcessor
{
    public const string File1C = "Коды для загрузки в 1С.xlsx";
    public const string FileManual = "Коды для ручной проверки.xlsx";
    public const string FileNoChange = "Комм. код не был изменён.xlsx";
    public const string FileDuplicates1C = "Дубликаты в кодах для переноса в 1С.xlsx";
    public const string SrcHeaderArticle = "Номенклатура.Артикул";
    public const string SrcHeaderCode = "Номенклатура.Коммерческий код (Общие)";
    public const string SrcHeaderGroup = "Номенклатура.Группа выгрузки ИМ (Общие)";
    public const string OutHeaderGroup = "Группа выгрузки ИМ";
    /// Текст предупреждения о дубликатах — для вкладки и для файла сводки.
    public static string DuplicateWarning(string file) => file == File1C
        ? $"ВНИМАНИЕ!!! При формировании файла {File1C} обнаружены дубликаты комм. кодов " +
          $"для разных артикулов, проверьте их в файле \"{FileDuplicates1C}\"!"
        : $"ВНИМАНИЕ!!! В файле {file} обнаружены дубликаты комм. кодов для разных артикулов, проверьте!";
    private static readonly XLColor Yellow = XLColor.FromArgb(255, 255, 153);
    private static readonly XLColor Orange = XLColor.FromArgb(255, 204, 153);
    private static readonly XLColor Red = XLColor.FromArgb(255, 153, 153);
    private static readonly XLColor Violet = XLColor.FromArgb(226, 204, 255);
    private static readonly XLColor Blue = XLColor.FromArgb(141, 180, 226);

    // ---------------- проверка шаблона + чтение ----------------
    public static bool ValidateTemplate(string path, out string error)
    {
        error = "";
        try
        {
            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet(1);

            var expected = new[] { SrcHeaderArticle, SrcHeaderCode, SrcHeaderGroup };
            for (int c = 0; c < expected.Length; c++)
            {
                string actual = (ws.Cell(1, c + 1).GetFormattedString() ?? "").Trim();
                if (!actual.Equals(expected[c], StringComparison.OrdinalIgnoreCase))
                {
                    error = $"заголовок {c + 1}-го столбца «{actual}» вместо «{expected[c]}»";
                    return false;
                }
            }
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    public static List<SourceRow> Read(string path)
    {
        var rows = new List<SourceRow>();
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet(1);
        var last = ws.LastRowUsed();
        if (last == null) return rows;

        for (int r = 2; r <= last.RowNumber(); r++)
        {
            string a = (ws.Cell(r, 1).GetFormattedString() ?? "").Trim();
            string c = ws.Cell(r, 2).GetFormattedString() ?? "";
            string g = (ws.Cell(r, 3).GetFormattedString() ?? "").Trim();
            if (a.Length == 0 && c.Trim().Length == 0) continue;
            rows.Add(new SourceRow(a, c, g));
        }
        return rows;
    }

    // ---------------- основной прогон ----------------
    public static BatchReport Run(string sourcePath, ProcessOptions opts,
        IProgress<int>? progress, CancellationToken ct, string? outputRoot = null)
    {
        var rows = Read(sourcePath);
        var report = new BatchReport
        {
            SourceFileName = Path.GetFileName(sourcePath),
            TotalRows = rows.Count,
            OutputFolder = CreateOutputFolder(outputRoot),
            EnabledRules = RuleCatalog.DisplayOrder.Where(opts.Enabled.Contains).ToList()
        };
        var for1C = new List<ProcessResult>();
        var manual = new List<ProcessResult>();
        var unchanged = new List<ProcessResult>();
        int failures = 0;

        try
        {
            for (int i = 0; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            ProcessResult res;
            try
            {
                res = CodeProcessor.Process(rows[i].Article, rows[i].Code, opts);
            }
            catch (Exception ex)
            {
                res = new ProcessResult { Article = rows[i].Article, Original = rows[i].Code };
                res.Result = rows[i].Code;
                res.ProcessingError = ex.Message;
                failures++;
            }
            res.Group = rows[i].Group;   // значение переносится в файл результата

            foreach (var r in res.Applied) report.RuleCounts[r]++;

            if (res.ProcessingError != null) manual.Add(res);
            // Марка стали на кириллице — всегда на ручную проверку, даже если значение не менялось.
            else if (!res.Changed && !res.DeletedByArticle && !res.SteelGradeCyrillic) unchanged.Add(res);
            else if (res.ReadyFor1C) for1C.Add(res);
            else manual.Add(res);

            if (progress != null && (i % 25 == 0 || i == rows.Count - 1))
                progress.Report(rows.Count == 0 ? 100 : (i + 1) * 100 / rows.Count);
        }
        }
        catch (OperationCanceledException)
        {
            // Прервано: файлы пишутся после цикла, папка ещё пустая — удаляем.
            try { Directory.Delete(report.OutputFolder, recursive: true); } catch { }
            throw;
        }

        // Один и тот же комм. код у разных артикулов — ищем отдельно в каждом файле.
        var dup1C = FindCrossArticleDuplicates(for1C);
        var dupManual = FindCrossArticleDuplicates(manual);

        // Дубликаты в файл для 1С не включаются — только в файл дубликатов.
        var duplicates1C = dup1C.OrderBy(i => i).Select(i => for1C[i]).ToList();
        var clean1C = for1C.Where((_, i) => !dup1C.Contains(i)).ToList();

        report.Count1C = clean1C.Count;
        report.CountManual = manual.Count;
        report.CountUnchanged = unchanged.Count;
        report.Failures = failures;
        report.Duplicates1C = duplicates1C.Count;
        report.DuplicatesManual = dupManual.Count;

        WriteSimple(Path.Combine(report.OutputFolder, File1C), clean1C, useResult: true);
        WriteSimple(Path.Combine(report.OutputFolder, FileNoChange), unchanged, useResult: false);
        WriteManual(Path.Combine(report.OutputFolder, FileManual), manual, report, dupManual);

        // Файл дубликатов — только если найден хотя бы один дубликат.
        if (duplicates1C.Count > 0)
            WriteDuplicates(Path.Combine(report.OutputFolder, FileDuplicates1C), duplicates1C);

        try { ReportWriter.Save(report); }
        catch (Exception ex) { report.ReportWriteError = ex.Message; }

        return report;
    }

    /// Групповая обработка в фоновом потоке — для интерфейса.
    public static Task<BatchReport> RunAsync(string sourcePath, ProcessOptions opts,
        IProgress<int>? progress, CancellationToken ct, string? outputRoot = null) =>
        Task.Run(() => Run(sourcePath, opts, progress, ct, outputRoot), ct);
    /// <summary>
    /// Индексы строк, у которых комм. код совпадает с другой строкой,
    /// но артикул отличается. Сравнивается исправленное значение (Result).
    /// </summary>
    private static HashSet<int> FindCrossArticleDuplicates(List<ProcessResult> data)
    {
        var byCode = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (int i = 0; i < data.Count; i++)
        {
            var r = data[i];

            // Пустые значения и сбойные строки дубликатами не считаем:
            // это не совпадение кодов, а отсутствие кода.
            if (r.ProcessingError != null) continue;
            if (r.CatalogReviewEmpty || r.DeletedByArticle) continue;   // удалённый код — не код
            string code = r.Result ?? "";
            if (code.Length == 0) continue;

            if (!byCode.TryGetValue(code, out var list))
                byCode[code] = list = new List<int>();
            list.Add(i);
        }

        var marks = new HashSet<int>();
        foreach (var kv in byCode)
        {
            if (kv.Value.Count < 2) continue;

            // Одинаковый код у ОДНОГО артикула (дубль строки) — не наш случай.
            var articles = new HashSet<string>(
                kv.Value.Select(i => (data[i].Article ?? "").Trim()),
                StringComparer.OrdinalIgnoreCase);
            if (articles.Count < 2) continue;

            foreach (int i in kv.Value) marks.Add(i);
        }
        return marks;
    }

    private static string CreateOutputFolder(string? outputRoot)
    {
        string root = outputRoot ?? AppPaths.ResultsFolder;
        string name = DateTime.Now.ToString("dd.MM.yyyy_HH.mm");
        string path = Path.Combine(root, name);
        int n = 2;
        while (Directory.Exists(path)) path = Path.Combine(root, $"{name}_{n++}");
        Directory.CreateDirectory(path);
        return path;
    }
    // ---------------- простые файлы (2 столбца) ----------------
    private static void WriteSimple(string path, List<ProcessResult> data, bool useResult)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Данные");
        ws.Cell(1, 1).Value = "Артикул";
        ws.Cell(1, 2).Value = "Коммерческий код";
        ws.Cell(1, 4).Value = OutHeaderGroup;   // 4-й столбец по ТЗ
        ws.Row(1).Style.Font.Bold = true;
        ws.Columns(1, 4).Style.NumberFormat.Format = "@";

        for (int i = 0; i < data.Count; i++)
        {
            ws.Cell(i + 2, 1).Value = data[i].Article;
            ws.Cell(i + 2, 2).Value = useResult ? data[i].Result : data[i].Original;
            ws.Cell(i + 2, 4).Value = data[i].Group;
        }

        int lastRow = Math.Max(1, data.Count + 1);
        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, lastRow, 4).SetAutoFilter();
        ws.Range(1, 1, lastRow, 4).Style.Alignment.WrapText = true;
        ws.Column(1).Width = 24; ws.Column(2).Width = 42;
        ws.Column(3).Width = 3; ws.Column(4).Width = 28;

        wb.SaveAs(path);
    }

    // ---------------- файл дубликатов (только дубликаты, без выделения) ----------------
    private static void WriteDuplicates(string path, List<ProcessResult> data)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Дубликаты");
        ws.Cell(1, 1).Value = "Артикул";
        ws.Cell(1, 2).Value = "Коммерческий код";
        ws.Row(1).Style.Font.Bold = true;
        ws.Columns(1, 2).Style.NumberFormat.Format = "@";

        for (int i = 0; i < data.Count; i++)
        {
            ws.Cell(i + 2, 1).Value = data[i].Article;
            ws.Cell(i + 2, 2).Value = data[i].Result;
        }

        int last = Math.Max(1, data.Count + 1);
        ws.Range(1, 1, last, 2).Style.Alignment.WrapText = true;
        ws.Range(1, 1, last, 2).SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        ws.Column(1).Width = 24; ws.Column(2).Width = 42;
        wb.SaveAs(path);
    }

    // ---------------- файл ручной проверки ----------------
    private static void WriteManual(string path, List<ProcessResult> data,
                                BatchReport report, HashSet<int> duplicates)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Ручная проверка");
        ws.Cell(1, 1).Value = "Артикул";
        ws.Cell(1, 2).Value = "Коммерческий код";
        ws.Cell(1, 3).Value = "Коммерческий код (исправленный)";
        ws.Cell(1, 4).Value = "Критерий изменений";
        ws.Cell(1, 6).Value = OutHeaderGroup;          // 6-й столбец по ТЗ
        ws.Row(1).Style.Font.Bold = true;
        ws.Columns(1, 6).Style.NumberFormat.Format = "@";

        for (int i = 0; i < data.Count; i++)
        {
            var r = data[i];
            int row = i + 2;

            var cellArticle = ws.Cell(row, 1);
            cellArticle.Value = r.Article;
            SetWithHighlight(ws.Cell(row, 2), r.Original, r.Highlight);

            ws.Cell(row, 3).Value = r.ProcessingError != null
                ? "Обработка не выполнена"
                : (r.CatalogReviewEmpty ? "" : (r.IsEmptyResult && !r.DeletedByArticle ? "Пустое значение" : r.Result));
            ws.Cell(row, 4).Value = r.CriteriaText;
            ws.Cell(row, 6).Value = r.Group;

            var rng = ws.Range(row, 1, row, 6);
            if (r.ProcessingError != null) rng.Style.Fill.BackgroundColor = Violet;
            else if (r.IsEmptyResult && !r.DeletedByArticle && !r.CatalogReviewEmpty) rng.Style.Fill.BackgroundColor = Red;
            else if (r.LeadingWordRemoved) rng.Style.Fill.BackgroundColor = Blue;
            else if (r.TrailingResidue) rng.Style.Fill.BackgroundColor = Yellow;
            else if (r.Suspicious) rng.Style.Fill.BackgroundColor = Orange;

            if (duplicates.Contains(i))
            {
                cellArticle.Style.Font.FontColor = XLColor.Red;
                cellArticle.Style.Font.Bold = true;
            }
        }

        int lastRow = Math.Max(1, data.Count + 1);
        ws.Range(1, 1, lastRow, 6).Style.Alignment.WrapText = true;
        ws.Range(1, 1, lastRow, 6).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        ws.Range(1, 1, lastRow, 6).SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        ws.Column(1).Width = 22; ws.Column(2).Width = 38;
        ws.Column(3).Width = 38; ws.Column(4).Width = 32;
        ws.Column(5).Width = 3; ws.Column(6).Width = 28;

        WriteLegendSheet(wb, report);
        wb.SaveAs(path);
    }

    private static void SetWithHighlight(IXLCell cell, string text, HashSet<int> marks)
    {
        if (text.Length == 0 || marks.Count == 0)
        {
            cell.Value = text;
            return;
        }

        // cell.Value здесь НЕ присваиваем: CreateRichText() засеял бы первый run
        // текущим значением ячейки и текст продублировался бы.
        cell.Clear(XLClearOptions.Contents);

        var rt = cell.CreateRichText();
        rt.ClearText();

        foreach (var (start, len, marked) in CodeProcessor.Segments(text, marks))
        {
            var run = rt.AddText(text.Substring(start, len));
            if (marked)
            {
                run.SetFontColor(XLColor.Red);
                run.SetBold(true);
            }
        }
    }

    private static void WriteLegendSheet(XLWorkbook wb, BatchReport report)
    {
        var ws = wb.AddWorksheet("Легенда и сводка");
        int r = 1;
        ws.Cell(r, 1).Value = "ЛЕГЕНДА ВЫДЕЛЕНИЯ";
        ws.Cell(r, 1).Style.Font.Bold = true; r += 2;

        void Legend(XLColor? fill, string sample, string text, bool redFont = false)
        {
            var c = ws.Cell(r, 1);
            c.Value = sample;
            if (fill != null) c.Style.Fill.BackgroundColor = fill;
            if (redFont) { c.Style.Font.FontColor = XLColor.Red; c.Style.Font.Bold = true; }
            ws.Cell(r, 2).Value = text;
            r++;
        }

        Legend(Red, "Красная заливка", "Обработка вернула пустое значение (кроме удаления по виду артикула) — проверьте исходный код");
        Legend(Yellow, "Жёлтая заливка", "После правил «Удаление значащих слов»/«Удаление окончания» в конце значения остались необработанные символы");
        Legend(Orange, "Оранжевая заливка", "Подозрительный результат: остались обозначения стандарта/резьбы/размерности или пробелы");
        Legend(Blue, "Синяя заливка", "Правило «Удаление значащих слов» удалило слово, стоявшее в начале исходного значения — проверьте, не является ли оно частью кода");
        Legend(Violet, "Сиреневая заливка", "Сбой обработки строки: значение оставлено без изменений, текст ошибки в столбце «Критерий изменений»");
        Legend(null, "Красный шрифт в «Коммерческий код»", "Символы, к которым были применены правила", redFont: true);
        Legend(null, "Красный шрифт в «Артикул»", "Исправленный комм. код совпадает с кодом другого артикула в этом файле — требуется проверка", redFont: true);

        r += 1;
        ws.Cell(r, 1).Value = "СВОДНАЯ ТАБЛИЦА";
        ws.Cell(r, 1).Style.Font.Bold = true; r += 1;

        ws.Cell(r, 1).Value = "Показатель";
        ws.Cell(r, 2).Value = "Значение";
        ws.Row(r).Style.Font.Bold = true; r++;

        ws.Cell(r, 1).Value = "Анализируемый файл"; ws.Cell(r++, 2).Value = report.SourceFileName;
        ws.Cell(r, 1).Value = "Проанализировано строк"; ws.Cell(r++, 2).Value = report.TotalRows;
        ws.Cell(r, 1).Value = "Коды для загрузки в 1С"; ws.Cell(r++, 2).Value = report.Count1C;
        ws.Cell(r, 1).Value = "Коды для ручной проверки"; ws.Cell(r++, 2).Value = report.CountManual;
        ws.Cell(r, 1).Value = "Комм. код не был изменён"; ws.Cell(r++, 2).Value = report.CountUnchanged;

        if (report.Failures > 0)
        {
            ws.Cell(r, 1).Value = "Сбоев обработки";
            ws.Cell(r, 2).Value = report.Failures;
            ws.Row(r).Style.Font.FontColor = XLColor.DarkRed;
            ws.Row(r).Style.Font.Bold = true;
            r++;
        }

        if (report.Duplicates1C > 0 || report.DuplicatesManual > 0)
        {
            ws.Cell(r, 1).Value = "Дубликатов кода (исключены из файла для 1С)";
            ws.Cell(r, 2).Value = report.Duplicates1C;
            ws.Row(r).Style.Font.FontColor = XLColor.Red;
            ws.Row(r).Style.Font.Bold = true;
            r++;

            ws.Cell(r, 1).Value = "Дубликатов кода (ручная проверка)";
            ws.Cell(r, 2).Value = report.DuplicatesManual;
            ws.Row(r).Style.Font.FontColor = XLColor.Red;
            ws.Row(r).Style.Font.Bold = true;
            r++;
        }
        r++;

        ws.Cell(r, 1).Value = "Правило";
        ws.Cell(r, 2).Value = "Исправлено записей";
        ws.Row(r).Style.Font.Bold = true; r++;
        foreach (var rule in RuleCatalog.DisplayOrder)
        {
            ws.Cell(r, 1).Value = RuleCatalog.ShortName(rule);
            ws.Cell(r, 2).Value = report.RuleCounts[rule];
            r++;
        }
        ws.Column(1).Width = 46; ws.Column(2).Width = 60;
        ws.Column(2).Style.Alignment.WrapText = true;
    }
}

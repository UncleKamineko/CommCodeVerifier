    using System.Text;

namespace CommCodeVerifier.Core;

/// <summary>
/// Формирование сводки по результатам групповой обработки.
/// Единый источник как для поля на второй вкладке, так и для txt-файла.
/// </summary>
public static class ReportWriter
{
    public const string FileName = "Применённые правила.txt";

    private const int MinColumns = 54;

    public static List<(string left, string right)> BuildSummaryLines(BatchReport r)
    {
        var lines = new List<(string left, string right)>
        {
            ("Файл: " + r.SourceFileName, ""),
            ("Проанализировано строк:", r.TotalRows.ToString()),
            ("Коды для загрузки в 1С:", r.Count1C.ToString()),
            ("Коды для ручной проверки:", r.CountManual.ToString()),
            ("Комм. код не был изменён:", r.CountUnchanged.ToString())
        };

        if (r.Failures > 0)
            lines.Add(("СБОЕВ ОБРАБОТКИ:", r.Failures.ToString()));

        if (r.Duplicates1C > 0)
            lines.Add(("ДУБЛИКАТОВ КОДА (исключены из 1С):", r.Duplicates1C.ToString()));
        if (r.DuplicatesManual > 0)
            lines.Add(("ДУБЛИКАТОВ КОДА (ручная проверка):", r.DuplicatesManual.ToString()));

        lines.Add(("", ""));
        lines.Add(("Изменено строк по правилам:", ""));

        foreach (var rule in RuleCatalog.DisplayOrder)
            lines.Add((RuleCatalog.ShortName(rule), r.RuleCounts[rule].ToString()));

        return lines;
    }

    public static string BuildText(BatchReport r)
    {
        var lines = BuildSummaryLines(r);
        int cols = Math.Max(MinColumns, lines.Max(l => l.left.Length + l.right.Length + 2));

        var sb = new StringBuilder();

        sb.AppendLine("РЕЗУЛЬТАТЫ ОБРАБОТКИ КОММЕРЧЕСКИХ КОДОВ");
        sb.AppendLine(new string('=', cols));
        sb.AppendLine("Дата и время анализа: " + r.StartedAt.ToString("dd.MM.yyyy HH:mm:ss"));
        sb.AppendLine("Папка с результатами: " + r.OutputFolder);
        sb.AppendLine();

        foreach (var (left, right) in lines)
        {
            if (left.Length == 0 && right.Length == 0) { sb.AppendLine(); continue; }
            if (right.Length == 0) { sb.AppendLine(left); continue; }

            int pad = Math.Max(1, cols - left.Length - right.Length);
            sb.AppendLine(left + new string(' ', pad) + right);
        }

        var dupFiles = r.FilesWithDuplicates.ToList();
        if (dupFiles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("ДУБЛИКАТЫ КОММ. КОДОВ");
            sb.AppendLine(new string('-', cols));
            foreach (var f in dupFiles)
                sb.AppendLine("  " + BatchProcessor.DuplicateWarning(f));
            if (r.DuplicatesManual > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"  В файле {BatchProcessor.FileManual} артикулы таких строк выделены красным полужирным шрифтом.");
            }
        }

        // Состав включённых правил: иначе счётчик 0 неотличим от «правило отключено».
        sb.AppendLine();
        sb.AppendLine("ПРАВИЛА, ВКЛЮЧЁННЫЕ В НАСТРОЙКАХ АНАЛИЗА");
        sb.AppendLine(new string('-', cols));

        var enabled = r.EnabledRules ?? new List<RuleId>();
        foreach (var rule in RuleCatalog.DisplayOrder)
        {
            bool on = enabled.Contains(rule);
            sb.AppendLine($"  [{(on ? "X" : " ")}] {RuleCatalog.ShortName(rule)}" +
                          (on ? "" : "   — ОТКЛЮЧЕНО"));
        }

        if (enabled.Count < RuleCatalog.DisplayOrder.Length)
        {
            sb.AppendLine();
            sb.AppendLine("ВНИМАНИЕ! Выбраны не все правила, анализ может быть не полный!");
        }

        sb.AppendLine();
        sb.AppendLine("ФАЙЛЫ С РЕЗУЛЬТАТАМИ");
        sb.AppendLine(new string('-', cols));
        sb.AppendLine("  " + BatchProcessor.File1C);
        sb.AppendLine("  " + BatchProcessor.FileManual);
        sb.AppendLine("  " + BatchProcessor.FileNoChange);
        if (r.Duplicates1C > 0) sb.AppendLine("  " + BatchProcessor.FileDuplicates1C);
        sb.AppendLine("  " + FileName + "   (этот файл)");

        return sb.ToString();
    }

    public static string Save(BatchReport r)
    {
        string path = Path.Combine(r.OutputFolder, FileName);
        File.WriteAllText(path, BuildText(r), new UTF8Encoding(true));
        return path;
    }
}
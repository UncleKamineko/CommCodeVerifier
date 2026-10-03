using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace CommCodeVerifier.Core;

public enum ImRuleKind { StartsWith, EndsWith, Contains }

/// <summary>Что проверяет набор правил.</summary>
public enum ImRuleTarget { Code, Article }

public sealed class ImRule
{
    public ImRule(ImRuleTarget target, ImRuleKind kind, string pattern)
    {
        Target = target;
        Kind = kind;
        Pattern = pattern;

        string body = "(?:" + ConfigRepository.WildcardToRegex(pattern) + ")";
        string expr = kind switch
        {
            ImRuleKind.StartsWith => "^" + body,
            ImRuleKind.EndsWith => body + "$",
            _ => body
        };
        Rx = new Regex(expr, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public ImRuleTarget Target { get; }
    public ImRuleKind Kind { get; }
    public string Pattern { get; }
    public Regex Rx { get; }

    public string KindName => Kind switch
    {
        ImRuleKind.StartsWith => "начинается с",
        ImRuleKind.EndsWith => "заканчивается на",
        _ => "содержит"
    };

    public string TargetName => Target == ImRuleTarget.Code ? "комм. код" : "артикул";

    public override string ToString() => $"{TargetName} {KindName} «{Pattern}»";
}

/// <summary>Результат проверки одного значения по набору правил.</summary>
public sealed class ImMatch
{
    public List<ImRule> Matched { get; } = new();
    /// <summary>Индексы символов, из-за которых значение признано ошибочным.</summary>
    public HashSet<int> Chars { get; } = new();
    public bool IsMatch => Matched.Count > 0;
    public string Description => string.Join("; ", Matched.Select(r => r.ToString()));
}

public sealed class ImRuleSet
{
    public ImRuleSet(ImRuleTarget target) => Target = target;

    public ImRuleTarget Target { get; }
    public List<ImRule> Rules { get; } = new();
    public List<string> Warnings { get; } = new();
    public string? SourcePath { get; set; }

    public bool IsEmpty => Rules.Count == 0;

    /// <summary>
    /// Значение проверяется по ВСЕМ правилам: собираются все совпадения,
    /// чтобы подсветить каждый проблемный фрагмент, а не только первый.
    /// </summary>
    public ImMatch Check(string? value)
    {
        var res = new ImMatch();
        if (string.IsNullOrEmpty(value)) return res;

        foreach (var rule in Rules)
        {
            bool hit = false;
            foreach (Match m in rule.Rx.Matches(value))
            {
                hit = true;
                for (int i = m.Index; i < m.Index + m.Length; i++) res.Chars.Add(i);

                // Для префикса и суффикса совпадение единственно по определению.
                if (rule.Kind != ImRuleKind.Contains) break;
            }
            if (hit) res.Matched.Add(rule);
        }
        return res;
    }

    // ---------------- имена файлов и листов ----------------
    public const string CodeFileName = "Правила проверки комм. кода для ИМ.xlsx";
    public const string ArticleFileName = "Правила проверки артикула для ИМ.xlsx";

    public const string SheetStarts = "Начинается с";
    public const string SheetEnds = "Заканчивается на";
    public const string SheetContains = "Содержит";

    private static readonly Dictionary<ImRuleTarget, (string starts, string ends, string contains)> Headers =
        new()
        {
            [ImRuleTarget.Code] = (
                "Префиксы комм. кода для исключения",
                "Окончания комм. кода для исключения",
                "Части значений для исключения"),
            [ImRuleTarget.Article] = (
                "Начало артикула для исключения",
                "Окончание артикула для исключения",
                "Символы в артикуле для исключения")
        };

    // ---------------- загрузка ----------------
    public static ImRuleSet Load(string path, ImRuleTarget target)
    {
        var set = new ImRuleSet(target) { SourcePath = path };

        if (!File.Exists(path))
        {
            set.Warnings.Add($"Файл «{Path.GetFileName(path)}» не найден: {path}");
            return set;
        }

        try
        {
            using var wb = new XLWorkbook(path);
            AddSheet(wb, set, SheetStarts, ImRuleKind.StartsWith);
            AddSheet(wb, set, SheetEnds, ImRuleKind.EndsWith);
            AddSheet(wb, set, SheetContains, ImRuleKind.Contains);
        }
        catch (Exception ex)
        {
            set.Warnings.Add($"«{Path.GetFileName(path)}»: {ex.Message}");
        }
        return set;
    }

    private static void AddSheet(XLWorkbook wb, ImRuleSet set, string name, ImRuleKind kind)
    {
        var ws = wb.Worksheets.FirstOrDefault(
            w => string.Equals(w.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (ws == null)
        {
            set.Warnings.Add($"В файле «{Path.GetFileName(set.SourcePath)}» нет листа «{name}».");
            return;
        }

        var last = ws.LastRowUsed();
        if (last == null) return;

        for (int r = 2; r <= last.RowNumber(); r++)   // строка 1 — заголовок
        {
            string v = (ws.Cell(r, 1).GetFormattedString() ?? "").Trim();
            if (v.Length == 0) continue;
            set.Rules.Add(new ImRule(set.Target, kind, v));
        }
    }

    /// <summary>Создаёт файл-заготовку с тремя листами и заголовками.</summary>
    public static void CreateTemplate(string path, ImRuleTarget target)
    {
        var h = Headers[target];

        using var wb = new XLWorkbook();
        wb.AddWorksheet(SheetStarts).Cell(1, 1).Value = h.starts;
        wb.AddWorksheet(SheetEnds).Cell(1, 1).Value = h.ends;
        wb.AddWorksheet(SheetContains).Cell(1, 1).Value = h.contains;

        foreach (var ws in wb.Worksheets)
        {
            ws.Column(1).Width = 56;
            ws.Row(1).Style.Font.Bold = true;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        wb.SaveAs(path);
    }
}
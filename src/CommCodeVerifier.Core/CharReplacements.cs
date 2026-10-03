using ClosedXML.Excel;

namespace CommCodeVerifier.Core;

/// <summary>
/// Карта замен символов для правила 10 и предохранитель к нему.
/// Замена применяется к значению целиком только при отсутствии кириллических
/// букв, не входящих в карту.
/// </summary>
public sealed class CharReplacements
{
    public const string FileName = "Замены символов.xlsx";
    public const string HeaderFrom = "Заменяемый символ";
    public const string HeaderTo = "Корректный символ";

    private readonly Dictionary<char, string> _map = new();

    public List<string> Warnings { get; } = new();
    public string? SourcePath { get; private set; }

    public int Count => _map.Count;
    public bool IsEmpty => _map.Count == 0;

    /// <summary>Заменяемые символы — они же «известная» кириллица для предохранителя.</summary>
    public IEnumerable<char> Keys => _map.Keys;

    public bool TryMap(char c, out string replacement) => _map.TryGetValue(c, out replacement!);

    /// <summary>Символ входит в карту замен.</summary>
    public bool IsKnown(char c) => _map.ContainsKey(c);

    /// <summary>Кириллица, включая расширенные блоки.</summary>
    public static bool IsCyrillic(char c) =>
        (c >= '\u0400' && c <= '\u04FF') || (c >= '\u0500' && c <= '\u052F');

    /// <summary>
    /// Предохранитель: замену можно применять, только если в значении нет
    /// кириллических букв вне карты замен.
    /// ПЗК-RU52 — «П» вне карты, значение не трогаем.
    /// БУ-Р04-РФ03 — «Б» и «Ф» вне карты, значение не трогаем.
    /// </summary>
    public bool CanApply(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        foreach (char c in value)
            if (IsCyrillic(c) && !IsKnown(c)) return false;

        return true;
    }

    /// <summary>Кириллические буквы вне карты — для диагностики.</summary>
    public string BlockingChars(string value)
    {
        var set = new SortedSet<char>();
        foreach (char c in value)
            if (IsCyrillic(c) && !IsKnown(c)) set.Add(c);
        return new string(set.ToArray());
    }

    // ---------------- загрузка ----------------
    public static CharReplacements Load(string path)
    {
        var map = new CharReplacements { SourcePath = path };

        if (!File.Exists(path))
        {
            map.Warnings.Add($"Файл «{FileName}» не найден: {path}");
            return map;
        }

        try
        {
            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet(1);
            var last = ws.LastRowUsed();
            if (last == null)
            {
                map.Warnings.Add($"Файл «{FileName}» пуст.");
                return map;
            }

            for (int r = 2; r <= last.RowNumber(); r++)   // строка 1 — заголовки
            {
                string from = (ws.Cell(r, 1).GetFormattedString() ?? "").Trim();
                string to = (ws.Cell(r, 2).GetFormattedString() ?? "").Trim();

                if (from.Length == 0) continue;
                if (from.Length > 1)
                {
                    map.Warnings.Add($"«{FileName}», строка {r}: в первом столбце " +
                                     $"должен быть один символ, указано «{from}» — строка пропущена.");
                    continue;
                }
                if (to.Length == 0)
                {
                    map.Warnings.Add($"«{FileName}», строка {r}: не указан корректный символ " +
                                     "— строка пропущена.");
                    continue;
                }

                map._map[from[0]] = to;
            }

            if (map._map.Count == 0)
                map.Warnings.Add($"В файле «{FileName}» не найдено ни одной пары замены.");
        }
        catch (Exception ex)
        {
            map.Warnings.Add($"«{FileName}»: {ex.Message}");
        }
        return map;
    }

    /// <summary>
    /// Заготовка с 12 парами прописных букв из ТЗ.
    /// Строчные намеренно НЕ включены: они сделали бы предохранитель мягче
    /// и позволили бы исказить остаточные русские слова (например «мост» -> «moct»).
    /// При необходимости пользователь добавляет их сам.
    /// </summary>
    public static void CreateTemplate(string path)
    {
        var pairs = new (char from, char to)[]
        {
            ('А','A'), ('В','B'), ('Е','E'), ('К','K'), ('М','M'), ('Н','H'),
            ('О','O'), ('Р','P'), ('С','C'), ('Т','T'), ('Х','X'), ('У','Y')
        };

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Замены");
        ws.Cell(1, 1).Value = HeaderFrom;
        ws.Cell(1, 2).Value = HeaderTo;
        ws.Row(1).Style.Font.Bold = true;
        ws.Columns(1, 2).Style.NumberFormat.Format = "@";

        for (int i = 0; i < pairs.Length; i++)
        {
            ws.Cell(i + 2, 1).Value = pairs[i].from.ToString();
            ws.Cell(i + 2, 2).Value = pairs[i].to.ToString();
        }

        ws.Column(1).Width = 22;
        ws.Column(2).Width = 22;
        ws.SheetView.FreezeRows(1);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        wb.SaveAs(path);
    }
}
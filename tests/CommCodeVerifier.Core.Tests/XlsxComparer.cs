using System.Text;
using ClosedXML.Excel;

namespace CommCodeVerifier.Core.Tests;

/// <summary>
/// Сравнение двух xlsx: состав листов, значения всех ячеек;
/// при compareStyles — заливка, цвет и жирность шрифта, фрагменты форматированного текста.
/// </summary>
internal static class XlsxComparer
{
    public static List<string> Compare(string expectedPath, string actualPath, bool compareStyles, int max = 30)
    {
        var diffs = new List<string>();
        using var e = new XLWorkbook(expectedPath);
        using var a = new XLWorkbook(actualPath);

        var eNames = e.Worksheets.Select(w => w.Name).ToList();
        var aNames = a.Worksheets.Select(w => w.Name).ToList();
        if (!eNames.SequenceEqual(aNames))
        {
            diffs.Add($"листы: эталон [{string.Join(", ", eNames)}], получено [{string.Join(", ", aNames)}]");
            return diffs;
        }

        foreach (var we in e.Worksheets)
        {
            var wa = a.Worksheet(we.Name);
            int rows = Math.Max(we.LastRowUsed()?.RowNumber() ?? 0, wa.LastRowUsed()?.RowNumber() ?? 0);
            int cols = Math.Max(we.LastColumnUsed()?.ColumnNumber() ?? 0, wa.LastColumnUsed()?.ColumnNumber() ?? 0);

            for (int r = 1; r <= rows; r++)
                for (int c = 1; c <= cols; c++)
                {
                    var ce = we.Cell(r, c);
                    var ca = wa.Cell(r, c);

                    string ve = ce.GetFormattedString();
                    string va = ca.GetFormattedString();
                    if (ve != va)
                        diffs.Add($"{we.Name}!{ce.Address}: эталон «{ve}», получено «{va}»");
                    else if (compareStyles)
                    {
                        string se = StyleKey(ce), sa = StyleKey(ca);
                        if (se != sa) diffs.Add($"{we.Name}!{ce.Address} оформление: эталон [{se}], получено [{sa}]");
                    }
                    if (diffs.Count >= max) return diffs;
                }
        }
        return diffs;
    }

    private static string StyleKey(IXLCell c)
    {
        var sb = new StringBuilder();
        sb.Append("заливка=").Append(ColorKey(c.Style.Fill.BackgroundColor));
        sb.Append("; шрифт=").Append(ColorKey(c.Style.Font.FontColor));
        if (c.Style.Font.Bold) sb.Append(" ж");
        if (c.HasRichText)
            foreach (var run in c.GetRichText())
                sb.Append(" | «").Append(run.Text).Append("» ")
                  .Append(ColorKey(run.FontColor)).Append(run.Bold ? " ж" : "");
        return sb.ToString();
    }

    private static string ColorKey(XLColor? c)
    {
        if (c == null || !c.HasValue) return "-";
        return c.ColorType switch
        {
            XLColorType.Color => c.Color.ToArgb().ToString("X8"),
            XLColorType.Theme => $"тема {c.ThemeColor} {c.ThemeTint:0.##}",
            XLColorType.Indexed => $"индекс {c.Indexed}",
            _ => c.ToString()
        };
    }
}

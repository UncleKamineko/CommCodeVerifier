using ClosedXML.Excel;

namespace CommCodeVerifier.Core.Tests;

/// <summary>
/// Небольшой входной файл в формате групповой обработки:
/// все кейсы самопроверки + строки для проверки дубликатов.
/// </summary>
internal static class SmallDataset
{
    /// Одинаковый код у разных артикулов: оба — в файл дубликатов, не в файл для 1С.
    public const string DupArticleA = "DUP-A";
    public const string DupArticleB = "DUP-B";
    public const string DupResult = "AB7731";

    /// Одинаковый код у одного артикула: не дубликат.
    public const string SameArticle = "SAME-1";
    public const string SameResult = "PC5519";

    public const string Group = "TEST";

    public static string Build(string folder)
    {
        string path = Path.Combine(folder, "Small test data.xlsx");

        var rows = new List<(string article, string code)>();
        rows.AddRange(SelfTest.Cases.Select(c => (c.article, c.code)));
        rows.AddRange(SelfTest.Rule12Cases.Select(c => (c.article, c.code)));
        rows.AddRange(SelfTest.KeepSeriesCases.Select(c => (c.article, c.code)));

        // Кириллица из карты замен — значение изменяется и готово для 1С.
        rows.Add((DupArticleA, "АВ7731"));
        rows.Add((DupArticleB, "АВ7731 "));
        rows.Add((SameArticle, "РС5519"));
        rows.Add((SameArticle, "РС5519"));
        rows.Add(("EMPTY-1", ""));

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Данные");
        ws.Cell(1, 1).Value = BatchProcessor.SrcHeaderArticle;
        ws.Cell(1, 2).Value = BatchProcessor.SrcHeaderCode;
        ws.Cell(1, 3).Value = BatchProcessor.SrcHeaderGroup;
        ws.Columns(1, 3).Style.NumberFormat.Format = "@";

        for (int i = 0; i < rows.Count; i++)
        {
            ws.Cell(i + 2, 1).Value = rows[i].article;
            ws.Cell(i + 2, 2).Value = rows[i].code;
            ws.Cell(i + 2, 3).Value = Group;
        }
        wb.SaveAs(path);
        return path;
    }
}

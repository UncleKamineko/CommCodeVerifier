using ClosedXML.Excel;
using Xunit;

namespace CommCodeVerifier.Core.Tests;

/// Групповая обработка на маленьком файле: распределение строк и дубликаты.
public class BatchInvariantTests
{
    private static BatchReport RunSmall()
    {
        var opts = TestSetup.UseConfig(TestSetup.BaseSettings());
        string folder = TestSetup.NewTempFolder("Small");
        string input = SmallDataset.Build(folder);
        return BatchProcessor.Run(input, opts, null, CancellationToken.None, Path.Combine(folder, "out"));
    }

    private static List<(string article, string code)> ReadOutput(string path)
    {
        var list = new List<(string, string)>();
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet(1);
        int last = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int r = 2; r <= last; r++)
            list.Add((ws.Cell(r, 1).GetFormattedString(), ws.Cell(r, 2).GetFormattedString()));
        return list;
    }

    [Fact(DisplayName = "Все строки распределены: 1С + ручная + не изменён + дубликаты = всего")]
    public void AllRowsAreDistributed()
    {
        var r = RunSmall();
        Assert.Equal(0, r.Failures);
        Assert.Null(r.ReportWriteError);
        Assert.Equal(r.TotalRows, r.Count1C + r.CountManual + r.CountUnchanged + r.Duplicates1C);
    }

    [Fact(DisplayName = "Дубликаты: только в файле дубликатов, не в файле для 1С")]
    public void DuplicatesAreExcludedFrom1C()
    {
        var r = RunSmall();
        string dupPath = Path.Combine(r.OutputFolder, BatchProcessor.FileDuplicates1C);

        Assert.True(r.Duplicates1C >= 2, $"Дубликатов: {r.Duplicates1C}, ожидалось не меньше 2");
        Assert.True(File.Exists(dupPath), "Нет файла дубликатов");

        var dup = ReadOutput(dupPath);
        Assert.Contains((SmallDataset.DupArticleA, SmallDataset.DupResult), dup);
        Assert.Contains((SmallDataset.DupArticleB, SmallDataset.DupResult), dup);
        Assert.DoesNotContain(dup, x => x.article == SmallDataset.SameArticle);
        Assert.DoesNotContain(dup, x => x.code.Length == 0);
        Assert.Equal(r.Duplicates1C, dup.Count);

        var for1C = ReadOutput(Path.Combine(r.OutputFolder, BatchProcessor.File1C));
        Assert.DoesNotContain(for1C, x => x.article == SmallDataset.DupArticleA);
        Assert.DoesNotContain(for1C, x => x.article == SmallDataset.DupArticleB);
        Assert.Equal(r.Count1C, for1C.Count);
    }

    [Fact(DisplayName = "Одинаковый код у одного артикула — не дубликат")]
    public void SameArticleIsNotDuplicate()
    {
        var r = RunSmall();
        var for1C = ReadOutput(Path.Combine(r.OutputFolder, BatchProcessor.File1C));
        Assert.Equal(2, for1C.Count(x => x.article == SmallDataset.SameArticle && x.code == SmallDataset.SameResult));
    }

    [Fact(DisplayName = "Без дубликатов файл дубликатов не создаётся")]
    public void NoDuplicateFileWithoutDuplicates()
    {
        var opts = TestSetup.UseConfig(TestSetup.BaseSettings());
        string folder = TestSetup.NewTempFolder("NoDup");
        string input = Path.Combine(folder, "no dup.xlsx");

        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Данные");
            ws.Cell(1, 1).Value = BatchProcessor.SrcHeaderArticle;
            ws.Cell(1, 2).Value = BatchProcessor.SrcHeaderCode;
            ws.Cell(1, 3).Value = BatchProcessor.SrcHeaderGroup;
            ws.Cell(2, 1).Value = "ONE-1"; ws.Cell(2, 2).Value = "АВ1111";
            ws.Cell(3, 1).Value = "TWO-2"; ws.Cell(3, 2).Value = "АВ2222";
            wb.SaveAs(input);
        }

        var r = BatchProcessor.Run(input, opts, null, CancellationToken.None, Path.Combine(folder, "out"));
        Assert.Equal(0, r.Duplicates1C);
        Assert.False(File.Exists(Path.Combine(r.OutputFolder, BatchProcessor.FileDuplicates1C)));
    }

    [Fact(DisplayName = "Отмена: исключение, папка результатов не остаётся")]
    public void CancelRemovesOutputFolder()
    {
        var opts = TestSetup.UseConfig(TestSetup.BaseSettings());
        string folder = TestSetup.NewTempFolder("Cancel");
        string input = SmallDataset.Build(folder);
        string outRoot = Path.Combine(folder, "out");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            BatchProcessor.Run(input, opts, null, cts.Token, outRoot));
        Assert.Empty(Directory.GetDirectories(outRoot));
    }
}

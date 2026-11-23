using ClosedXML.Excel;
using Xunit;

namespace CommCodeVerifier.Core.Tests;

public class ImSingleFileTests
{
    private static string CreateFile(string thirdHeader)
    {
        Directory.CreateDirectory(TestSetup.TempRoot);
        string path = Path.Combine(TestSetup.TempRoot, Guid.NewGuid() + ".xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Данные");
        ws.Cell(1, 1).Value = ImChecker.HeaderArticle;
        ws.Cell(1, 2).Value = ImChecker.HeaderCode;
        ws.Cell(1, 3).Value = thirdHeader;
        ws.Cell(2, 1).Value = "A1";
        ws.Cell(2, 2).Value = "CODE1";
        ws.Cell(2, 3).Value = "Группа";
        ws.Cell(3, 1).Value = "A2";
        ws.Cell(3, 2).Value = "CODE2";
        wb.SaveAs(path);
        return path;
    }

    [Fact]
    public void RowsWithoutGroupAreSkipped()
    {
        string path = CreateFile(BatchProcessor.SrcHeaderGroup);
        Assert.True(ImChecker.ValidateSingleTemplate(path, out _));

        var report = ImChecker.RunSingleFile(path, new ImRuleSet(ImRuleTarget.Code),
            new ImRuleSet(ImRuleTarget.Article), null, CancellationToken.None);

        Assert.Equal(1, report.TotalRows);
        Assert.Equal(1, report.SkippedNoGroup);
    }

    [Fact]
    public void FileWithoutGroupColumnIsRejected()
    {
        Assert.False(ImChecker.ValidateSingleTemplate(CreateFile(""), out string error));
        Assert.Contains(BatchProcessor.SrcHeaderGroup, error);
    }
}

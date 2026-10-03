using System.Text.RegularExpressions;
using Xunit;

namespace CommCodeVerifier.Core.Tests;

/// <summary>
/// Эталон: обработка Golden/Data for tests.xlsx с Golden/settings.json даёт те же
/// выходные файлы, что WinForms 0.9.0. Без папки Golden тест пропускается.
/// </summary>
public class GoldenTests
{
    private const string InputName = "Data for tests.xlsx";

    // Строки сводки, зависящие от времени запуска и папки результатов.
    private static readonly Regex VolatileLine = new(@"\d{2}\.\d{2}\.\d{4}|\d{1,2}:\d{2}|Результаты обработки");

    [SkippableFact(DisplayName = "Групповая обработка совпадает с WinForms 0.9.0")]
    public void BatchMatchesWinForms090()
    {
        string input = Path.Combine(TestSetup.GoldenDir, InputName);
        string settingsFile = Path.Combine(TestSetup.GoldenDir, "settings.json");
        Skip.IfNot(File.Exists(input) && File.Exists(settingsFile), "Нет эталонных данных в Golden");

        var s = ProcessingSettings.FromJson(File.ReadAllText(settingsFile));
        TestSetup.ClearPaths(s);
        var opts = TestSetup.UseConfig(s);

        string outRoot = TestSetup.NewTempFolder("Golden");
        var report = BatchProcessor.Run(input, opts, null, CancellationToken.None, outRoot);
        Assert.Null(report.ReportWriteError);

        var diffs = new List<string>();

        foreach (var file in new[]
                 {
                     BatchProcessor.File1C, BatchProcessor.FileManual,
                     BatchProcessor.FileNoChange, BatchProcessor.FileDuplicates1C
                 })
        {
            string expected = Path.Combine(TestSetup.GoldenDir, file);
            string actual = Path.Combine(report.OutputFolder, file);

            if (File.Exists(expected) != File.Exists(actual))
            {
                diffs.Add($"{file}: в эталоне {(File.Exists(expected) ? "есть" : "нет")}, " +
                          $"в результате {(File.Exists(actual) ? "есть" : "нет")}");
                continue;
            }
            if (!File.Exists(expected)) continue;

            bool styles = file == BatchProcessor.FileManual;
            diffs.AddRange(XlsxComparer.Compare(expected, actual, styles).Select(d => $"{file}: {d}"));
        }

        diffs.AddRange(CompareSummary(TestSetup.GoldenDir, report.OutputFolder));

        Assert.True(diffs.Count == 0,
            $"Расхождений с эталоном: {diffs.Count} (показаны первые)\n" + string.Join("\n", diffs.Take(60)));
    }

    /// Применённые правила.txt — без строк с датой, временем и путём.
    private static IEnumerable<string> CompareSummary(string goldenDir, string outDir)
    {
        string? expected = Directory.GetFiles(goldenDir, "*.txt").FirstOrDefault();
        string? actual = Directory.GetFiles(outDir, "*.txt").FirstOrDefault();
        if (expected == null || actual == null)
        {
            yield return $"сводка txt: в эталоне {(expected != null ? "есть" : "нет")}, в результате {(actual != null ? "есть" : "нет")}";
            yield break;
        }

        var e = Normalize(expected);
        var a = Normalize(actual);
        int n = Math.Max(e.Count, a.Count), shown = 0;
        for (int i = 0; i < n && shown < 20; i++)
        {
            string le = i < e.Count ? e[i] : "<нет строки>";
            string la = i < a.Count ? a[i] : "<нет строки>";
            if (le == la) continue;
            shown++;
            yield return $"сводка txt, строка {i + 1}: эталон «{le}», получено «{la}»";
        }
    }

    private static List<string> Normalize(string path) =>
        ConfigRepository.ReadLinesSmart(path)
            .Select(l => l.TrimEnd())
            .Where(l => !VolatileLine.IsMatch(l))
            .ToList();
}

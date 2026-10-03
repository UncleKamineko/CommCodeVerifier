using Xunit;

namespace CommCodeVerifier.Core.Tests;

/// Проверки этапа 2: Core собирается без WinForms, читает конфигурацию и обрабатывает значения.
public class CoreSmokeTests
{
    private static ProcessOptions LoadSnapshot()
    {
        var s = new ProcessingSettings();
        ConfigRepository.Reload(s);
        return s.ToProcessOptions();
    }

    [Fact]
    public void ConfigSnapshotLoadsWithoutWarnings()
    {
        LoadSnapshot();
        var cfg = ConfigRepository.Instance;

        Assert.True(cfg.LoadWarnings.Count == 0,
            "Предупреждения загрузки:\n" + string.Join("\n", cfg.LoadWarnings));
        Assert.NotEmpty(cfg.Words);
        Assert.NotEmpty(cfg.EndingRegexes);
        Assert.False(cfg.CharMap.IsEmpty);
    }

    [Theory]
    [InlineData("A1", "M18х1.5", "M18X1,5")]
    [InlineData("A5", " SE32-16 ", "SE32-16")]
    [InlineData("A10", "М12х1,5", "M12X1,5")]
    [InlineData("A13", "BW300D1\u2013KIT", "BW300D1-KIT")]
    [InlineData("A20", "28.58.49", "28.58.49")]
    public void BasicRulesWork(string article, string code, string expected)
    {
        var opts = LoadSnapshot();
        Assert.Equal(expected, CodeProcessor.Process(article, code, opts).Result);
    }

    [Fact]
    public void ReloadReplacesInstance()
    {
        LoadSnapshot();
        var before = ConfigRepository.Instance;
        LoadSnapshot();
        Assert.NotSame(before, ConfigRepository.Instance);
    }

    [Fact]
    public void LegacyWinFormsSettingsAreReadable()
    {
        string json = File.ReadAllText(Path.Combine(TestSetup.GoldenDir, "settings.json"));
        var s = ProcessingSettings.FromJson(json);
        Assert.Equal(RuleCatalog.DisplayOrder.Length, s.EnabledRules.Count);   // в эталоне включены все 12 правил
    }
}

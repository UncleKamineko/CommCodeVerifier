using Xunit;

namespace CommCodeVerifier.Core.Tests;

/// <summary>
/// Примеры из «ПРЕОБРАЗОВАНИЕ_КОДА_В_АРТИКУЛ.txt» на картах из шаблонов программы.
/// </summary>
public class CylinderArticleGeneratorTests
{
    private static readonly Lazy<CylinderArticleGenerator> Generator = new(() =>
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "ArticleMaps");
        return new CylinderArticleGenerator(
            Path.Combine(dir, "ALEN_карта.xlsx"),
            Path.Combine(dir, "MID_карта.xlsx"),
            Path.Combine(dir, "Доп_код_карта.xlsx"));
    });

    private static CylinderArticleResult Generate(string code, string? manual = null) =>
        Generator.Value.Generate(code, manual);

    [Theory]
    // Раздел 10, полные примеры.
    [InlineData("40C2L160A0120", "SRF10-6266-1202")]
    [InlineData("40M2L125RL0320", "SRF10-5986-3202ZP")]
    [InlineData("67MP2C032A0030C4", "SRF10-4267-0302")]
    [InlineData("31F2A020A060N2", "SRF10-3223-0603ZK2")]
    [InlineData("45NHB100A0520", "SRF10-9124-5200")]
    [InlineData("30-050/090-3", "SRF10-8050-0903")]
    [InlineData("30-050/090", "SRF10-8050-0902")]
    [InlineData("69-032/070-F", "SRF10-8012-0700")]
    [InlineData("69-032/070-M", "SRF10-8012-0702")]
    [InlineData("QCBF2A020B100", "SRF10-3063-1002")]
    [InlineData("52G2C25A6000", "SRF10-6A86-ZZC002")]
    [InlineData("63MT2C100A0400C1V", "SRF10-5788-4002ZD")]
    [InlineData("14N1M06A10", "SRF10-0520-0101")]
    [InlineData("30K-080/180-3", "SRF10-8090-1803")]
    // Разделы 4–7.
    [InlineData("63MP2C050A0160C2G", "SRF10-4898-1602")]
    [InlineData("40M2L063A0100V", "SRF10-5086-1002ZD")]
    [InlineData("40M2L200A0400W", "SRF10-6586-4002ZL")]
    [InlineData("40M2L200A0400M", "SRF10-6586-4002ZH")]
    [InlineData("97M6V032A0050", "SRF10-702A-0506ZL")]
    [InlineData("67MP2C032A0015G", "SRF10-4301-0152")]
    [InlineData("67MT2N050A0200M", "SRF10-4882-2003ZH")]
    [InlineData("ST322A020A015", "SRF10-3150-0152")]
    [InlineData("ST322A020F015", "SRF10-3151-0152")]
    [InlineData("ST319A020A015", "SRF10-3130-0159")]
    [InlineData("43N-40-40", "SRF10-8400-0400")]
    [InlineData("QLRM2A032A010", "SRF10-QL32-0103")]
    // П. 9.3: -EX отбрасывается, EX без дефиса — доп. код.
    [InlineData("40C2L160A0120-EX", "SRF10-6266-1202")]
    [InlineData("40C2L160A0120EX", "SRF10-6266-1202EX")]
    // Шаг 1: регистр и кириллица.
    [InlineData("40с2l160а0120", "SRF10-6266-1202")]
    public void Generated(string code, string expected)
    {
        var result = Generate(code);

        Assert.Equal(CylinderArticleStatus.Generated, result.Status);
        Assert.Equal(expected, result.Article);
    }

    [Theory]
    // Раздел 3 и п. 9.2: отброшенный хвост заменяется «**», остальное сохраняется.
    [InlineData("30-050/090-3-RC04", "SRF10-8050-0903**")]
    [InlineData("40M2L125A0750(M27X2-84)", "SRF10-5986-7502**")]
    [InlineData("31F1A032A5/25N-RF01", "SRF10-3240-0051**")]
    // П. 7.1: числовой доп. код назначается вручную.
    [InlineData("40C2L160A0120-01", "SRF10-6266-1202**")]
    // П. 7.6: несколько блоков доп. кода — спец. исполнение.
    [InlineData("40M2L063F0100V", "SRF10-5086-1002**")]
    // П. 7.8: GFR на MID не влияет, доп. код вручную.
    [InlineData("67MP2C032A0010(2)GFR", "SRF10-4283-0102**")]
    public void ManualAdditionalCode(string code, string expected)
    {
        var result = Generate(code);

        Assert.Equal(CylinderArticleStatus.ManualAdditionalCode, result.Status);
        Assert.Equal(expected, result.Article);
    }

    [Fact]
    public void ManualValueReplacesStars()
    {
        var result = Generate("30-050/090-3-RC04", "01");

        Assert.Equal("SRF10-8050-090301", result.Article);
    }

    [Theory]
    [InlineData("QLSM2A032A010")]
    [InlineData("QLSN2A032A010")]
    [InlineData("19N1A20A015")]
    public void ExcludedSeries(string code)
    {
        var result = Generate(code);

        Assert.Equal(CylinderArticleStatus.ExcludedSeries, result.Status);
        Assert.Null(result.Article);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("4OC2L160A0120")]
    [InlineData("40")]
    public void InvalidInput(string code)
    {
        var result = Generate(code);

        Assert.Null(result.Article);
        Assert.NotEqual(CylinderArticleStatus.Generated, result.Status);
    }
}

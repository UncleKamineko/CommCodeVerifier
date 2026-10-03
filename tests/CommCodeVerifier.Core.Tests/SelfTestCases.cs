using Xunit;

namespace CommCodeVerifier.Core.Tests;

/// <summary>
/// Кейсы самопроверки (SelfTest.cs в Core) — каждый отдельным тестом.
/// Условия наборов те же, что в SelfTest.Run.
/// </summary>
public class SelfTestCases
{
    public static IEnumerable<object[]> Set1 =>
        SelfTest.Cases.Select(c => new object[] { c.article, c.code, c.expected });

    public static IEnumerable<object[]> Set2 =>
        SelfTest.Rule12Cases.Select(c => new object[] { c.article, c.code, c.expected });

    public static IEnumerable<object[]> Set3 =>
        SelfTest.KeepSeriesCases.Select(c => new object[] { c.article, c.code, c.expected, c.ready });

    private static ProcessOptions Options(bool rule12, string? keepSeries = null, string? extra1C = null) =>
        SelfTest.With(TestSetup.UseConfig(TestSetup.BaseSettings()), rule12, keepSeries, extra1C);

    [Theory(DisplayName = "Правила 1–11")]
    [MemberData(nameof(Set1))]
    public void Rules1To11(string article, string code, string expected)
    {
        var res = CodeProcessor.Process(article, code, Options(rule12: false));
        Assert.Equal(expected, res.Result);
    }

    [Theory(DisplayName = "Правило 12")]
    [MemberData(nameof(Set2))]
    public void Rule12(string article, string code, string expected)
    {
        var res = CodeProcessor.Process(article, code, Options(rule12: true));
        Assert.Equal(expected, res.Result);
    }

    [Theory(DisplayName = "Серии без удаления окончаний")]
    [MemberData(nameof(Set3))]
    public void KeepSeries(string article, string code, string expected, bool ready)
    {
        var opts = Options(rule12: true, SelfTest.KeepSeriesTest, SelfTest.KeepSeriesExtra);
        var res = CodeProcessor.Process(article, code, opts);
        Assert.Equal(expected, res.Result);
        Assert.True(res.ReadyFor1C == ready,
            $"Готовность для 1С: {res.ReadyFor1C}, ожидалось {ready}");
    }
}

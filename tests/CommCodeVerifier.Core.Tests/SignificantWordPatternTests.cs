using System.Text;
using Xunit;

namespace CommCodeVerifier.Core.Tests;

public sealed class SignificantWordPatternTests
{

    [Fact]
    public void AnyNumberOfAsterisksMatchesExactlyThatNumberOfCharacters()
    {
        string directory =
            TestSetup.NewTempFolder("significant-word-pattern");

        string wordsPath =
            Path.Combine(directory, "Значащие слова.txt");

        File.WriteAllLines(
            wordsPath,
            new[]
            {
            "Значащие слова",
            "Тестовый файл",
            "",
            "Шток(*)",
            "Шток(***)"
            },
            new UTF8Encoding(true));

        var settings = TestSetup.BaseSettings();
        settings.WordsFilePath = wordsPath;

        ConfigRepository.Reload(settings);

        // Шаблон Шток(*) — ровно один символ внутри скобок.
        Assert.True(
            ConfigRepository.Instance.IsSignificantWord(
                "Шток(А)"));

        Assert.True(
            ConfigRepository.Instance.IsSignificantWord(
                "шток(1)"));

        // Шаблон Шток(***) — ровно три символа внутри скобок.
        Assert.True(
            ConfigRepository.Instance.IsSignificantWord(
                "Шток(М12)"));

        Assert.True(
            ConfigRepository.Instance.IsSignificantWord(
                "шток(123)"));

        // Два символа не соответствуют ни Шток(*), ни Шток(***).
        Assert.False(
            ConfigRepository.Instance.IsSignificantWord(
                "Шток(АБ)"));

        // Четыре символа не соответствуют ни одному шаблону.
        Assert.False(
            ConfigRepository.Instance.IsSignificantWord(
                "Шток(АБВГ)"));

        // Пять символов также не соответствуют шаблону Шток(***).
        Assert.False(
            ConfigRepository.Instance.IsSignificantWord(
                "Шток(АБВГД)"));

        // Скобки обязательны.
        Assert.False(
            ConfigRepository.Instance.IsSignificantWord(
                "Шток"));

        // Дополнительный символ после закрывающей скобки запрещён.
        Assert.False(
            ConfigRepository.Instance.IsSignificantWord(
                "Шток(А)X"));
    }
    [Fact]
    public void DifferentWildcardLengthsAreIndependentPatterns()
    {
        string directory =
            TestSetup.NewTempFolder("significant-word-pattern-lengths");

        string wordsPath =
            Path.Combine(directory, "Значащие слова.txt");

        File.WriteAllLines(
            wordsPath,
            new[]
            {
            "Значащие слова",
            "Тестовый файл",
            "",
            "Шток(*)",
            "Шток(**)",
            "Шток(***)"
            },
            new UTF8Encoding(true));

        var settings = TestSetup.BaseSettings();
        settings.WordsFilePath = wordsPath;

        ConfigRepository.Reload(settings);

        Assert.True(
            ConfigRepository.Instance.IsSignificantWord(
                "Шток(А)"));

        Assert.True(
            ConfigRepository.Instance.IsSignificantWord(
                "Шток(АБ)"));

        Assert.True(
            ConfigRepository.Instance.IsSignificantWord(
                "Шток(АБВ)"));

        Assert.False(
            ConfigRepository.Instance.IsSignificantWord(
                "Шток(АБВГ)"));
    }
    public void ExistingAsteriskWordRemainsLiteral()
    {
        string directory =
            TestSetup.NewTempFolder("significant-word-literal");

        string wordsPath =
            Path.Combine(directory, "Значащие слова.txt");

        File.WriteAllLines(
            wordsPath,
            new[]
            {
                "Значащие слова",
                "Тестовый файл",
                "",
                "S.6*"
            },
            new UTF8Encoding(true));

        var settings = TestSetup.BaseSettings();
        settings.WordsFilePath = wordsPath;

        ConfigRepository.Reload(settings);

        Assert.True(
            ConfigRepository.Instance.IsSignificantWord(
                "S.6*"));

        Assert.False(
            ConfigRepository.Instance.IsSignificantWord(
                "S.6A"));
    }
}

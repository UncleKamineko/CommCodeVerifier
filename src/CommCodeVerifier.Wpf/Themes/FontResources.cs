using System.Windows;
using CommCodeVerifier.Wpf.Settings;

namespace CommCodeVerifier.Wpf.Themes;

/// <summary>
/// Размеры шрифтов — ресурсы приложения. Все элементы берут их через DynamicResource,
/// поэтому новый размер применяется сразу ко всей программе (вместо FontApplier WinForms).
/// </summary>
public static class FontResources
{
    public const string Text = "Font.Text";
    public const string Header = "Font.Header";
    public const string Button = "Font.Button";
    public const string Code = "Font.Code";
    public const string Rules = "Font.Rules";
    public const string RulesEmphasis = "Font.RulesEmphasis";
    public const string Summary = "Font.Summary";

    public static void Apply(UiSettings s)
    {
        s.Normalize();
        var r = Application.Current.Resources;
        r[Text] = (double)s.TabTextFontPx;
        r[Header] = s.TabTextFontPx + 1.0;      // заголовки — на 1 больше, полужирные (как в WinForms)
        r[Button] = (double)s.ButtonFontPx;
        r[Code] = (double)s.CodeFontPx;
        r[Rules] = (double)s.RulesFontPx;
        r[RulesEmphasis] = s.RulesFontPx + 1.0; // сообщение о пустом результате
        r[Summary] = (double)s.SummaryFontPx;
    }
}

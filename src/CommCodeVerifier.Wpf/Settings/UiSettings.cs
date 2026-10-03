namespace CommCodeVerifier.Wpf.Settings;

/// <summary>
/// Настройки интерфейса. Размеры шрифтов — в единицах WPF (1/96 дюйма);
/// при масштабе 100 % совпадают с пикселями WinForms 0.9.0. Имена полей — как
/// в settings.json WinForms, чтобы размеры переносились без преобразования.
/// </summary>
public sealed class UiSettings
{
    public const int MinFont = 6;
    public const int MaxFont = 48;

    public const int DefaultButtonFont = 12;
    public const int DefaultTabTextFont = 12;
    public const int DefaultCodeFont = 12;
    public const int DefaultRulesFont = 8;
    public const int DefaultSummaryFont = 8;

    /// Текст на вкладках (кроме кода, правил и сводки).
    public int TabTextFontPx { get; set; } = DefaultTabTextFont;
    /// Текст на кнопках.
    public int ButtonFontPx { get; set; } = DefaultButtonFont;
    /// Поле кода и результат на вкладке «Один код».
    public int CodeFontPx { get; set; } = DefaultCodeFont;
    /// Список применённых правил.
    public int RulesFontPx { get; set; } = DefaultRulesFont;
    /// Сводка групповой обработки.
    public int SummaryFontPx { get; set; } = DefaultSummaryFont;

    /// Цветовая схема интерфейса — ключ из Themes/ColorSchemes.xaml.
    public string ColorScheme { get; set; } = "Light";

    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    public void Normalize()
    {
        TabTextFontPx = Clamp(TabTextFontPx);
        ButtonFontPx = Clamp(ButtonFontPx);
        CodeFontPx = Clamp(CodeFontPx);
        RulesFontPx = Clamp(RulesFontPx);
        SummaryFontPx = Clamp(SummaryFontPx);
    }

    private static int Clamp(int v) => Math.Clamp(v, MinFont, MaxFont);
}

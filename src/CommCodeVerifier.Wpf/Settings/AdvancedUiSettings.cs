namespace CommCodeVerifier.Wpf.Settings;

/// <summary>
/// Расширенные настройки оформления: шрифты интерфейса, заголовка и разделов, радиусы скругления.
/// Хранятся в ui-settings.json (поле «Advanced»), задаются в окне «Расширенные настройки».
/// Значения по умолчанию совпадают с прежними постоянными значениями в XAML.
/// </summary>
public sealed class AdvancedUiSettings
{
    public const int MinRadius = 0;
    public const int MaxRadius = 20;

    public const string DefaultUiFontFamily = "Segoe UI";
    public const string DefaultTitleFontFamily = "Segoe UI Semibold";
    public const int DefaultTitleFont = 16;
    public const int DefaultTabRadius = 6;
    public const int DefaultSectionRadius = 6;
    public const int DefaultButtonRadius = 6;

    /// Шрифт интерфейса главного окна.
    public string UiFontFamily { get; set; } = DefaultUiFontFamily;

    /// Заголовок главного окна.
    public string TitleFontFamily { get; set; } = DefaultTitleFontFamily;
    public int TitleFontPx { get; set; } = DefaultTitleFont;

    /// <summary>
    /// Шрифты названий разделов — отдельно для каждого раздела, ключ — ключ стиля
    /// раздела в Styles.xaml (NavigationSection.TextStyleKey). Здесь только разделы,
    /// шрифт которых отличается от стиля в Styles.xaml; остальные берутся из стиля.
    /// </summary>
    public Dictionary<string, SectionFontSettings> SectionFonts { get; set; } = new();

    /// Радиусы скругления: верхние углы вкладок, левые углы разделов, все углы кнопок.
    public int TabRadius { get; set; } = DefaultTabRadius;
    public int SectionRadius { get; set; } = DefaultSectionRadius;
    public int ButtonRadius { get; set; } = DefaultButtonRadius;

    public void Normalize()
    {
        UiFontFamily = FamilyOrDefault(UiFontFamily, DefaultUiFontFamily);
        TitleFontFamily = FamilyOrDefault(TitleFontFamily, DefaultTitleFontFamily);
        TitleFontPx = ClampFont(TitleFontPx);

        // В старом ui-settings.json поля нет или там null.
        SectionFonts ??= new Dictionary<string, SectionFontSettings>();
        foreach (var key in SectionFonts.Keys.ToList())
        {
            var font = SectionFonts[key];
            if (string.IsNullOrWhiteSpace(key) || font == null || string.IsNullOrWhiteSpace(font.FontFamily))
            {
                SectionFonts.Remove(key);
                continue;
            }

            font.FontFamily = font.FontFamily.Trim();
            font.FontPx = ClampFont(font.FontPx);
        }

        TabRadius = Math.Clamp(TabRadius, MinRadius, MaxRadius);
        SectionRadius = Math.Clamp(SectionRadius, MinRadius, MaxRadius);
        ButtonRadius = Math.Clamp(ButtonRadius, MinRadius, MaxRadius);
    }

    internal static int ClampFont(int value) => Math.Clamp(value, UiSettings.MinFont, UiSettings.MaxFont);

    private static string FamilyOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

/// Шрифт названия одного раздела.
public sealed class SectionFontSettings
{
    public string FontFamily { get; set; } = "";
    public int FontPx { get; set; }
    public bool Bold { get; set; }

    public bool SameAs(SectionFontSettings other) =>
        string.Equals(FontFamily, other.FontFamily, StringComparison.OrdinalIgnoreCase) &&
        FontPx == other.FontPx &&
        Bold == other.Bold;
}

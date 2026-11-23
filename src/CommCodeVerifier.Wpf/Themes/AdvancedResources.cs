using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommCodeVerifier.Wpf.Settings;

namespace CommCodeVerifier.Wpf.Themes;

/// <summary>
/// Расширенные настройки оформления — ресурсы приложения. Элементы берут их через
/// DynamicResource, поэтому новые значения применяются сразу ко всей программе.
/// Значения по умолчанию — в Fonts.xaml и Styles.xaml.
/// </summary>
public static class AdvancedResources
{
    public const string UiFontFamily = "FontFamily.Ui";
    public const string TitleFontFamily = "FontFamily.Title";
    public const string TitleFont = "Font.Title";
    public const string TabRadius = "Radius.Tab";
    public const string SectionRadius = "Radius.Section";
    public const string ButtonRadius = "Radius.Button";

    // Шрифт стиля, если в нём нет сеттера.
    private const string FallbackFontFamily = "Segoe UI";
    private const double FallbackFontSize = 16;

    public static void Apply(AdvancedUiSettings s)
    {
        s.Normalize();
        var r = Application.Current.Resources;
        r[UiFontFamily] = new FontFamily(s.UiFontFamily);
        r[TitleFontFamily] = new FontFamily(s.TitleFontFamily);
        r[TitleFont] = (double)s.TitleFontPx;
        r[TabRadius] = (double)s.TabRadius;
        r[SectionRadius] = (double)s.SectionRadius;
        r[ButtonRadius] = new CornerRadius(s.ButtonRadius);

        ApplySectionFonts(s.SectionFonts);
    }

    /// <summary>
    /// Шрифт раздела по умолчанию — из его стиля в Styles.xaml (ключ — TextStyleKey раздела).
    /// Берётся исходный стиль, а не переопределённый настройками.
    /// </summary>
    public static SectionFontSettings DefaultSectionFont(string styleKey)
    {
        var font = new SectionFontSettings
        {
            FontFamily = FallbackFontFamily,
            FontPx = (int)FallbackFontSize,
            Bold = false
        };

        if (OriginalStyle(styleKey) is not { } style)
            return font;

        foreach (var setter in style.Setters.OfType<Setter>())
        {
            if (setter.Property == TextBlock.FontFamilyProperty && setter.Value is FontFamily family)
                font.FontFamily = family.Source;
            else if (setter.Property == TextBlock.FontSizeProperty && setter.Value is double size)
                font.FontPx = (int)Math.Round(size);
            else if (setter.Property == TextBlock.FontWeightProperty && setter.Value is FontWeight weight)
                font.Bold = weight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight();
        }

        return font;
    }

    /// <summary>
    /// Переопределяет стили разделов, для которых в настройках задан свой шрифт:
    /// новый стиль на основе исходного кладётся в ресурсы приложения под тем же ключом
    /// (они важнее словаря Styles.xaml). Для остальных разделов переопределение снимается.
    /// </summary>
    private static void ApplySectionFonts(IReadOnlyDictionary<string, SectionFontSettings> fonts)
    {
        var resources = Application.Current.Resources;

        // Снять прежние переопределения — разделы без настроек вернутся к Styles.xaml.
        foreach (var key in resources.Keys.OfType<string>().ToList())
        {
            if (resources[key] is Style { BasedOn: { } basedOn } &&
                ReferenceEquals(basedOn, OriginalStyle(key)))
            {
                resources.Remove(key);
            }
        }

        foreach (var (key, font) in fonts)
        {
            if (OriginalStyle(key) is not { } original)
                continue; // раздела больше нет — настройка остаётся в файле, но не применяется

            var style = new Style(typeof(TextBlock), original);
            style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily(font.FontFamily)));
            style.Setters.Add(new Setter(TextBlock.FontSizeProperty, (double)font.FontPx));
            style.Setters.Add(new Setter(TextBlock.FontWeightProperty,
                font.Bold ? FontWeights.Bold : FontWeights.Normal));
            style.Seal();
            resources[key] = style;
        }
    }

    /// Исходный стиль из подключённых словарей (Styles.xaml), без переопределений.
    private static Style? OriginalStyle(string key)
    {
        foreach (var dictionary in Application.Current.Resources.MergedDictionaries)
        {
            if (dictionary.Contains(key) && dictionary[key] is Style style)
                return style;
        }

        return null;
    }
}

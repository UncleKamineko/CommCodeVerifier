using System.Windows.Media;
using CommCodeVerifier.Wpf.Settings;
using CommCodeVerifier.Wpf.Themes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeVerifier.Wpf.ViewModels;

/// Раздел навигации для настройки его шрифта: название и ключ стиля в Styles.xaml.
public sealed record SectionStyleInfo(string Title, string StyleKey);

/// <summary>
/// Шрифт названия одного раздела в окне «Расширенные настройки».
/// Значения по умолчанию — из стиля раздела в Styles.xaml.
/// </summary>
public sealed partial class SectionFontItem : ObservableObject
{
    [ObservableProperty] private string _fontFamily;
    [ObservableProperty] private int _fontPx;
    [ObservableProperty] private bool _bold;

    public string Title { get; }
    public string StyleKey { get; }
    public SectionFontSettings Default { get; }

    public string DefaultText =>
        $"по умолчанию {Default.FontFamily}, {Default.FontPx}{(Default.Bold ? ", полужирный" : "")}";

    public SectionFontItem(SectionStyleInfo section, SectionFontSettings defaults, SectionFontSettings current)
    {
        Title = section.Title;
        StyleKey = section.StyleKey;
        Default = defaults;
        _fontFamily = current.FontFamily;
        _fontPx = current.FontPx;
        _bold = current.Bold;
    }

    public SectionFontSettings ToSettings() => new()
    {
        FontFamily = FontFamily,
        FontPx = AdvancedUiSettings.ClampFont(FontPx),
        Bold = Bold
    };

    public void Reset()
    {
        FontFamily = Default.FontFamily;
        FontPx = Default.FontPx;
        Bold = Default.Bold;
    }
}

/// Окно «Расширенные настройки»: шрифты интерфейса, заголовка и разделов, радиусы скругления.
/// Применяются только после «Сохранить».
public sealed partial class AdvancedSettingsViewModel : ObservableObject
{
    [ObservableProperty] private string _uiFontFamily = AdvancedUiSettings.DefaultUiFontFamily;
    [ObservableProperty] private string _titleFontFamily = AdvancedUiSettings.DefaultTitleFontFamily;
    [ObservableProperty] private int _titleFontPx;
    [ObservableProperty] private int _tabRadius;
    [ObservableProperty] private int _sectionRadius;
    [ObservableProperty] private int _buttonRadius;

    /// Разделы навигации в порядке отображения — каждый со своим шрифтом.
    public IReadOnlyList<SectionFontItem> SectionFonts { get; }

    /// Шрифты, установленные в системе, по алфавиту.
    public IReadOnlyList<string> FontFamilies { get; }

    public AdvancedSettingsViewModel(AdvancedUiSettings s, IReadOnlyList<SectionStyleInfo> sections)
    {
        s.Normalize();
        UiFontFamily = s.UiFontFamily;
        TitleFontFamily = s.TitleFontFamily;
        TitleFontPx = s.TitleFontPx;
        TabRadius = s.TabRadius;
        SectionRadius = s.SectionRadius;
        ButtonRadius = s.ButtonRadius;

        SectionFonts = sections
            .Select(section =>
            {
                var defaults = AdvancedResources.DefaultSectionFont(section.StyleKey);
                var current = s.SectionFonts.TryGetValue(section.StyleKey, out var saved) ? saved : defaults;
                return new SectionFontItem(section, defaults, current);
            })
            .ToList();

        // Сохранённый шрифт может отсутствовать на этом компьютере — он всё равно в списке.
        FontFamilies = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .Concat(new[] { UiFontFamily, TitleFontFamily })
            .Concat(SectionFonts.Select(item => item.FontFamily))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        UiFontFamily = AdvancedUiSettings.DefaultUiFontFamily;
        TitleFontFamily = AdvancedUiSettings.DefaultTitleFontFamily;
        TitleFontPx = AdvancedUiSettings.DefaultTitleFont;
        TabRadius = AdvancedUiSettings.DefaultTabRadius;
        SectionRadius = AdvancedUiSettings.DefaultSectionRadius;
        ButtonRadius = AdvancedUiSettings.DefaultButtonRadius;

        foreach (var item in SectionFonts)
            item.Reset();
    }

    public void ApplyTo(AdvancedUiSettings s)
    {
        s.UiFontFamily = UiFontFamily;
        s.TitleFontFamily = TitleFontFamily;
        s.TitleFontPx = TitleFontPx;
        s.TabRadius = TabRadius;
        s.SectionRadius = SectionRadius;
        s.ButtonRadius = ButtonRadius;

        // Сохраняются только отличия от Styles.xaml: изменённое там значение по умолчанию
        // подействует на все разделы, которые в настройках не меняли.
        foreach (var item in SectionFonts)
        {
            var font = item.ToSettings();
            if (font.SameAs(item.Default))
                s.SectionFonts.Remove(item.StyleKey);
            else
                s.SectionFonts[item.StyleKey] = font;
        }

        s.Normalize();
    }
}

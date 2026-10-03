using CommCodeVerifier.Wpf.Settings;
using CommCodeVerifier.Wpf.Themes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeVerifier.Wpf.ViewModels;

/// Окно «Настройки программы»: размеры шрифтов и цветовая схема. Применяются только после «Сохранить».
public sealed partial class ProgramSettingsViewModel : ObservableObject
{
    [ObservableProperty] private int _buttonFontPx;
    [ObservableProperty] private int _tabTextFontPx;
    [ObservableProperty] private int _codeFontPx;
    [ObservableProperty] private int _rulesFontPx;
    [ObservableProperty] private int _summaryFontPx;
    [ObservableProperty] private string _colorScheme = ColorSchemes.DefaultId;

    /// Доступные схемы — из Themes/ColorSchemes.xaml.
    public IReadOnlyList<ColorSchemeInfo> ColorSchemeOptions { get; } = ColorSchemes.All;

    public ProgramSettingsViewModel(UiSettings s)
    {
        ButtonFontPx = s.ButtonFontPx;
        TabTextFontPx = s.TabTextFontPx;
        CodeFontPx = s.CodeFontPx;
        RulesFontPx = s.RulesFontPx;
        SummaryFontPx = s.SummaryFontPx;
        ColorScheme = ColorSchemes.Resolve(s.ColorScheme);
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        ButtonFontPx = UiSettings.DefaultButtonFont;
        TabTextFontPx = UiSettings.DefaultTabTextFont;
        CodeFontPx = UiSettings.DefaultCodeFont;
        RulesFontPx = UiSettings.DefaultRulesFont;
        SummaryFontPx = UiSettings.DefaultSummaryFont;
        ColorScheme = ColorSchemes.DefaultId;
    }

    public void ApplyTo(UiSettings s)
    {
        s.ButtonFontPx = ButtonFontPx;
        s.TabTextFontPx = TabTextFontPx;
        s.CodeFontPx = CodeFontPx;
        s.RulesFontPx = RulesFontPx;
        s.SummaryFontPx = SummaryFontPx;
        s.ColorScheme = ColorSchemes.Resolve(ColorScheme);
        s.Normalize();
    }
}

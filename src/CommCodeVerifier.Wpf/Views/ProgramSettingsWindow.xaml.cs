using System.Windows;
using CommCodeVerifier.Wpf.ViewModels;

namespace CommCodeVerifier.Wpf.Views;

public partial class ProgramSettingsWindow : Window
{
    /// Размер текста окна — постоянный, не зависит от настроек шрифтов программы.
    public const double TextFontSize = 16;

    /// Размер текста на кнопках окна — постоянный, не зависит от настроек шрифтов программы.
    public const double ButtonFontSize = 18;

    public ProgramSettingsWindow(ProgramSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnSave(object sender, RoutedEventArgs e) => DialogResult = true;
}

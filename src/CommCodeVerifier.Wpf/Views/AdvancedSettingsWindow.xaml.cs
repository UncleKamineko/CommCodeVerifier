using System.Windows;
using CommCodeVerifier.Wpf.ViewModels;

namespace CommCodeVerifier.Wpf.Views;

/// <summary>
/// «Расширенные настройки» — оформление, которое обычно не меняют:
/// шрифты разделов и заголовка, радиусы скругления.
/// Открывается ссылкой из окна «Настройки программы».
/// </summary>
public partial class AdvancedSettingsWindow : Window
{
    public AdvancedSettingsWindow(AdvancedSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnSave(object sender, RoutedEventArgs e) => DialogResult = true;
}

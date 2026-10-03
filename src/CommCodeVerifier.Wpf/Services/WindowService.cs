using System.Windows;
using CommCodeVerifier.Wpf.Themes;
using CommCodeVerifier.Wpf.ViewModels;
using CommCodeVerifier.Wpf.Views;

namespace CommCodeVerifier.Wpf.Services;

public sealed class WindowService : IWindowService
{
    private readonly IShellService _shell;
    private readonly IDialogService _dialogs;
    private readonly ISettingsService _settings;
    private HelpWindow? _help;

    public WindowService(IShellService shell, IDialogService dialogs, ISettingsService settings)
    {
        _shell = shell;
        _dialogs = dialogs;
        _settings = settings;
    }

    public void ShowHelp()
    {
        if (_help != null)
        {
            if (_help.WindowState == WindowState.Minimized) _help.WindowState = WindowState.Normal;
            _help.Activate();
            return;
        }

        _help = new HelpWindow(_shell, _dialogs) { Owner = Application.Current.MainWindow };
        _help.Closed += (_, _) => _help = null;
        _help.Show();
    }

    public void ShowProgramSettings()
    {
        var vm = new ProgramSettingsViewModel(_settings.Ui);
        var window = new ProgramSettingsWindow(vm) { Owner = Application.Current.MainWindow };
        if (window.ShowDialog() != true) return;

        vm.ApplyTo(_settings.Ui);
        // Как в WinForms: шрифты и схема применяются и при ошибке записи.
        FontResources.Apply(_settings.Ui);
        ColorSchemes.Apply(_settings.Ui.ColorScheme);

        try { _settings.SaveUi(); }
        catch (Exception ex)
        {
            _dialogs.Error("Не удалось сохранить настройки программы:\n\n" + ex.Message +
                           "\n\nНовые настройки действуют до закрытия программы.");
        }
    }
    public void ShowSelfTest(string report)
    {
        var window = new SelfTestWindow(report, _shell, _dialogs) { Owner = Application.Current.MainWindow };
        window.ShowDialog();
    }
}

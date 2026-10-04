using System.Windows;
using System.Windows.Threading;
using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Services;
using CommCodeVerifier.Wpf.Themes;
using CommCodeVerifier.Wpf.ViewModels;

namespace CommCodeVerifier.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Как в WinForms: непредвиденная ошибка — сообщение, программа продолжает работу.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, ex) => ex.SetObserved();

        var dialogs = new DialogService();
        var settings = new SettingsService();

        if (settings.LoadError != null)
            dialogs.Warning("Не удалось прочитать настройки:\n\n" + settings.LoadError +
                            "\n\nБудут использованы значения по умолчанию.");

        try
        {
            ConfigRepository.EnsureDefaultFiles();
            ConfigRepository.Reload(settings.Processing);
        }
        catch (Exception ex)
        {
            dialogs.Warning("Не удалось загрузить файлы конфигурации:\n\n" + ex.Message +
                            "\n\nБудут использованы встроенные значения по умолчанию.");
        }

        FontResources.Apply(settings.Ui);
        ColorSchemes.Apply(settings.Ui.ColorScheme);

        var shell = new ShellService();
        var services = new AppServices(settings, dialogs, shell, new WindowService(shell, dialogs, settings));
        var window = new MainWindow(new MainViewModel(services), settings);
        MainWindow = window;

        // Замечания загрузки — после появления окна, чтобы сообщение было поверх него.
        EventHandler? shown = null;
        shown = (_, _) =>
        {
            window.ContentRendered -= shown;
            ShowLoadWarnings(dialogs);
        };
        window.ContentRendered += shown;

        window.Show();
    }

    private static void ShowLoadWarnings(IDialogService dialogs)
    {
        var warnings = ConfigRepository.Instance.LoadWarnings;
        if (warnings.Count == 0) return;
        dialogs.Warning("При загрузке файлов конфигурации возникли замечания:\n\n• " +
                        string.Join("\n• ", warnings));
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Цепочка сообщений: настоящая причина обычно во вложенном исключении.
        var messages = new List<string>();
        for (var ex = e.Exception; ex != null; ex = ex.InnerException)
            messages.Add(ex.Message);

        string details = e.Exception.ToString();
        try { Clipboard.SetText(details); } catch { /* буфер занят */ }

        MessageBox.Show("Непредвиденная ошибка:\n\n" + string.Join("\n→ ", messages) +
                        "\n\nПодробности скопированы в буфер обмена.",
            DialogService.AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

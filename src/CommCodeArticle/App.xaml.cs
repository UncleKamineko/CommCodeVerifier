using System.Windows;
using System.Windows.Threading;
using CommCodeArticle.Settings;
using CommCodeArticle.Themes;

namespace CommCodeArticle;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Непредвиденная ошибка: сообщение, программа продолжает работу.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var ui = new UiSettings();
        FontResources.Apply(ui);
        ColorSchemes.Apply(ui.ColorScheme);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show("Непредвиденная ошибка:\n\n" + e.Exception.Message,
            "Генерация артикула", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

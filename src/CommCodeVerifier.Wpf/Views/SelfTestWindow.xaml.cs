using System.Windows;
using System.Windows.Media;
using CommCodeVerifier.Wpf.Services;

namespace CommCodeVerifier.Wpf.Views;

/// Результат самопроверки (Ctrl+Shift+T): первая строка — итог, ниже — отчёт по кейсам.
public partial class SelfTestWindow : Window
{
    private readonly string _report;
    private readonly IShellService _shell;
    private readonly IDialogService _dialogs;

    public SelfTestWindow(string report, IShellService shell, IDialogService dialogs)
    {
        InitializeComponent();
        _report = report;
        _shell = shell;
        _dialogs = dialogs;

        var lines = report.Replace("\r\n", "\n").Split('\n');
        int failed = lines.Count(l => l.StartsWith("FAIL", StringComparison.Ordinal));

        HeaderText.Text = lines.FirstOrDefault(l => l.Length > 0) ?? "";
        if (failed > 0) HeaderText.Text += $"  —  ошибок: {failed}";
        HeaderText.Foreground = failed > 0 ? Brushes.Red : Brushes.DarkGreen;

        ReportBox.Text = report;
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (!_shell.CopyText(_report))
            _dialogs.Warning("Не удалось скопировать: буфер обмена занят другой программой.", Title);
    }
}

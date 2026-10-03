using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CommCodeVerifier.Wpf.Services;
using CommCodeVerifier.Wpf.ViewModels;

namespace CommCodeVerifier.Wpf;

public partial class MainWindow : Window
{
    public const string BusyOnCloseText =
        "Идёт обработка. Прервать её и закрыть программу?\n\nРезультаты текущей обработки не будут сохранены.";

    private readonly ISettingsService _settings;
    private readonly MainViewModel _vm;
    private bool _closeAllowed;

    public MainWindow(MainViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();
        DataContext = viewModel;
        _vm = viewModel;
        _settings = settings;

        RestorePlacement();
        Closing += OnClosing;
        StateChanged += (_, _) => UpdateWindowState();
        Activated += (_, _) => TitleText.Foreground = (System.Windows.Media.Brush)FindResource("Brush.TitleText");
        Deactivated += (_, _) => TitleText.Foreground = (System.Windows.Media.Brush)FindResource("Brush.TitleText.Inactive");
        Loaded += (_, _) => UpdateWindowState();
    }

    // ================= свой заголовок окна =================
    private void OnMinimize(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaximizeRestore(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }

    /// Через Close(): проходит прежняя проверка закрытия (идущая обработка, несохранённые настройки).
    private void OnClose(object sender, RoutedEventArgs e) => Close();

    /// Щелчок по значку — системное меню окна; двойной щелчок — закрыть (как в Windows).
    private void OnIconClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { Close(); return; }
        var p = PointToScreen(new Point(0, 32));
        SystemCommands.ShowSystemMenu(this, new Point(p.X / DpiScale().X, p.Y / DpiScale().Y));
    }

    private Point DpiScale()
    {
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        return new Point(dpi.DpiScaleX, dpi.DpiScaleY);
    }

    /// <summary>
    /// В развёрнутом виде Windows выносит рамку окна за пределы экрана —
    /// без отступа заголовок и края содержимого обрезаются.
    /// Значок кнопки: «развернуть» или «восстановить».
    /// </summary>
    private void UpdateWindowState()
    {
        bool max = WindowState == WindowState.Maximized;
        var frame = SystemParameters.WindowResizeBorderThickness;
        RootBorder.Padding = max
            ? new Thickness(frame.Left + 2, frame.Top + 2, frame.Right + 2, frame.Bottom + 2)
            : new Thickness(0);
        MaxButton.Content = max ? "\uE923" : "\uE922";
        MaxButton.ToolTip = max ? "Свернуть в окно" : "Развернуть";
    }
    // ================= закрытие =================
    /// <summary>
    /// Во время обработки — подтверждение, остановка и ожидание её завершения;
    /// затем — несохранённые настройки анализа.
    /// </summary>
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeAllowed)
        {
            SavePlacement();
            return;
        }

        if (_vm.Processing.IsBusy)
        {
            e.Cancel = true;
            if (!_vm.Services.Dialogs.Confirm(BusyOnCloseText)) return;

            IsEnabled = false;
            Cursor = System.Windows.Input.Cursors.Wait;
            try { await _vm.StopProcessingAsync(); }
            finally
            {
                Cursor = null;
                IsEnabled = true;
            }

            if (!_vm.ConfirmUnsavedSettingsOnClose()) return;
            _closeAllowed = true;
            Close();
            return;
        }

        if (!_vm.ConfirmUnsavedSettingsOnClose())
        {
            e.Cancel = true;
            return;
        }
        SavePlacement();
    }

     // ================= размер и положение окна =================
    private void RestorePlacement()
    {
        var u = _settings.Ui;
        if (u.WindowLeft is double l && u.WindowTop is double t &&
            u.WindowWidth is double w && u.WindowHeight is double h)
        {
            var wanted = new Rect(l, t, w, h);
            var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                                  SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            var visible = Rect.Intersect(wanted, screen);

            // Окно восстанавливается, только если заметная его часть видна на одном из мониторов
            // (монитор могли отключить).
            if (!visible.IsEmpty && visible.Width >= 100 && visible.Height >= 50)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = l;
                Top = t;
                Width = Math.Max(w, MinWidth);
                Height = Math.Max(h, MinHeight);
            }
        }
        if (u.WindowMaximized) WindowState = WindowState.Maximized;
    }

    private void SavePlacement()
    {
        var u = _settings.Ui;
        var b = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (!b.IsEmpty)
        {
            u.WindowLeft = b.Left;
            u.WindowTop = b.Top;
            u.WindowWidth = b.Width;
            u.WindowHeight = b.Height;
        }
        u.WindowMaximized = WindowState == WindowState.Maximized;

        try { _settings.SaveUi(); }
        catch { /* положение окна не должно мешать закрытию программы */ }
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {

    }

    private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {

    }

    private void ImCheckView_Loaded(object sender, RoutedEventArgs e)
    {

    }

    private void SingleCheckView_Loaded(object sender, RoutedEventArgs e)
    {

    }

    private void HelpButton_Click_1(object sender, RoutedEventArgs e)
    {

    }

    private void PrefsButton_Click(object sender, RoutedEventArgs e)
    {

    }
}

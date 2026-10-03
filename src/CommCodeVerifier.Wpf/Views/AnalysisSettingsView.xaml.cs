using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CommCodeVerifier.Wpf.Views;

public partial class AnalysisSettingsView : UserControl
{
    public AnalysisSettingsView()
    {
        InitializeComponent();
        PreviewMouseWheel += OnPreviewMouseWheel;
    }

    /// <summary>
    /// Поля ввода поглощают колесо мыши своей (скрытой) прокруткой.
    /// Над полями колесо прокручивает вкладку целиком.
    /// </summary>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!IsInsideTextBox(e.OriginalSource as DependencyObject)) return;

        e.Handled = true;
        OuterScroll.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source = OuterScroll
        });
    }

    private static bool IsInsideTextBox(DependencyObject? d)
    {
        while (d != null)
        {
            if (d is TextBox) return true;
            if (d is ScrollViewer sv && sv.Name == nameof(OuterScroll)) return false;
            d = d is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(d)
                : LogicalTreeHelper.GetParent(d);
        }
        return false;
    }
}

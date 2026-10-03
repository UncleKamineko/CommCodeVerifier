using System.Windows;

namespace CommCodeVerifier.Wpf.Controls;

/// Радиус скругления кнопки: у Button в WPF такого свойства нет — задаётся через этот класс.
public static class ButtonAssist
{
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.RegisterAttached("CornerRadius", typeof(CornerRadius), typeof(ButtonAssist),
            new FrameworkPropertyMetadata(new CornerRadius(4)));

    public static CornerRadius GetCornerRadius(DependencyObject d) => (CornerRadius)d.GetValue(CornerRadiusProperty);
    public static void SetCornerRadius(DependencyObject d, CornerRadius v) => d.SetValue(CornerRadiusProperty, v);
}

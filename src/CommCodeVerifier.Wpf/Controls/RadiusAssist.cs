using System.Windows;

namespace CommCodeVerifier.Wpf.Controls;

/// <summary>
/// Числовой радиус, используемый шаблонами вкладок и разделов.
/// </summary>
public static class RadiusAssist
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.RegisterAttached(
            "Value",
            typeof(double),
            typeof(RadiusAssist),
            new FrameworkPropertyMetadata(6.0));

    public static void SetValue(DependencyObject element, double value)
    {
        element.SetValue(ValueProperty, value);
    }

    public static double GetValue(DependencyObject element)
    {
        return (double)element.GetValue(ValueProperty);
    }
}

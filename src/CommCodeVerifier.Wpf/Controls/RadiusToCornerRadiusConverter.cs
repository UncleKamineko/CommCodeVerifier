using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CommCodeVerifier.Wpf.Controls;

/// <summary>
/// Преобразует одно числовое значение радиуса
/// в CornerRadius нужной формы.
/// </summary>
public sealed class RadiusToCornerRadiusConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        double radius = value switch
        {
            double d => d,
            float f => f,
            int i => i,
            _ => 0
        };

        string mode = parameter?.ToString() ?? "All";

        return mode switch
        {
            "TopOnly" => new CornerRadius(
                radius,
                radius,
                0,
                0),

            "LeftOnly" => new CornerRadius(
                radius,
                0,
                0,
                radius),

            "BottomOnly" => new CornerRadius(
                0,
                0,
                radius,
                radius),

            _ => new CornerRadius(radius)
        };
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

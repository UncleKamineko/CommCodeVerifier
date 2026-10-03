using System.Collections;
using System.Windows;
using System.Windows.Media;

namespace CommCodeVerifier.Wpf.Themes;

/// Цветовая схема для списка выбора: ключ, название, образец фона страницы.
public sealed record ColorSchemeInfo(string Id, string Name, Brush Preview);

/// <summary>
/// Цветовые схемы из Themes/ColorSchemes.xaml. Apply копирует кисти схемы («Brush.*»)
/// в ресурсы программы — все элементы с DynamicResource перекрашиваются сразу.
/// Кисти, которых нет в схеме, берутся из Colors.xaml.
/// </summary>
public static class ColorSchemes
{
    public const string DefaultId = "Light";

    private static readonly Uri SourceUri = new("pack://application:,,,/Themes/ColorSchemes.xaml");
    private static ResourceDictionary? _source;
    private static IReadOnlyList<ColorSchemeInfo>? _all;

    private static ResourceDictionary Source => _source ??= new ResourceDictionary { Source = SourceUri };

    /// Все схемы в порядке Schemes.Order.
    public static IReadOnlyList<ColorSchemeInfo> All => _all ??= Build();

    private static List<ColorSchemeInfo> Build()
    {
        var order = Source["Schemes.Order"] as string[] ?? Array.Empty<string>();
        var list = new List<ColorSchemeInfo>();
        foreach (string id in order)
        {
            if (Source[id] is not ResourceDictionary d) continue;
            string name = d["Name"] as string ?? id;
            var preview = d["Brush.Page.Background"] as Brush ?? Brushes.White;
            list.Add(new ColorSchemeInfo(id, name, preview));
        }
        return list;
    }

    /// Ключ схемы, если она есть; иначе — схема по умолчанию.
    public static string Resolve(string? id) =>
        id != null && All.Any(s => s.Id == id) ? id : DefaultId;

    public static void Apply(string? id)
    {
        if (Source[Resolve(id)] is not ResourceDictionary scheme) return;

        var target = Application.Current.Resources;
        foreach (DictionaryEntry e in scheme)
            if (e.Key is string key && key.StartsWith("Brush.", StringComparison.Ordinal))
                target[key] = e.Value;
    }
}

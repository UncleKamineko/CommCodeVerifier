using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CommCodeVerifier.Wpf.ViewModels;

namespace CommCodeVerifier.Wpf.Controls;

/// <summary>
/// Текст из фрагментов; отмеченные — цветом MarkBrush, полужирным.
/// Отмеченные пробелы и табуляции дополнительно получают фон MarkSpaceBrush.
/// </summary>
public sealed class HighlightedTextBlock : TextBlock
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(IReadOnlyList<TextSegment>), typeof(HighlightedTextBlock),
        new PropertyMetadata(null, (d, _) => ((HighlightedTextBlock)d).Rebuild()));

    public static readonly DependencyProperty MarkBrushProperty = DependencyProperty.Register(
        nameof(MarkBrush), typeof(Brush), typeof(HighlightedTextBlock),
        new PropertyMetadata(Brushes.Red, (d, _) => ((HighlightedTextBlock)d).Rebuild()));

    public static readonly DependencyProperty MarkSpaceBrushProperty = DependencyProperty.Register(
        nameof(MarkSpaceBrush), typeof(Brush), typeof(HighlightedTextBlock),
        new PropertyMetadata(null, (d, _) => ((HighlightedTextBlock)d).Rebuild()));

    public IReadOnlyList<TextSegment>? Segments
    {
        get => (IReadOnlyList<TextSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public Brush MarkBrush
    {
        get => (Brush)GetValue(MarkBrushProperty);
        set => SetValue(MarkBrushProperty, value);
    }

    public Brush? MarkSpaceBrush
    {
        get => (Brush?)GetValue(MarkSpaceBrushProperty);
        set => SetValue(MarkSpaceBrushProperty, value);
    }

    private void Rebuild()
    {
        Inlines.Clear();
        if (Segments == null) return;

        foreach (var s in Segments)
        {
            var run = new Run(s.Text);
            if (s.Marked)
            {
                run.Foreground = MarkBrush;
                run.FontWeight = FontWeights.Bold;
                if (MarkSpaceBrush != null && s.Text.Any(char.IsWhiteSpace))
                    run.Background = MarkSpaceBrush;
            }
            Inlines.Add(run);
        }
    }
}

using System.Windows;
using System.Windows.Controls;

namespace CommCodeVerifier.Wpf.Views;

public partial class SingleCheckView : UserControl
{
    public SingleCheckView()
    {
        InitializeComponent();
        DataObject.AddPastingHandler(InputBox, OnPaste);
    }

    /// <summary>
    /// Вставка из буфера: перевод строки в конце (копирование ячейки Excel) отбрасывается,
    /// из многострочного текста берётся первая строка — как однострочное поле WinForms.
    /// Табуляции и прочие символы внутри значения сохраняются: их обрабатывают правила.
    /// </summary>
    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text) return;

        string line = text.TrimEnd('\r', '\n');
        int nl = line.IndexOfAny(new[] { '\r', '\n' });
        if (nl >= 0) line = line[..nl];

        e.CancelCommand();
        InputBox.SelectedText = line;
        InputBox.CaretIndex = InputBox.SelectionStart + InputBox.SelectionLength;
        InputBox.SelectionLength = 0;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {

    }

    private void Button_Click_1(object sender, RoutedEventArgs e)
    {

    }
}

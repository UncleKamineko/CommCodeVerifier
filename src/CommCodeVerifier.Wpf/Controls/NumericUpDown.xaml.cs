using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CommCodeVerifier.Wpf.Controls;

/// <summary>
/// Поле целого числа со стрелками (аналог NumericUpDown WinForms).
/// Ввод — только цифры; значение вне диапазона приводится к границе при выходе
/// из поля или по Enter. ↑/↓ и колесо мыши — шаг 1.
/// </summary>
public partial class NumericUpDown : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int), typeof(NumericUpDown),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((NumericUpDown)d).ShowValue(), CoerceValue));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(int), typeof(NumericUpDown),
        new PropertyMetadata(0, (d, _) => d.CoerceValue(ValueProperty)));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(int), typeof(NumericUpDown),
        new PropertyMetadata(100, (d, _) => d.CoerceValue(ValueProperty)));

    public int Value { get => (int)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    private bool _sync;

    public NumericUpDown()
    {
        InitializeComponent();
        ShowValue();

        Box.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        DataObject.AddPastingHandler(Box, OnPaste);
        Box.TextChanged += OnTextChanged;
        Box.LostKeyboardFocus += (_, _) => Commit();
        Box.PreviewKeyDown += OnKeyDown;
        PreviewMouseWheel += OnWheel;
    }

    private static object CoerceValue(DependencyObject d, object value)
    {
        var c = (NumericUpDown)d;
        return Math.Clamp((int)value, c.Minimum, Math.Max(c.Minimum, c.Maximum));
    }

    /// Текст поля — по значению; если в поле уже то же число, текст не трогается (каретка не прыгает).
    private void ShowValue()
    {
        if (Box == null) return;
        if (int.TryParse(Box.Text, out int shown) && shown == Value) return;
        _sync = true;
        Box.Text = Value.ToString();
        _sync = false;
    }

    /// Во время ввода значение обновляется, только если число уже в диапазоне.
    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_sync) return;
        if (int.TryParse(Box.Text, out int v) && v >= Minimum && v <= Maximum) Value = v;
    }

    /// Ввод завершён: число приводится к диапазону, пустое или неверное — прежнее значение.
    private void Commit()
    {
        if (int.TryParse(Box.Text, out int v)) Value = v;   // CoerceValue приведёт к границе
        _sync = true;
        Box.Text = Value.ToString();
        _sync = false;
    }

    private void Step(int delta)
    {
        Commit();
        Value += delta;
        ShowValue();
    }

    private void OnUp(object sender, RoutedEventArgs e) => Step(+1);
    private void OnDown(object sender, RoutedEventArgs e) => Step(-1);

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up: Step(+1); e.Handled = true; break;
            case Key.Down: Step(-1); e.Handled = true; break;
            // Enter не помечается обработанным: его получает кнопка по умолчанию («Сохранить»).
            case Key.Enter: Commit(); break;
        }
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (!IsKeyboardFocusWithin) return;   // как в WinForms: колесо — только у поля с фокусом
        Step(e.Delta > 0 ? +1 : -1);
        e.Handled = true;
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        string? text = e.DataObject.GetData(DataFormats.UnicodeText) as string;
        if (string.IsNullOrEmpty(text) || !text.Trim().All(char.IsDigit)) e.CancelCommand();
    }
}

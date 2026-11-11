using System.Media;
using System.Windows;

namespace CommCodeVerifier.Wpf.Views;

/// <summary>
/// Вопрос с кнопками «Да» / «Нет».
/// «Нет», Esc и крестик закрывают окно с ответом «Нет».
/// </summary>
public partial class ConfirmWindow : Window
{
    public ConfirmWindow(string message, string title)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;

        Loaded += (_, _) =>
        {
            SystemSounds.Exclamation.Play();
            YesButton.Focus();
        };
    }

    private void OnYes(object sender, RoutedEventArgs e) => DialogResult = true;
}

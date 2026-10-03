using System.Media;
using System.Windows;
using System.Windows.Input;
using CommCodeVerifier.Wpf.Services;

namespace CommCodeVerifier.Wpf.Views;

/// <summary>
/// «На странице есть несохранённые изменения, выйти без сохранения?» (как YesSaveDialog WinForms).
/// Enter — сохранить; Esc и крестик — остаться на вкладке.
/// </summary>
public partial class SaveChangesWindow : Window
{
    public const string DefaultMessage = "На странице есть несохранённые изменения, выйти без сохранения?";

    public SaveChoice Choice { get; private set; } = SaveChoice.Cancel;

    public SaveChangesWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        Loaded += (_, _) =>
        {
            SystemSounds.Exclamation.Play();
            SaveButton.Focus();
        };
    }

    private void OnDiscard(object sender, RoutedEventArgs e) { Choice = SaveChoice.Discard; Close(); }
    private void OnSave(object sender, RoutedEventArgs e) { Choice = SaveChoice.Save; Close(); }
}

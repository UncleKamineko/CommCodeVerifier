using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace CommCodeVerifier.Wpf.Services;

public sealed class DialogService : IDialogService
{
    public const string AppTitle = "Проверка коммерческих кодов";

    /// Владелец окна: активное окно программы, иначе главное.
    private static Window? Owner
    {
        get
        {
            var app = Application.Current;
            if (app == null) return null;
            var w = app.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ?? app.MainWindow;
            return w is { IsLoaded: true } ? w : null;
        }
    }

    private static MessageBoxResult Show(string text, string? title, MessageBoxButton buttons, MessageBoxImage icon)
    {
        var owner = Owner;
        return owner != null
            ? MessageBox.Show(owner, text, title ?? AppTitle, buttons, icon)
            : MessageBox.Show(text, title ?? AppTitle, buttons, icon);
    }

    public void Info(string text, string? title = null) =>
        Show(text, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Warning(string text, string? title = null) =>
        Show(text, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void Error(string text, string? title = null) =>
        Show(text, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string text, string? title = null) =>
        Show(text, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// Временная реализация. На этапе 6 — окно с текстом и кнопками как у YesSaveDialog WinForms.
    public SaveChoice AskSaveChanges(string? message = null)
    {
        var w = new Views.SaveChangesWindow(message ?? Views.SaveChangesWindow.DefaultMessage);
        var owner = Owner;
        if (owner != null) w.Owner = owner;
        else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        w.ShowDialog();
        return w.Choice;
    }

    public string? OpenFile(string filter, string? initialDirectory = null, string? title = null)
    {
        var d = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        if (title != null) d.Title = title;
        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            d.InitialDirectory = initialDirectory;

        var owner = Owner;
        bool? ok = owner != null ? d.ShowDialog(owner) : d.ShowDialog();
        return ok == true ? d.FileName : null;
    }

    public string? PickFolder(string? initialDirectory = null, string? title = null)
    {
        var d = new OpenFolderDialog();   // .NET 8
        if (title != null) d.Title = title;
        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            d.InitialDirectory = initialDirectory;

        var owner = Owner;
        bool? ok = owner != null ? d.ShowDialog(owner) : d.ShowDialog();
        return ok == true ? d.FolderName : null;
    }
}

using System.Diagnostics;
using System.Windows;

namespace CommCodeVerifier.Wpf.Services;

public sealed class ShellService : IShellService
{
    public void OpenFolder(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });

    public void OpenFile(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    public void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    public bool CopyText(string text)
    {
        try { Clipboard.SetText(text); return true; }
        catch { return false; }   // буфер открыт другой программой (COMException)
    }
}

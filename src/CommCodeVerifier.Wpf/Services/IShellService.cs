namespace CommCodeVerifier.Wpf.Services;

/// Открытие папок, файлов и ссылок, буфер обмена.
public interface IShellService
{
    void OpenFolder(string path);
    void OpenFile(string path);
    void OpenUrl(string url);

    /// Текст в буфер обмена. false — буфер занят другой программой.
    bool CopyText(string text);
}

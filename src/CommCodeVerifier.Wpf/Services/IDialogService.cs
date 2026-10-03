namespace CommCodeVerifier.Wpf.Services;

/// Ответ на запрос о несохранённых изменениях.
public enum SaveChoice { Save, Discard, Cancel }

/// Сообщения и стандартные окна выбора. Модели представления не обращаются к окнам напрямую.
public interface IDialogService
{
    void Info(string text, string? title = null);
    void Warning(string text, string? title = null);
    void Error(string text, string? title = null);

    /// «Да» / «Нет».
    bool Confirm(string text, string? title = null);

    /// Несохранённые изменения: не сохранять / сохранить; Esc и крестик — отмена.
    SaveChoice AskSaveChanges(string? message = null);

    /// Выбор файла. null — отмена.
    string? OpenFile(string filter, string? initialDirectory = null, string? title = null);

    /// Выбор папки. null — отмена.
    string? PickFolder(string? initialDirectory = null, string? title = null);
}

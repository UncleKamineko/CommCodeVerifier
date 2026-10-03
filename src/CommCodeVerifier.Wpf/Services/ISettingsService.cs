using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Settings;

namespace CommCodeVerifier.Wpf.Services;

/// Настройки обработки (settings.json) и интерфейса (ui-settings.json).
public interface ISettingsService
{
    ProcessingSettings Processing { get; }
    UiSettings Ui { get; }

    /// Ошибка чтения при запуске (повреждённый файл) — null, если всё прочитано.
    string? LoadError { get; }

    /// Сохранение настроек обработки. Ошибка записи — исключение.
    void SaveProcessing();

    /// Сохранение настроек интерфейса. Ошибка записи — исключение.
    void SaveUi();
}

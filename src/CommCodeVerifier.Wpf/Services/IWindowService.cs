namespace CommCodeVerifier.Wpf.Services;

/// Окна программы. Модели представления открывают окна только через этот сервис.
public interface IWindowService
{
    /// Справка: открыть или активировать уже открытое окно.
    void ShowHelp();

    /// Настройки программы (модальное окно). После «Сохранить» — запись и новые шрифты.
    void ShowProgramSettings();

    /// Результат самопроверки (модальное окно).
    void ShowSelfTest(string report);
}

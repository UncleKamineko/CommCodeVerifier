namespace CommCodeVerifier.Core;

/// <summary>
/// Пути программы. По умолчанию всё рядом с exe (для однофайловой сборки
/// AppContext.BaseDirectory — папка exe). Тесты переопределяют папки конфигурации и результатов.
/// </summary>
public static class AppPaths
{
    private static string? _configFolder;
    private static string? _resultsFolder;

    /// Папка программы.
    public static string BaseFolder { get; set; } = AppContext.BaseDirectory;

    /// «Файлы конфигурации».
    public static string ConfigFolder
    {
        get => _configFolder ?? Path.Combine(BaseFolder, "Файлы конфигурации");
        set => _configFolder = value;
    }

    /// «Результаты обработки».
    public static string ResultsFolder
    {
        get => _resultsFolder ?? Path.Combine(BaseFolder, "Результаты обработки");
        set => _resultsFolder = value;
    }

    /// Файл настроек.
    public static string SettingsFile => Path.Combine(BaseFolder, "settings.json");
}

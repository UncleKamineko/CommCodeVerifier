using System.Globalization;
using System.Runtime.CompilerServices;
using Xunit;

// Пути, культура и ConfigRepository.Instance — общие для всех тестов: параллельный запуск отключён.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CommCodeVerifier.Core.Tests;

internal static class TestSetup
{
    public static string ConfigDir => Path.Combine(AppContext.BaseDirectory, "Config");
    public static string GoldenDir => Path.Combine(AppContext.BaseDirectory, "Golden");
    public static string TempRoot => Path.Combine(Path.GetTempPath(), "CommCodeVerifier.Tests");

    private static string? _loadedKey;
    private static ConfigRepository? _loadedInstance;

    /// <summary>
    /// Культура ru-RU: на агентах GitHub английская локаль, а ClosedXML
    /// форматирует числовые ячейки по текущей культуре (1,5 / 1.5).
    /// Конфигурация — снимок из Config, результаты — во временную папку.
    /// </summary>
    [ModuleInitializer]
    internal static void Init()
    {
        var ru = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentCulture = ru;
        CultureInfo.DefaultThreadCurrentUICulture = ru;
        CultureInfo.CurrentCulture = ru;
        CultureInfo.CurrentUICulture = ru;

        AppPaths.ConfigFolder = ConfigDir;
        AppPaths.ResultsFolder = Path.Combine(TempRoot, "Результаты обработки");
    }

    /// <summary>
    /// Настройки тестов: Config/settings.json, иначе Golden/settings.json, иначе по умолчанию.
    /// Пути к файлам сбрасываются: конфигурация всегда читается из снимка Config.
    /// </summary>
    public static ProcessingSettings BaseSettings()
    {
        string? file = new[]
        {
            Path.Combine(ConfigDir, "settings.json"),
            Path.Combine(GoldenDir, "settings.json")
        }.FirstOrDefault(File.Exists);

        var s = file == null ? new ProcessingSettings() : ProcessingSettings.FromJson(File.ReadAllText(file));
        ClearPaths(s);
        return s;
    }

    /// Пути из settings.json указывают на файлы компьютера, где он создан, — в тестах не используются.
    public static void ClearPaths(ProcessingSettings s)
    {
        s.WordsFilePath = null;
        s.EndingsFilePath = null;
        s.ExclusionsFilePath = null;
        s.ForceDeleteWordsFilePath = null;
        s.CharMapFilePath = null;
        s.ImRulesFilePath = null;
        s.ImArticleRulesFilePath = null;
    }

    /// <summary>
    /// Загружает конфигурацию по настройкам и возвращает параметры обработки.
    /// Повторная загрузка — только если изменились настройки или Instance подменён другим тестом.
    /// </summary>
    public static ProcessOptions UseConfig(ProcessingSettings s)
    {
        string key = s.ToJson();
        if (key != _loadedKey || !ReferenceEquals(_loadedInstance, ConfigRepository.Instance))
        {
            _loadedInstance = ConfigRepository.Reload(s);
            _loadedKey = key;
        }
        return s.ToProcessOptions();
    }

    /// Новая временная папка для результатов одного теста.
    public static string NewTempFolder(string name)
    {
        string path = Path.Combine(TempRoot, name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

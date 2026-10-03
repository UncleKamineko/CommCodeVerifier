using System.IO;
using System.Text;
using System.Text.Json;
using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Settings;

namespace CommCodeVerifier.Wpf.Services;

public sealed class SettingsService : ISettingsService
{
    public static string ProcessingFile => AppPaths.SettingsFile;
    public static string UiFile => Path.Combine(AppPaths.BaseFolder, "ui-settings.json");

    private static readonly JsonSerializerOptions UiJson = new() { WriteIndented = true };

    public ProcessingSettings Processing { get; }
    public UiSettings Ui { get; }
    public string? LoadError { get; }

    public SettingsService()
    {
        var errors = new List<string>();
        Processing = LoadProcessing(errors);
        Ui = LoadUi(errors);
        LoadError = errors.Count == 0 ? null : string.Join("\n", errors);
    }

    public void SaveProcessing() => WriteAtomic(ProcessingFile, Processing.ToJson());

    public void SaveUi()
    {
        Ui.Normalize();
        WriteAtomic(UiFile, JsonSerializer.Serialize(Ui, UiJson));
    }

    private static ProcessingSettings LoadProcessing(List<string> errors)
    {
        if (!File.Exists(ProcessingFile)) return new ProcessingSettings();
        try { return ProcessingSettings.FromJson(File.ReadAllText(ProcessingFile)); }
        catch (Exception ex)
        {
            errors.Add($"{Path.GetFileName(ProcessingFile)}: {ex.Message}");
            return new ProcessingSettings();
        }
    }

    /// <summary>
    /// ui-settings.json; если его нет — размеры шрифтов из settings.json WinForms 0.9.0
    /// (имена полей совпадают, остальные поля игнорируются).
    /// </summary>
    private static UiSettings LoadUi(List<string> errors)
    {
        string? file = File.Exists(UiFile) ? UiFile
                     : File.Exists(ProcessingFile) ? ProcessingFile
                     : null;
        if (file == null) return new UiSettings();

        try
        {
            var s = JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(file), UiJson) ?? new UiSettings();
            s.Normalize();
            return s;
        }
        catch (Exception ex)
        {
            // Повреждённый settings.json уже попал в ошибки при чтении настроек обработки.
            if (file == UiFile) errors.Add($"{Path.GetFileName(UiFile)}: {ex.Message}");
            return new UiSettings();
        }
    }

    /// Запись через временный файл: при сбое прежний файл настроек остаётся целым.
    private static void WriteAtomic(string path, string text)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, text, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }
}

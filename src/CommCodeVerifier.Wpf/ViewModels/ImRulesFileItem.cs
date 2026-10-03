using System.IO;
using CommCodeVerifier.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CommCodeVerifier.Wpf.ViewModels;

public enum ImRulesFileState { Default, Ok, Missing, NoRules }

/// <summary>
/// Строка выбора файла правил ИМ: путь (свой или по умолчанию) и число загруженных правил.
/// Путь хранится прямо в настройках обработки: выбор сохраняется сразу (как в WinForms).
/// </summary>
public sealed class ImRulesFileItem : ObservableObject
{
    private readonly Func<string> _defaultPath;
    private readonly Func<string?> _getCustom;
    private readonly Action<string?> _setCustom;
    private readonly Func<int> _rulesCount;

    public ImRulesFileItem(string caption, Func<string> defaultPath,
        Func<string?> getCustom, Action<string?> setCustom, Func<int> rulesCount)
    {
        Caption = caption;
        _defaultPath = defaultPath;
        _getCustom = getCustom;
        _setCustom = setCustom;
        _rulesCount = rulesCount;
        Refresh();
    }

    public string Caption { get; }
    public string DisplayText { get; private set; } = "";
    public ImRulesFileState State { get; private set; }

    public string? CustomPath
    {
        get => string.IsNullOrWhiteSpace(_getCustom()) ? null : _getCustom();
        set => _setCustom(value);
    }

    /// Начальная папка окна выбора: папка текущего файла, иначе папка конфигурации.
    public string InitialDirectory
    {
        get
        {
            string? dir = CustomPath != null ? Path.GetDirectoryName(CustomPath) : null;
            return dir != null && Directory.Exists(dir) ? dir : ConfigRepository.DefaultConfigFolder;
        }
    }

    /// Перечитать состояние после смены пути или перезагрузки конфигурации.
    public void Refresh()
    {
        string? custom = CustomPath;
        int count = _rulesCount();

        string text = custom ?? "по умолчанию: " + _defaultPath();
        var state = custom == null ? ImRulesFileState.Default : ImRulesFileState.Ok;

        if (custom != null && !File.Exists(custom))
        {
            text += " (файл не найден!)";
            state = ImRulesFileState.Missing;
        }

        text += count == 0 ? " — правил не загружено!" : $" — правил: {count}";
        if (count == 0) state = ImRulesFileState.NoRules;

        DisplayText = text;
        State = state;
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(State));
    }
}

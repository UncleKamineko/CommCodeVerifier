using System.IO;
using CommCodeVerifier.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CommCodeVerifier.Wpf.ViewModels;

/// Флажок правила на вкладке «Настройки анализа».
public sealed partial class RuleItem : ObservableObject
{
    private readonly Action<RuleItem> _changed;

    public RuleItem(RuleId rule, Action<RuleItem> changed)
    {
        Rule = rule;
        Name = RuleCatalog.ShortName(rule);
        ShowLinkedNote = rule == RuleId.TrailingEnding;
        _changed = changed;
    }

    public RuleId Rule { get; }
    public string Name { get; }

    /// Пояснение о совместном переключении правил 7 и 8 — под правилом «Удаление окончания».
    public bool ShowLinkedNote { get; }

    [ObservableProperty] private bool _isChecked;

    partial void OnIsCheckedChanged(bool value) => _changed(this);
}

public enum ConfigFileState { Default, Ok, Missing }

/// Строка выбора файла конфигурации: свой путь или файл по умолчанию.
public sealed partial class ConfigFileItem : ObservableObject
{
    private readonly Func<string> _defaultPath;

    public ConfigFileItem(string caption, string filter, Func<string> defaultPath)
    {
        Caption = caption;
        Filter = filter;
        _defaultPath = defaultPath;
    }

    public string Caption { get; }
    public string Filter { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText), nameof(State))]
    private string? _path;

    /// Путь для сохранения: null — файл по умолчанию.
    public string? PathOrNull => string.IsNullOrWhiteSpace(Path) ? null : Path;

    public ConfigFileState State =>
        PathOrNull == null ? ConfigFileState.Default
        : File.Exists(PathOrNull) ? ConfigFileState.Ok
        : ConfigFileState.Missing;

    public string DisplayText => State switch
    {
        ConfigFileState.Default => "по умолчанию: " + _defaultPath(),
        ConfigFileState.Missing => PathOrNull + " (файл не найден!)",
        _ => PathOrNull!
    };

    /// Начальная папка окна выбора: папка текущего файла, иначе папка конфигурации.
    public string InitialDirectory
    {
        get
        {
            string? dir = PathOrNull != null ? System.IO.Path.GetDirectoryName(PathOrNull) : null;
            return dir != null && Directory.Exists(dir) ? dir : ConfigRepository.DefaultConfigFolder;
        }
    }

    /// Файл могли создать или удалить — перечитать состояние.
    public void Refresh()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(DisplayText));
    }
}

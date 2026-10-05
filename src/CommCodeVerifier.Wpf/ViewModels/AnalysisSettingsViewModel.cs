using System.Collections.ObjectModel;
using System.IO;
using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeVerifier.Wpf.ViewModels;

/// Вкладка «Настройки анализа».
public sealed partial class AnalysisSettingsViewModel : ObservableObject
{
    public const string WarnPartial = "ВНИМАНИЕ! Выбраны не все правила, анализ может быть не полный!";
    public const string WarnEmpty = "ВНИМАНИЕ! Не выбрано ни одного правила — переход на другие вкладки заблокирован!";
    public const string NeedOneRuleText = "Выберите как минимум одно правило для анализа";
    public const string LockedText = "Идёт обработка — сохранение настроек станет доступно после её завершения";
    private const string Title = "Настройки анализа";

    private const string TxtFilter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";
    private const string XlsxFilter = "Файлы Excel (*.xlsx)|*.xlsx";

    private readonly AppServices _services;
    private readonly ProcessingState _state;
    /// Файлы конфигурации — по отдельности, для привязки строк блока «Файлы конфигураций».
    public ConfigFileItem WordsFile { get; }
    public ConfigFileItem EndingsFile { get; }
    public ConfigFileItem ExclusionsFile { get; }
    public ConfigFileItem ForceDeleteFile { get; }
    private bool _suppress;

    public AnalysisSettingsViewModel(AppServices services, ProcessingState state)
    {
        _services = services;
        _state = state;
        _state.PropertyChanged += (_, _) =>
        {
            SaveCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(IsLocked));
        };

        foreach (var rule in RuleCatalog.DisplayOrder)
            Rules.Add(new RuleItem(rule, OnRuleToggled));

        WordsFile = new("Значащие слова", TxtFilter, () => ConfigRepository.DefaultWordsPath);
        EndingsFile = new("Удаляемые окончания", TxtFilter, () => ConfigRepository.DefaultEndingsPath);
        ExclusionsFile = new("Артикулы-исключения", XlsxFilter, () => ConfigRepository.DefaultExclusionsPath);
        ForceDeleteFile = new("Слова удаления кода", TxtFilter, () => ConfigRepository.DefaultForceDeleteWordsPath);
        Files = new ObservableCollection<ConfigFileItem> { WordsFile, EndingsFile, ExclusionsFile, ForceDeleteFile };
        LoadFromSettings();
        RefreshRulesText();
    }

    public ObservableCollection<RuleItem> Rules { get; } = new();
    public ObservableCollection<ConfigFileItem> Files { get; }

    /// Порядок — как MorphologyMode.
    public IReadOnlyList<string> MorphologyOptions { get; } = new[]
    {
        "Нет (точное совпадение)",
        "Да, парадигмы (рекомендуется)",
        "Да, по корням"
    };

    [ObservableProperty] private int _morphologyIndex;
    [ObservableProperty] private string _extra1CChars = "";
    [ObservableProperty] private string _cyrillicExceptions = "";
    [ObservableProperty] private string _endingKeepSeries = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRulesWarning))]
    private string? _rulesWarning;

    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private bool _savedVisible;
    [ObservableProperty] private string _rulesText = "";

    public bool HasRulesWarning => RulesWarning != null;
    public int SelectedRulesCount => Rules.Count(r => r.IsChecked);

    partial void OnMorphologyIndexChanged(int value) => MarkDirty();
    partial void OnExtra1CCharsChanged(string value) => MarkDirty();
    partial void OnCyrillicExceptionsChanged(string value) => MarkDirty();
    partial void OnEndingKeepSeriesChanged(string value) => MarkDirty();

    // ================= правила =================
    /// Связанные правила (7 и 8) переключаются только вместе.
    private void OnRuleToggled(RuleItem item)
    {
        if (_suppress) return;

        _suppress = true;
        try
        {
            foreach (var group in RuleCatalog.LinkedGroups)
            {
                if (!group.Contains(item.Rule)) continue;
                foreach (var other in Rules.Where(r => r != item && group.Contains(r.Rule)))
                    other.IsChecked = item.IsChecked;
            }
        }
        finally { _suppress = false; }

        UpdateWarning();
        MarkDirty();
    }

    [RelayCommand] private void SelectAll() => SetAllRules(true);
    [RelayCommand] private void SelectNone() => SetAllRules(false);

    private void SetAllRules(bool state)
    {
        _suppress = true;
        try { foreach (var r in Rules) r.IsChecked = state; }
        finally { _suppress = false; }
        UpdateWarning();
        MarkDirty();
    }

    private void UpdateWarning()
    {
        int n = SelectedRulesCount;
        RulesWarning = n == 0 ? WarnEmpty : n < Rules.Count ? WarnPartial : null;
    }

    // ================= файлы =================
    [RelayCommand]
    private void PickFile(ConfigFileItem? item)
    {
        if (item == null) return;
        string? path = _services.Dialogs.OpenFile(item.Filter, item.InitialDirectory, item.Caption);
        if (path == null) return;
        item.Path = path;
        MarkDirty();
    }

    [RelayCommand]
    private void ResetFiles()
    {
        foreach (var f in Files) f.Path = null;
        MarkDirty();
    }

    [RelayCommand]
    private void OpenConfigFolder()
    {
        try
        {
            Directory.CreateDirectory(ConfigRepository.DefaultConfigFolder);
            _services.Shell.OpenFolder(ConfigRepository.DefaultConfigFolder);
        }
        catch (Exception ex)
        {
            _services.Dialogs.Warning("Не удалось открыть папку:\n" + ex.Message, Title);
        }
    }

    // ================= текст правил =================
    public void RefreshRulesText() =>
        RulesText = ConfigRepository.Instance.RulesText
            .Replace("\r\n", "\n").Replace("\n", Environment.NewLine);

    /// Перечитать файлы конфигурации по сохранённым настройкам.
    public void ReloadConfiguration()
    {
        ConfigRepository.Reload(_services.Settings.Processing);
        RefreshRulesText();
    }

    // ================= сохранение / откат =================
    private void MarkDirty()
    {
        if (_suppress) return;
        IsDirty = true;
        SavedVisible = false;
    }

    /// Идёт обработка — сохранение недоступно (конфигурация не перечитывается посреди анализа).
    public bool IsLocked => _state.IsBusy;

    private bool CanSave => !_state.IsBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => SaveChanges();

    public bool SaveChanges()
    {
        if (_state.IsBusy)
        {
            _services.Dialogs.Warning(LockedText, Title);
            return false;
        }
        if (SelectedRulesCount == 0)
        {
            _services.Dialogs.Warning(NeedOneRuleText, Title);
            return false;
        }

        foreach (var f in Files)
        {
            f.Refresh();
            if (f.State == ConfigFileState.Missing)
            {
                _services.Dialogs.Warning($"Файл «{f.Caption}» не найден по указанному пути:\n{f.Path}", Title);
                return false;
            }
        }

        var s = _services.Settings.Processing;
        s.EnabledRules = Rules.Where(r => r.IsChecked).Select(r => r.Rule).ToList();
        s.NormalizeLinkedRules();
        s.Morphology = (MorphologyMode)Math.Max(0, MorphologyIndex);
        s.WordsFilePath = WordsFile.PathOrNull;
        s.EndingsFilePath = EndingsFile.PathOrNull;
        s.ExclusionsFilePath = ExclusionsFile.PathOrNull;
        s.ForceDeleteWordsFilePath = ForceDeleteFile.PathOrNull;
        s.Extra1CChars = Extra1CChars ?? "";
        s.CyrillicExceptions = CyrillicExceptions ?? "";
        s.EndingKeepSeries = EndingKeepSeries ?? "";

        try { _services.Settings.SaveProcessing(); }
        catch (Exception ex)
        {
            _services.Dialogs.Error("Не удалось сохранить настройки анализа:\n\n" + ex.Message +
                                    "\n\nИзменения действуют до закрытия программы.", Title);
        }

        ReloadConfiguration();

        var warnings = ConfigRepository.Instance.LoadWarnings;
        if (warnings.Count > 0)
            _services.Dialogs.Warning(
                "Настройки сохранены, но при загрузке конфигураций возникли замечания:\n\n• " +
                string.Join("\n• ", warnings), Title);

        LoadFromSettings();
        SavedVisible = true;
        return true;
    }

    public void RevertChanges() => LoadFromSettings();

    /// <summary>
    /// Откатывает несохранённые изменения и сообщает навигации,
    /// что переход можно выполнить.
    /// </summary>
    private bool RevertChangesAndLeave()
    {
        RevertChanges();
        return true;
    }

    public void LoadFromSettings()
    {
        _suppress = true;
        try
        {
            var s = _services.Settings.Processing;
            foreach (var r in Rules) r.IsChecked = s.EnabledRules.Contains(r.Rule);
            MorphologyIndex = (int)s.Morphology;
            WordsFile.Path = s.WordsFilePath;
            EndingsFile.Path = s.EndingsFilePath;
            ExclusionsFile.Path = s.ExclusionsFilePath;
            ForceDeleteFile.Path = s.ForceDeleteWordsFilePath;
            Extra1CChars = s.Extra1CChars ?? "";
            CyrillicExceptions = s.CyrillicExceptions ?? "";
            EndingKeepSeries = s.EndingKeepSeries ?? "";
        }
        finally { _suppress = false; }

        foreach (var f in Files) f.Refresh();
        UpdateWarning();
        IsDirty = false;
        SavedVisible = false;
    }

    /// <summary>
    /// Можно ли уйти с вкладки: без правил — нельзя; при несохранённых изменениях —
    /// запрос «Не сохранять / Сохранить» (Esc — остаться).
    /// </summary>
    /// <summary>
    /// Проверяет возможность ухода с вкладки «Настройки анализа».
    ///
    /// При работающем анализе несохранённые изменения нельзя сохранить.
    /// После предупреждения они откатываются, а переход разрешается.
    /// Таким образом пользователь не остаётся на вкладке с изменёнными
    /// настройками, которые невозможно применить к текущему анализу.
    /// </summary>
    public bool CanLeave()
    {
        if (SelectedRulesCount == 0)
        {
            _services.Dialogs.Warning(NeedOneRuleText, Title);
            return false;
        }

        if (!IsDirty)
            return true;

        if (_state.IsBusy)
        {
            // Текущий анализ продолжает работать на уже сохранённой
            // конфигурации. Изменения пользователя были только локальными
            // и не должны попасть в настройки после завершения анализа.
            _services.Dialogs.Warning(LockedText, Title);

            // После единственной кнопки OK возвращаем экран к последним
            // сохранённым настройкам и разрешаем переход на выбранную вкладку.
            RevertChanges();

            return true;
        }

        return _services.Dialogs.AskSaveChanges() switch
        {
            SaveChoice.Cancel => false,
            SaveChoice.Save => SaveChanges(),
            SaveChoice.Discard => RevertChangesAndLeave(),
            _ => false
        };
    }
}

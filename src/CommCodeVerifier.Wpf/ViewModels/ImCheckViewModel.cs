using System.IO;
using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeVerifier.Wpf.ViewModels;

public enum ChosenFolderState { Chosen, Latest, None }

/// Вкладка «Проверка для ИМ».
public sealed partial class ImCheckViewModel : ObservableObject
{
    public const string NoteText = "Если планируете проверять последние результаты, просто нажмите «Запустить проверку».";
    public const string NoInvalidCodesText = "Некорректных коммерческих кодов не обнаружено";
    private const string Title = "Проверка для ИМ";
    private const string XlsxFilter = "Файлы Excel (*.xlsx)|*.xlsx";

    private readonly AppServices _services;
    private readonly ProcessingState _state;
    private readonly Action _reloadConfiguration;
    private CancellationTokenSource? _cts;
    private Task? _current;

    /// Папка, выбранная кнопкой «Папка для проверки»; null — последняя папка результатов.
    private string? _chosenFolder;

    public ImCheckViewModel(AppServices services, ProcessingState state, Action reloadConfiguration)
    {
        _services = services;
        _state = state;
        _reloadConfiguration = reloadConfiguration;

        CodeRules = new ImRulesFileItem(
            "Файл «Правила проверки комм. кода для ИМ»",
            () => ConfigRepository.DefaultImRulesPath,
            () => _services.Settings.Processing.ImRulesFilePath,
            p => _services.Settings.Processing.ImRulesFilePath = p,
            () => ConfigRepository.Instance.ImRules.Rules.Count);

        ArticleRules = new ImRulesFileItem(
            "Файл «Правила проверки артикула для ИМ»",
            () => ConfigRepository.DefaultImArticleRulesPath,
            () => _services.Settings.Processing.ImArticleRulesFilePath,
            p => _services.Settings.Processing.ImArticleRulesFilePath = p,
            () => ConfigRepository.Instance.ImArticleRules.Rules.Count);

        RulesFiles = new[] { CodeRules, ArticleRules };

        _state.PropertyChanged += (_, _) =>
        {
            LoadFileCommand.NotifyCanExecuteChanged();
            RunFolderCommand.NotifyCanExecuteChanged();
            PickRulesFileCommand.NotifyCanExecuteChanged();
            ResetRulesFilesCommand.NotifyCanExecuteChanged();
        };

        Refresh();
    }

    public ImRulesFileItem CodeRules { get; }
    public ImRulesFileItem ArticleRules { get; }
    public IReadOnlyList<ImRulesFileItem> RulesFiles { get; }

    public string SingleResultsTooltip =>
        "Открыть папку с результатами проверки по отдельному файлу (Результаты обработки\\" +
        ImChecker.SingleRunSubfolder + ")";

    // ================= состояние =================
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    [ObservableProperty] private string _chosenText = "";
    [ObservableProperty] private ChosenFolderState _chosenState;

    /// Идёт проверка (этой вкладкой).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate), nameof(HasSingleResults), nameof(HasBatchResults))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    private int _progress;

    [ObservableProperty] private string _status = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummary))]
    private string _summary = "";

    [ObservableProperty] private bool _summaryHasWrong;

    [ObservableProperty]
    private bool _noInvalidCodesSingle;

    [ObservableProperty]
    private bool _noInvalidCodesBatch;

    /// Папки результатов последнего прогона — отдельно для каждого варианта (как в WinForms).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSingleResults))]
    private string? _lastSingleFolder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBatchResults))]
    private string? _lastBatchFolder;

    public bool HasError => Error != null;
    public bool HasSummary => Summary.Length > 0;
    public bool IsIndeterminate => IsBusy && Progress == 0;
    public bool HasSingleResults => LastSingleFolder != null && !IsBusy;
    public bool HasBatchResults => LastBatchFolder != null && !IsBusy;

    /// Запуск проверки и смена файлов правил — только когда не идёт никакая обработка.
    private bool CanStart => !_state.IsBusy;

    /// Переход на вкладку: файлы правил и последняя папка результатов могли измениться.
    public void Refresh()
    {
        CodeRules.Refresh();
        ArticleRules.Refresh();
        UpdateChosenFolder();
    }

    /// Прервать проверку (при закрытии программы) и дождаться её остановки.
    public Task StopAsync()
    {
        _cts?.Cancel();
        return _current ?? Task.CompletedTask;
    }

    // ================= вариант «Отдельный файл» =================
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task LoadFileAsync()
    {
        Error = null;
        string? path = _services.Dialogs.OpenFile(XlsxFilter, title: "Выберите файл для проверки признака ИМ");
        if (path == null) return;

        var (ok, why) = await Task.Run(() =>
        {
            bool valid = ImChecker.ValidateSingleTemplate(path, out string w);
            return (valid, w);
        });
        if (!ok)
        {
            Error = "Файл не соответствует шаблону - " + why;
            return;
        }
        if (!EnsureRulesLoaded()) return;

        var cfg = ConfigRepository.Instance;
        await (_current = ExecuteAsync(
            (p, ct) => ImChecker.RunSingleFile(path, cfg.ImRules, cfg.ImArticleRules, p, ct),
            singleFile: true));
    }

    // ================= вариант «Папка групповой обработки» =================
    [RelayCommand]
    private void PickFolder()
    {
        string initial = Directory.Exists(ImChecker.ResultsRoot) ? ImChecker.ResultsRoot : AppPaths.BaseFolder;
        string? folder = _services.Dialogs.PickFolder(initial, "Выберите папку с результатами групповой обработки");
        if (folder == null) return;

        _chosenFolder = folder;
        UpdateChosenFolder();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task RunFolderAsync()
    {
        Error = null;

        string? folder = _chosenFolder ?? ImChecker.FindLatestResultsFolder();
        if (folder == null)
        {
            Error = "Не найдено ни одной папки с результатами групповой обработки. " +
                    "Выполните групповую проверку или укажите папку кнопкой «Папка для проверки».";
            return;
        }
        if (!ImChecker.ValidateResultsFolder(folder, out string why))
        {
            Error = "Папку проверить невозможно - " + why;
            return;
        }
        if (!EnsureRulesLoaded()) return;

        var cfg = ConfigRepository.Instance;
        await (_current = ExecuteAsync(
            (p, ct) => ImChecker.RunResultsFolder(folder, cfg.ImRules, cfg.ImArticleRules, p, ct),
            singleFile: false));
    }

    private void UpdateChosenFolder()
    {
        if (_chosenFolder != null)
        {
            ChosenText = "Будет проверена папка: " + _chosenFolder;
            ChosenState = ChosenFolderState.Chosen;
            return;
        }

        string? latest = ImChecker.FindLatestResultsFolder();
        ChosenText = latest == null
            ? "Папка не выбрана, готовых результатов групповой обработки не найдено"
            : "Папка не выбрана — будет проверена последняя: " + latest;
        ChosenState = latest == null ? ChosenFolderState.None : ChosenFolderState.Latest;
    }

    // ================= выполнение =================
    private bool EnsureRulesLoaded()
    {
        var cfg = ConfigRepository.Instance;
        if (!cfg.ImRules.IsEmpty || !cfg.ImArticleRules.IsEmpty) return true;

        Error = "Оба файла правил для ИМ не содержат ни одного правила или не найдены. " +
                "Заполните файлы либо укажите другие в блоке ниже.";
        return false;
    }

    /// Проверка в фоне. Все ошибки обрабатываются внутри — задачу можно безопасно ожидать.
    private async Task ExecuteAsync(Func<IProgress<int>, CancellationToken, ImReport> work, bool singleFile)
    {
        _state.Begin();
        IsBusy = true;
        Progress = 0;
        Status = "";
        Summary = "";
        SummaryHasWrong = false;
        if (singleFile)
            NoInvalidCodesSingle = false;
        else
            NoInvalidCodesBatch = false;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var progress = new Progress<int>(v => Progress = Math.Clamp(v, 0, 100));

        try
        {
            var report = await Task.Run(() => work(progress, token), token);

            if (singleFile) LastSingleFolder = report.OutputFolder;
            else LastBatchFolder = report.OutputFolder;
            if (singleFile) 
                NoInvalidCodesSingle = report.Wrong == 0;
            else
                NoInvalidCodesBatch = report.Wrong == 0;

            Progress = 100;
            Status = singleFile
                ? "Проверка завершена, файлы результатов готовы для просмотра (проверка по отдельному файлу)"
                : "Проверка завершена, файлы результатов готовы для просмотра (проверка по файлам групповой обработки)";
            BuildSummary(report);
        }
        catch (OperationCanceledException)
        {
            Status = "Проверка прервана";
            Progress = 0;
        }
        catch (Exception ex)
        {
            Error = "Ошибка при проверке: " + ex.Message;
            Status = "";
            Progress = 0;
        }
        finally
        {
            IsBusy = false;
            _cts.Dispose();
            _cts = null;
            _state.End();
            UpdateChosenFolder();
        }
    }

    private void BuildSummary(ImReport r)
    {
        var lines = new List<string>
        {
            "Проверено: " + r.ScannedTarget,
            "Файлов-источников: " + (r.ScannedFiles.Count == 0 ? "нет" : string.Join(", ", r.ScannedFiles)),
            $"Применено правил: комм. код — {r.CodeRulesCount}, артикул — {r.ArticleRulesCount}",
            "Проанализировано строк: " + r.TotalRows,
            "Корректный признак ИМ: " + r.Correct,
            $"Ошибочный признак ИМ: {r.Wrong} (по комм. коду — {r.WrongByCode}, по артикулу — {r.WrongByArticle})",
            "Папка результатов: " + r.OutputFolder
        };
        if (r.SkippedNoGroup > 0)
            lines.Add("Пропущено (пустая группа выгрузки или нет кода): " + r.SkippedNoGroup);
        if (r.Warnings.Count > 0)
            lines.Add("Замечания: " + string.Join("; ", r.Warnings));

        Summary = string.Join(Environment.NewLine, lines);
        SummaryHasWrong = r.Wrong > 0;
    }

    // ================= папки результатов =================
    [RelayCommand] private void OpenSingleResults() => OpenFolder(LastSingleFolder);
    [RelayCommand] private void OpenBatchResults() => OpenFolder(LastBatchFolder);

    private void OpenFolder(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            _services.Dialogs.Info("Папка с результатами недоступна — возможно, она была перемещена или удалена.", Title);
            return;
        }
        try { _services.Shell.OpenFolder(path); }
        catch { /* открытие проводника не критично */ }
    }

    // ================= файлы правил =================
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void PickRulesFile(ImRulesFileItem? item)
    {
        if (item == null) return;
        string? path = _services.Dialogs.OpenFile(XlsxFilter, item.InitialDirectory, item.Caption);
        if (path == null) return;

        item.CustomPath = path;
        SaveAndReload();   // путь запоминается сразу (как в WinForms)
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void ResetRulesFiles()
    {
        CodeRules.CustomPath = null;
        ArticleRules.CustomPath = null;
        SaveAndReload();
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

    private void SaveAndReload()
    {
        try { _services.Settings.SaveProcessing(); }
        catch (Exception ex)
        {
            _services.Dialogs.Error("Не удалось сохранить настройки:\n\n" + ex.Message +
                                    "\n\nВыбранные файлы правил действуют до закрытия программы.", Title);
        }
        _reloadConfiguration();
        CodeRules.Refresh();
        ArticleRules.Refresh();
    }
}

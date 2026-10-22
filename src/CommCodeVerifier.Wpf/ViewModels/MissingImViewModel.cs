using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeVerifier.Wpf.ViewModels;

public sealed partial class MissingImWeightItem :
    ObservableObject
{
    public MissingImWeightItem(
        string title,
        Func<MissingImWeights, double> getter,
        Action<MissingImWeights, double> setter,
        MissingImWeights weights)
    {
        Title = title;
        _getter = getter;
        _setter = setter;
        _weights = weights;
        Value = getter(weights).ToString(
            "0.00",
            CultureInfo.CurrentCulture);
    }

    private readonly Func<MissingImWeights, double> _getter;
    private readonly Action<MissingImWeights, double> _setter;
    private readonly MissingImWeights _weights;

    public string Title { get; }

    [ObservableProperty]
    private string _value;

    public bool TryApply(out string error)
    {
        error = "";

        if (!double.TryParse(
                Value.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double parsed))
        {
            error = $"Некорректное значение веса «{Title}».";
            return false;
        }

        if (parsed < 0.00 || parsed > 1.00)
        {
            error =
                $"Вес «{Title}» должен находиться " +
                "в диапазоне от 0,00 до 1,00.";
            return false;
        }

        parsed = Math.Round(parsed, 2);
        _setter(_weights, parsed);

        Value = parsed.ToString(
            "0.00",
            CultureInfo.CurrentCulture);

        return true;
    }

    public void Refresh()
    {
        Value = _getter(_weights).ToString(
            "0.00",
            CultureInfo.CurrentCulture);
    }
}
public sealed partial class MissingImLocalGroupThresholdItem :
    ObservableObject
{
    private readonly MissingImLocalGroupThreshold _settings;

    public MissingImLocalGroupThresholdItem(
        MissingImLocalGroupThreshold settings,
        string groupSizeRange)
    {
        _settings = settings;
        GroupSizeRange = groupSizeRange;

        MinimumPositiveReferences =
            settings.MinimumPositiveReferences.ToString(
                CultureInfo.CurrentCulture);

        MinimumPositiveRatio =
            settings.MinimumPositiveRatio.ToString(
                "0.00",
                CultureInfo.CurrentCulture);
    }

    public string GroupSizeRange { get; }

    [ObservableProperty]
    private string _minimumPositiveReferences = "";

    [ObservableProperty]
    private string _minimumPositiveRatio = "";

    public bool TryApply(out string error)
    {
        error = "";

        if (!int.TryParse(
                MinimumPositiveReferences,
                out int references) ||
            references < 1 ||
            references > _settings.MaximumGroupSize)
        {
            string allowedRange =
                _settings.MaximumGroupSize == int.MaxValue
                    ? "целым числом не меньше 1"
                    : $"целым числом от 1 до {_settings.MaximumGroupSize}";

            error =
                $"Для диапазона «{GroupSizeRange}» минимум " +
                $"положительных референсов должен быть {allowedRange}.";

            return false;
        }

        if (!double.TryParse(
                MinimumPositiveRatio.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double ratio) ||
            ratio < 0.00 ||
            ratio > 1.00)
        {
            error =
                $"Для диапазона «{GroupSizeRange}» минимальная доля " +
                "должна быть от 0,00 до 1,00.";

            return false;
        }

        _settings.MinimumPositiveReferences = references;
        _settings.MinimumPositiveRatio = Math.Round(ratio, 2);

        MinimumPositiveRatio =
            _settings.MinimumPositiveRatio.ToString(
                "0.00",
                CultureInfo.CurrentCulture);

        return true;
    }

    public void Refresh()
    {
        MinimumPositiveReferences =
            _settings.MinimumPositiveReferences.ToString(
                CultureInfo.CurrentCulture);

        MinimumPositiveRatio =
            _settings.MinimumPositiveRatio.ToString(
                "0.00",
                CultureInfo.CurrentCulture);
    }
}
public sealed partial class MissingImViewModel :
    ObservableObject
{
    public const string NoCandidatesText =
        "Некорректных коммерческих кодов с отсутствующим " +
        "признаком ИМ не обнаружено";
    public const string InputTemplateText =
    "Файл должен содержать три столбца: " +
    "«Номенклатура.Артикул», «Коммерческий код» и " +
    "«Номенклатура.Группа выгрузки ИМ (Общие)». " +
    "Непустое значение третьего столбца означает, " +
    "что признак ИМ установлен.";

    private readonly AppServices _services;
    private readonly ProcessingState _state;
    private CancellationTokenSource? _cts;
    private Task? _current;

    private MissingImSettings _workingSettings;

    public MissingImViewModel(AppServices services, ProcessingState state)
    {
        _services = services;
        _state = state;

        _workingSettings =
            services.Settings.Processing.MissingIm.Clone();
        _workingSettings.Normalize();
        LoadThresholdsFromSettings();
        BuildWeightItems();
    }

    public ObservableCollection<MissingImWeightItem> Weights
    {
        get;
    } = new();

    public ObservableCollection<MissingImLocalGroupThresholdItem>
    LocalGroupThresholds
    {
        get;
    } = new();

    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private string? _outputFolder;

    [NotifyPropertyChangedFor(nameof(HasOutput))]
    [NotifyCanExecuteChangedFor(nameof(OpenFolderCommand))]
    [ObservableProperty]
    private string? _outputFile;

    [ObservableProperty]
    private int _scannedCount;

    [ObservableProperty]
    private int _candidateCount;

    [ObservableProperty]
    private string _minimumCodeSimilarity = "0,80";

    [ObservableProperty]
    private string _minimumArticleSimilarityForLocalGroup = "0,35";

    [ObservableProperty]
    private string _positionDecay = "0,75";

    [ObservableProperty]
    private string _articlePositionDecay = "0,75";

    [ObservableProperty]
    private bool _enableSimilarityDiagnostics = true;

    public bool HasError =>
        !string.IsNullOrWhiteSpace(Error);

    public bool HasOutput =>
        !string.IsNullOrWhiteSpace(OutputFile) &&
        File.Exists(OutputFile);

    public bool HasCandidates =>
        CandidateCount > 0;

    private bool CanStart =>
        !_state.IsBusy;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task SelectAndRunAsync()
    {
        string? path =
            _services.Dialogs.OpenFile(
                "Файлы Excel (*.xlsx)|*.xlsx",
                title:
                "Выберите файл для проверки " +
                "отсутствующего признака ИМ");

        if (path == null)
            return;

        if (!MissingImChecker.ValidateInputTemplate(
        path,
        out string templateError))
        {
            Error =
                "Выбранный файл имеет некорректную структуру " +
                "или не соответствует шаблону новой проверки.\n" +
                templateError;

            Status = "";
            return;
        }
        await RunAsync(path);
    }

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void OpenFolder()
    {
        if (string.IsNullOrWhiteSpace(OutputFolder))
            return;

        if (Directory.Exists(OutputFolder))
            _services.Shell.OpenFolder(OutputFolder);
    }

    [RelayCommand]
    private void SaveSettings()
    {
        if (!TryApplySettings(out string error))
        {
            Error = error;
            return;
        }

        _services.Settings.Processing.MissingIm = _workingSettings.Clone();
        _services.Settings.SaveProcessing();

        Error = null;
        Status = "Настройки сохранены";
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        _workingSettings = new MissingImSettings();

        LoadThresholdsFromSettings();
        BuildWeightItems();
        foreach (var weight in Weights)
            weight.Refresh();

        Error = null;
        Status = "Восстановлены значения по умолчанию";
    }

    private async Task RunAsync(string sourcePath)
    {
        if (!TryApplySettings(out string error))
        {
            Error = error;
            return;
        }

        _state.Begin();
        IsBusy = true;
        Progress = 0;
        Error = null;
        OutputFolder = null;
        OutputFile = null;
        ScannedCount = 0;
        CandidateCount = 0;
        Status = "Проверка отсутствия признака ИМ…";

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            string outputFolder =
                Path.Combine(
                    AppPaths.ResultsFolder,
                    "Проверка отсутств. признака");

            var progress =
                new Progress<int>(
                    value => Progress =
                        Math.Clamp(value, 0, 100));

            var report = await Task.Run(
                () => MissingImChecker.Run(
                    sourcePath,
                    _workingSettings,
                    progress,
                    token,
                    outputFolder),
                token);

            OutputFolder = report.OutputFolder;
            OutputFile = report.OutputFile;
            ScannedCount = report.ScannedCount;
            CandidateCount = report.CandidateCount;
            if (report.CandidateCount > 0 && string.IsNullOrWhiteSpace(report.OutputFile))
            {
                Error =
                    "Кандидаты найдены, но путь к файлу " +
                    "результатов не был возвращён.";
            }
            Progress = 100;

            Status = report.CandidateCount == 0
                ? NoCandidatesText
                : $"Найдено кандидатов: " +
                  $"{report.CandidateCount}";
        }
        catch (OperationCanceledException)
        {
            Progress = 0;
            Status = "Проверка отменена";
        }
        catch (Exception ex)
        {
            Error = "Ошибка при проверке: " + ex.Message;
            Status = "";
        }
        finally
        {
            IsBusy = false;
            _cts.Dispose();
            _cts = null;
            _state.End();
        }
    }

    private bool TryApplySettings(out string error)
    {
        error = "";

        foreach (var weight in Weights)
        {
            if (!weight.TryApply(out error))
                return false;
        }

        if (!TryParseRatio(
                MinimumCodeSimilarity,
                out double codeSimilarity))
        {
            error =
                "Некорректное минимальное сходство кода.";
            return false;
        }

        if (!TryParseRatio(
        MinimumArticleSimilarityForLocalGroup,
        out double minimumArticleSimilarityForLocalGroup))
        {
            error =
                "Некорректный минимальный порог сходства артикулов: " +
                "укажите значение от 0,00 до 1,00.";
            return false;
        }

        if (!TryParseRatio(
        PositionDecay,
        out double positionDecay))
        {
            error =
                "Некорректный коэффициент позиционного затухания кода: " +
                "укажите значение от 0,00 до 1,00.";
            return false;
        }

        if (!TryParseRatio(
                ArticlePositionDecay,
                out double articlePositionDecay))
        {
            error =
                "Некорректный коэффициент позиционного затухания артикула: " +
                "укажите значение от 0,00 до 1,00.";
            return false;
        }

        foreach (var threshold in LocalGroupThresholds)
        {
            if (!threshold.TryApply(out error))
                return false;
        }

        _workingSettings.MinimumCodeSimilarity = codeSimilarity;
        _workingSettings.MinimumArticleSimilarityForLocalGroup = minimumArticleSimilarityForLocalGroup;
        _workingSettings.PositionDecay = positionDecay;
        _workingSettings.ArticlePositionDecay = articlePositionDecay;
        _workingSettings.EnableSimilarityDiagnostics = EnableSimilarityDiagnostics;

        _workingSettings.Normalize();

        return true;
    }

    private static bool TryParseRatio(
        string text,
        out double value)
    {
        return double.TryParse(
                   text.Replace(',', '.'),
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out value) &&
               value >= 0.00 &&
               value <= 1.00;
    }

    private void LoadThresholdsFromSettings()
    {
        MinimumCodeSimilarity =
            _workingSettings.MinimumCodeSimilarity.ToString("0.00", CultureInfo.CurrentCulture);

        MinimumArticleSimilarityForLocalGroup =
            _workingSettings.MinimumArticleSimilarityForLocalGroup.ToString("0.00", CultureInfo.CurrentCulture);
        PositionDecay =
            _workingSettings.PositionDecay.ToString("0.00", CultureInfo.CurrentCulture);

        ArticlePositionDecay =
            _workingSettings.ArticlePositionDecay.ToString("0.00", CultureInfo.CurrentCulture);

        EnableSimilarityDiagnostics =
            _workingSettings.EnableSimilarityDiagnostics; LocalGroupThresholds.Clear();

        int[] maxima = { 5, 20, 100, int.MaxValue };
        int[] minima = { 1, 6, 21, 101 };

        for (int i = 0;
             i < _workingSettings.LocalGroupThresholds.Count;
             i++)
        {
            string range = i == 3
                ? ">100"
                : $"{minima[i]}–{maxima[i]}";

            LocalGroupThresholds.Add(
                new MissingImLocalGroupThresholdItem(
                    _workingSettings.LocalGroupThresholds[i],
                    range));
        }
    }

    private void BuildWeightItems()
    {
        Weights.Clear();
        Weights.Add(
            new MissingImWeightItem(
                "Типы фрагментов",
                weights => weights.FragmentType,
                (weights, value) =>
                    weights.FragmentType = value,
                _workingSettings.Weights));

        Weights.Add(
            new MissingImWeightItem(
                "Позиции фрагментов",
                weights => weights.FragmentPosition,
                (weights, value) =>
                    weights.FragmentPosition = value,
                _workingSettings.Weights));

        Weights.Add(
            new MissingImWeightItem(
                "Буквенные сегменты",
                weights => weights.LetterSegments,
                (weights, value) =>
                    weights.LetterSegments = value,
                _workingSettings.Weights));

        Weights.Add(
            new MissingImWeightItem(
                "Позиции буквенных сегментов",
                weights => weights.LetterPositions,
                (weights, value) =>
                    weights.LetterPositions = value,
                _workingSettings.Weights));

        Weights.Add(
            new MissingImWeightItem(
                "Числовые блоки",
                weights => weights.NumericBlocks,
                (weights, value) =>
                    weights.NumericBlocks = value,
                _workingSettings.Weights));

        Weights.Add(
            new MissingImWeightItem(
                "Позиции числовых блоков",
                weights => weights.NumericPositions,
                (weights, value) =>
                    weights.NumericPositions = value,
                _workingSettings.Weights));

        Weights.Add(
            new MissingImWeightItem(
                "Длины фрагментов",
                weights => weights.FragmentLengths,
                (weights, value) =>
                    weights.FragmentLengths = value,
                _workingSettings.Weights));

        Weights.Add(
            new MissingImWeightItem(
                "Разделители",
                weights => weights.Separators,
                (weights, value) =>
                    weights.Separators = value,
                _workingSettings.Weights));

        Weights.Add(
            new MissingImWeightItem(
                "Общая длина кода",
                weights => weights.CodeLength,
                (weights, value) =>
                    weights.CodeLength = value,
                _workingSettings.Weights));

        Weights.Add(
            new MissingImWeightItem(
                "Сходство артикулов",
                weights => weights.ArticleSimilarity,
                (weights, value) =>
                    weights.ArticleSimilarity = value,
                _workingSettings.Weights));

        Weights.Add(
            new MissingImWeightItem(
                "Доля кодов с признаком в семействе",
                weights => weights.ImPresenceInFamily,
                (weights, value) =>
                    weights.ImPresenceInFamily = value,
                _workingSettings.Weights));
    }
}

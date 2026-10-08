using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeVerifier.Wpf.ViewModels;

/// Строка сводки: Left — по левому краю, Right — счётчик по правому.
public sealed record SummaryLine(string Left, string Right);

/// Вкладка «Групповая проверка».
public sealed partial class BatchViewModel : ObservableObject
{
    public const string BadStructureText = "Выбранный файл имеет некорректную структуру данных, анализ невозможен";
    public const string SuccessText = "Анализ успешно завершён, файлы с результатами готовы для просмотра";
    public const string NoInvalidCodesText = "Некорректных коммерческих кодов не обнаружено";
    private readonly AppServices _services;
    private readonly ProcessingState _state;
    private CancellationTokenSource? _cts;
    private Task? _current;

    public BatchViewModel(AppServices services, ProcessingState state)
    {
        _services = services;
        _state = state;
        _state.PropertyChanged += (_, _) => LoadAndRunCommand.NotifyCanExecuteChanged();
    }

    /// Идёт проверка или обработка файла (этой вкладкой).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    private int _progress;

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _noInvalidCodes;

    /// Статус — успешное завершение (зелёный); иначе серый.
    [ObservableProperty] private bool _statusSuccess;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDuplicateWarning))]
    private string? _duplicateWarning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpenFolder))]
    [NotifyCanExecuteChangedFor(nameof(OpenFolderCommand))]
    private string? _outputFolder;

    public bool HasError => Error != null;
    public bool HasDuplicateWarning => DuplicateWarning != null;
    public bool CanOpenFolder => OutputFolder != null;

    /// Пока файл читается, процент ещё не известен — бегущая полоса.
    public bool IsIndeterminate => IsBusy && Progress == 0;

    /// Запуск возможен, если не идёт никакая обработка (в том числе проверка ИМ).
    private bool CanStart => !_state.IsBusy;

    public ObservableCollection<SummaryLine> Summary { get; } = new();

    // ================= команды =================
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task LoadAndRunAsync()
    {
        string? path = _services.Dialogs.OpenFile("Файлы Excel (*.xlsx)|*.xlsx", title: "Выберите файл для анализа");
        if (path == null) return;
        _current = RunFileAsync(path);
        await _current;
    }

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel() => _cts?.Cancel();

    /// Прервать анализ (при закрытии программы) и дождаться его остановки.
    public Task StopAsync()
    {
        _cts?.Cancel();
        return _current ?? Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private void OpenFolder()
    {
        if (OutputFolder == null) return;
        if (!Directory.Exists(OutputFolder))
        {
            _services.Dialogs.Warning("Папка с результатами не найдена:\n" + OutputFolder);
            return;
        }
        _services.Shell.OpenFolder(OutputFolder);
    }

    [RelayCommand]
    private void CopySummary()
    {
        if (Summary.Count == 0) return;
        if (!_services.Shell.CopyText(SummaryAsText()))
            _services.Dialogs.Warning("Не удалось скопировать сводку: буфер обмена занят другой программой.");
    }

    // ================= анализ =================
    /// Анализ файла. Все ошибки обрабатываются внутри — задачу можно безопасно ожидать.
    public async Task RunFileAsync(string path)
    {
        _state.Begin();
        Error = null;
        DuplicateWarning = null;
        OutputFolder = null;
        Summary.Clear();
        Progress = 0;
        StatusSuccess = false;
        NoInvalidCodes = false;
        Status = "Проверка структуры файла…";
        IsBusy = true;
        _cts = new CancellationTokenSource();

        try
        {
            var (ok, reason) = await Task.Run(() =>
            {
                bool valid = BatchProcessor.ValidateTemplate(path, out string e);
                return (valid, e);
            });
            if (!ok)
            {
                Error = BadStructureText + (string.IsNullOrWhiteSpace(reason) ? "" : "\n(" + reason + ")");
                Status = "";
                return;
            }
            _cts.Token.ThrowIfCancellationRequested();

            Status = "Анализ файла…";
            // Сохранённые настройки анализа — как в WinForms.
            var opts = _services.Settings.Processing.ToProcessOptions();
            var progress = new Progress<int>(p => Progress = Math.Clamp(p, 0, 100));

            var report = await BatchProcessor.RunAsync(path, opts, progress, _cts.Token);

            Progress = 100;
            Status = SuccessText;
            StatusSuccess = true;
            OutputFolder = report.OutputFolder;
            NoInvalidCodes = report.Count1C == 0 && report.CountManual == 0;
            ShowDuplicateWarning(report);
            BuildSummary(report);
        }
        catch (OperationCanceledException)
        {
            Progress = 0;
            Status = "Анализ прерван, результаты не сохранены";
        }
        catch (Exception ex)
        {
            Error = "Ошибка при анализе файла: " + ex.Message;
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

    private void ShowDuplicateWarning(BatchReport report)
    {
        var files = report.FilesWithDuplicates.ToList();
        DuplicateWarning = files.Count == 0
            ? null
            : string.Join(Environment.NewLine, files.Select(BatchProcessor.DuplicateWarning));
    }

    private void BuildSummary(BatchReport r)
    {
        foreach (var (left, right) in ReportWriter.BuildSummaryLines(r))
            Summary.Add(new SummaryLine(left, right));

        if (r.ReportWriteError != null)
        {
            Summary.Add(new SummaryLine("", ""));
            Summary.Add(new SummaryLine("Файл сводки не сохранён: " + r.ReportWriteError, ""));
        }
    }

    /// Текст сводки для буфера: счётчики выровнены пробелами по правому краю (как в WinForms).
    private string SummaryAsText()
    {
        int cols = Summary.Where(l => l.Right.Length > 0)
                          .Select(l => l.Left.Length + l.Right.Length + 4)
                          .DefaultIfEmpty(0).Max();
        var sb = new StringBuilder();
        foreach (var l in Summary)
        {
            if (l.Right.Length == 0) { sb.AppendLine(l.Left); continue; }
            sb.AppendLine(l.Left + new string(' ', Math.Max(1, cols - l.Left.Length - l.Right.Length)) + l.Right);
        }
        return sb.ToString();
    }
}

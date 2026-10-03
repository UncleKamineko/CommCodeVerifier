using System.Windows.Threading;
using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeVerifier.Wpf.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    /// Индекс вкладки «Настройки анализа».
    public const int SettingsTabIndex = 2;

    /// Индекс вкладки «Проверка для ИМ».
    public const int ImCheckTabIndex = 3;

    public const string UnsavedOnCloseText =
        "В настройках анализа есть несохранённые изменения, выйти без сохранения?";

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private int _selectedTabIndex;

    public AppServices Services { get; }

    /// Идёт обработка (групповая проверка или проверка ИМ).
    public ProcessingState Processing { get; } = new();

    public SingleCheckViewModel SingleCheck { get; }
    public BatchViewModel Batch { get; }
    public AnalysisSettingsViewModel AnalysisSettings { get; }
    public ImCheckViewModel ImCheck { get; }

    public MainViewModel(AppServices services)
    {
        Services = services;
        SingleCheck = new SingleCheckViewModel(services);
        Batch = new BatchViewModel(services, Processing);
        AnalysisSettings = new AnalysisSettingsViewModel(services, Processing);
        ImCheck = new ImCheckViewModel(services, Processing, ReloadConfiguration);
    }

    /// <summary>
    /// Текущая вкладка. Уход с «Настроек анализа» проверяется (нет правил, несохранённые изменения).
    /// При отказе TabControl уже переключился — он возвращается на прежнюю вкладку
    /// после завершения текущего обновления привязки.
    /// </summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (value == _selectedTabIndex) return;

            if (_selectedTabIndex == SettingsTabIndex && !AnalysisSettings.CanLeave())
            {
                _dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(SelectedTabIndex)),
                    DispatcherPriority.Input);
                return;
            }
            if (SetProperty(ref _selectedTabIndex, value) && value == ImCheckTabIndex)
                ImCheck.Refresh();   // новая папка групповой обработки, изменённые файлы правил
        }
    }

    [RelayCommand]
    private void OpenProgramSettings() => Services.Windows.ShowProgramSettings();

    [RelayCommand]
    private void OpenHelp() => Services.Windows.ShowHelp();

    /// Ctrl+Shift+T — самопроверка правил на сохранённых настройках (как в WinForms).
    [RelayCommand]
    private void RunSelfTest() =>
        Services.Windows.ShowSelfTest(SelfTest.Run(Services.Settings.Processing.ToProcessOptions()));

    /// Перечитать файлы конфигурации (после смены путей или правки файлов).
    public void ReloadConfiguration() => AnalysisSettings.ReloadConfiguration();

    /// Прервать все обработки и дождаться их остановки (при закрытии программы).
    public Task StopProcessingAsync() => Task.WhenAll(Batch.StopAsync(), ImCheck.StopAsync());

    /// <summary>
    /// Несохранённые настройки анализа при закрытии: true — можно закрывать.
    /// Не сохранять — закрыть; Сохранить — сохранить и закрыть; Esc — остаться.
    /// </summary>
    public bool ConfirmUnsavedSettingsOnClose()
    {
        if (!AnalysisSettings.IsDirty) return true;

        return Services.Dialogs.AskSaveChanges(UnsavedOnCloseText) switch
        {
            SaveChoice.Save => AnalysisSettings.SaveChanges(),
            SaveChoice.Discard => true,
            _ => false
        };
    }
}

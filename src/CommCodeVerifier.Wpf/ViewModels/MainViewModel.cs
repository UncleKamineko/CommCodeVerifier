using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeVerifier.Wpf.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public const string UnsavedOnCloseText =
        "В настройках анализа есть несохранённые изменения, выйти без сохранения?";

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    private SectionId _selectedSectionId;
    private NavigationSection? _selectedSection;
    private NavigationTab? _selectedTab;

    private bool _navigationUpdateInProgress;

    /// <summary>
    /// Запоминает последнюю выбранную вкладку каждого раздела.
    /// При возврате в раздел пользователь увидит прежнюю вкладку
    /// и её сохранённое содержимое.
    /// </summary>
    private readonly Dictionary<SectionId, NavigationTab> _lastTabs = new();

    public AppServices Services { get; }

    /// <summary>
    /// Общее состояние выполняющихся операций.
    /// Не зависит от выбранного раздела или вкладки.
    /// </summary>
    public ProcessingState Processing { get; } = new();

    public SingleCheckViewModel SingleCheck { get; }

    public BatchViewModel Batch { get; }

    public AnalysisSettingsViewModel AnalysisSettings { get; }

    public ImCheckViewModel ImCheck { get; }

    public ObservableCollection<NavigationSection> Sections { get; } = new();

    /// <summary>
    /// Вкладки только текущего раздела.
    /// </summary>
    public ObservableCollection<NavigationTab> VisibleTabs { get; } = new();

    public NavigationSection? SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (value == null || value == _selectedSection)
                return;

            if (!_navigationUpdateInProgress && !CanLeaveCurrentTab())
            {
                RestoreNavigationSelection();
                return;
            }

            SetProperty(ref _selectedSection, value);
            _selectedSectionId = value.Id;

            ActivateSection(value);
        }
    }

    public NavigationTab? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (value == null || value == _selectedTab)
                return;

            if (!_navigationUpdateInProgress && !CanLeaveCurrentTab())
            {
                RestoreNavigationSelection();
                return;
            }

            SetProperty(ref _selectedTab, value);
            _lastTabs[value.Section] = value;

            OnTabActivated(value);
        }
    }

    public MainViewModel(AppServices services)
    {
        Services = services;

        // ViewModel создаются один раз.
        // Переключение разделов их не уничтожает и не создаёт заново.
        SingleCheck = new SingleCheckViewModel(services);
        Batch = new BatchViewModel(services, Processing);
        AnalysisSettings = new AnalysisSettingsViewModel(services, Processing);
        ImCheck = new ImCheckViewModel(services, Processing, ReloadConfiguration);

        BuildNavigation();

        // Стартовое состояние:
        // раздел «Коммерческий код»,
        // вкладка «Групповая проверка».
        _lastTabs[SectionId.CommercialCode] =
            FindTab(TabId.BatchProcessing)!;

        _lastTabs[SectionId.InternetShop] =
            FindTab(TabId.ImCheck)!;

        _navigationUpdateInProgress = true;
        try
        {
            SelectedSection = Sections.First(s => s.Id == SectionId.CommercialCode);
        }
        finally
        {
            _navigationUpdateInProgress = false;
        }
    }

    /// <summary>
    /// Все разделы и вкладки приложения описываются в одном месте.
    ///
    /// Для добавления нового раздела в дальнейшем потребуется:
    /// 1. добавить SectionId;
    /// 2. создать его вкладки;
    /// 3. добавить NavigationSection в этот метод.
    ///
    /// Остальная логика навигации изменяться не должна.
    /// </summary>
    private void BuildNavigation()
    {
        var singleCode = new NavigationTab(
            TabId.SingleCode,
            SectionId.CommercialCode,
            "Один код",
            SingleCheck);

        var batchProcessing = new NavigationTab(
            TabId.BatchProcessing,
            SectionId.CommercialCode,
            "Групповая проверка",
            Batch);

        var analysisSettings = new NavigationTab(
            TabId.AnalysisSettings,
            SectionId.CommercialCode,
            "Настройки анализа",
            AnalysisSettings);

        var imCheck = new NavigationTab(
            TabId.ImCheck,
            SectionId.InternetShop,
            "Проверка для ИМ",
            ImCheck);

        var commercialCode = new NavigationSection(
            SectionId.CommercialCode,
            "Коммерческий код",
            new[]
            {
                singleCode,
                batchProcessing,
                analysisSettings
            });

        var internetShop = new NavigationSection(
            SectionId.InternetShop,
            "Интернет магазин",
            new[]
            {
                imCheck
            });

        Sections.Clear();
        Sections.Add(commercialCode);
        Sections.Add(internetShop);
    }

    private NavigationTab? FindTab(TabId id)
    {
        return Sections
            .SelectMany(section => section.Tabs)
            .FirstOrDefault(tab => tab.Id == id);
    }

    private void ActivateSection(NavigationSection section)
    {
        _navigationUpdateInProgress = true;

        try
        {
            VisibleTabs.Clear();

            foreach (var tab in section.Tabs)
                VisibleTabs.Add(tab);

            var tabToSelect =
                _lastTabs.TryGetValue(section.Id, out var previousTab)
                    && section.Tabs.Contains(previousTab)
                    ? previousTab
                    : section.Tabs.FirstOrDefault();

            if (tabToSelect != null)
            {
                SetProperty(ref _selectedTab, tabToSelect, nameof(SelectedTab));
                _lastTabs[section.Id] = tabToSelect;
                OnTabActivated(tabToSelect);
            }
        }
        finally
        {
            _navigationUpdateInProgress = false;
        }
    }

    private void OnTabActivated(NavigationTab tab)
    {
        // Обновляем проверку ИМ только при фактическом переходе
        // на её вкладку. Сам ImCheckViewModel при этом не пересоздаётся.
        if (tab.Id == TabId.ImCheck)
            ImCheck.Refresh();
    }

    private bool CanLeaveCurrentTab()
    {
        if (_selectedTab?.Id == TabId.AnalysisSettings)
            return AnalysisSettings.CanLeave();

        return true;
    }

    private void RestoreNavigationSelection()
    {
        _dispatcher.BeginInvoke(
            RestoreNavigationSelectionCore,
            DispatcherPriority.Input);
    }

    private void RestoreNavigationSelectionCore()
    {
        _navigationUpdateInProgress = true;

        try
        {
            OnPropertyChanged(nameof(SelectedSection));
            OnPropertyChanged(nameof(SelectedTab));
        }
        finally
        {
            _navigationUpdateInProgress = false;
        }
    }

    [RelayCommand]
    private void OpenProgramSettings()
    {
        Services.Windows.ShowProgramSettings();
    }

    [RelayCommand]
    private void OpenHelp()
    {
        Services.Windows.ShowHelp();
    }

    [RelayCommand]
    private void RunSelfTest()
    {
        Services.Windows.ShowSelfTest(
            SelfTest.Run(
                Services.Settings.Processing.ToProcessOptions()));
    }

    /// <summary>
    /// Перечитать файлы конфигурации после их изменения.
    /// </summary>
    public void ReloadConfiguration()
    {
        AnalysisSettings.ReloadConfiguration();
    }

    /// <summary>
    /// Остановить все выполняющиеся операции.
    /// Переключение разделов этот метод не вызывает.
    /// </summary>
    public Task StopProcessingAsync()
    {
        return Task.WhenAll(
            Batch.StopAsync(),
            ImCheck.StopAsync());
    }

    public bool ConfirmUnsavedSettingsOnClose()
    {
        if (!AnalysisSettings.IsDirty)
            return true;

        return Services.Dialogs.AskSaveChanges(UnsavedOnCloseText) switch
        {
            SaveChoice.Save => AnalysisSettings.SaveChanges(),
            SaveChoice.Discard => true,
            _ => false
        };
    }
}

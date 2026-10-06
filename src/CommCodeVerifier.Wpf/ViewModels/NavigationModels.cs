using System.Collections.ObjectModel;

namespace CommCodeVerifier.Wpf.ViewModels;

/// <summary>
/// Раздел верхнего уровня приложения.
/// </summary>
public enum SectionId
{
    CommercialCode,
    InternetShop,
    Hierarchy,
    Description
}

/// <summary>
/// Идентификатор вкладки (для всех разделов).
/// </summary>
public enum TabId
{
    SingleCode,
    BatchProcessing,
    Duplicates,
    AnalysisSettings,
    ImCheck,
    Hierarchy,
    Description
}

/// <summary>
/// Описание вкладки приложения.
/// ViewModel создаётся один раз и сохраняется на всё время жизни приложения.
/// </summary>
public sealed class NavigationTab
{
    public TabId Id { get; }

    public SectionId Section { get; }

    public string Title { get; }

    /// <summary>
    /// Объект ViewModel, который будет передан соответствующему View.
    /// </summary>
    public object ViewModel { get; }

    public NavigationTab(
        TabId id,
        SectionId section,
        string title,
        object viewModel)
    {
        Id = id;
        Section = section;
        Title = title;
        ViewModel = viewModel;
    }
}

/// <summary>
/// Описание раздела приложения.
/// </summary>
public sealed class NavigationSection
{
    public SectionId Id { get; }

    public string Title { get; }

    /// <summary>
    /// Вкладки раздела в порядке отображения.
    /// </summary>
    public IReadOnlyList<NavigationTab> Tabs { get; }

    /// <summary>
    /// Ключ стиля текста раздела в Styles.xaml.
    /// </summary>
    public string TextStyleKey { get; }

    public NavigationSection(
        SectionId id,
        string title,
        IReadOnlyList<NavigationTab> tabs,
        string textStyleKey)
    {
        Id = id;
        Title = title;
        Tabs = tabs;
        TextStyleKey = textStyleKey;
    }
}

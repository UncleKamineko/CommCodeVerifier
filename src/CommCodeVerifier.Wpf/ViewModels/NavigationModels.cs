using System.Collections.ObjectModel;

namespace CommCodeVerifier.Wpf.ViewModels;

/// <summary>
/// Раздел верхнего уровня приложения.
/// </summary>
public enum SectionId
{
    CommercialCode,
    InternetShop,
    Article
}

/// <summary>
/// Идентификатор вкладки.
/// </summary>
public enum TabId
{
    SingleCode,
    BatchProcessing,
    AnalysisSettings,
    ImCheck,
    MissingIm,
    ImAnalysisSettings,
    ProductTypeSelection,
    CylinderArticle,
    ArticleSettings
}

/// <summary>
/// Тип изделия раздела «Артикул». Каждому типу соответствует свой набор вкладок.
/// </summary>
public enum ProductType
{
    Cylinders,
    Valves,
    Vacuum,
    //AirPreparation - БПВ и компоненты (фильтры, регуляторы и т.д.)
    AirPreparation,
    Fittings,
    //ShutoffValves - запорная арматура
    ShutoffValves,
    //ElectricActuators - электрические приводы, линейные модули, моторы и т.п.
    ElectricActuators
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

    /// <summary>
    /// Тип изделия, к набору вкладок которого относится вкладка.
    /// null — вкладка показывается вместе с вкладками раздела, у которых тоже null.
    /// </summary>
    public ProductType? ProductType { get; }

    /// <summary>
    /// Вкладка входит в набор вкладок типа изделия:
    /// на ней показывается кнопка «Вернуться к выбору типа изделия».
    /// </summary>
    public bool BelongsToProductType => ProductType.HasValue;

    public NavigationTab(
        TabId id,
        SectionId section,
        string title,
        object viewModel,
        ProductType? productType = null)
    {
        Id = id;
        Section = section;
        Title = title;
        ViewModel = viewModel;
        ProductType = productType;
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

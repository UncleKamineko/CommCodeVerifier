using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeVerifier.Wpf.ViewModels;

/// <summary>
/// Кнопка выбора типа изделия на вкладке «Тип изделия».
/// </summary>
public sealed record ProductTypeItem(ProductType ProductType, string Title);

/// <summary>
/// Вкладка «Тип изделия» раздела «Артикул».
/// </summary>
public sealed partial class ProductTypeSelectionViewModel : ObservableObject
{
    private readonly Action<ProductType> _openProductType;

    /// <summary>
    /// Кнопки в порядке отображения.
    /// Сетка (ProductTypeSelectionView.xaml) заполняется по строкам, слева направо.
    /// </summary>
    public IReadOnlyList<ProductTypeItem> ProductTypes { get; } = new[]
    {
        new ProductTypeItem(ProductType.Cylinders, "Цилиндры"),
        new ProductTypeItem(ProductType.Valves, "Распределители"),
        new ProductTypeItem(ProductType.Vacuum, "Вакуум"),
        new ProductTypeItem(ProductType.AirPreparation, "Блоки подг. воздуха"),
        new ProductTypeItem(ProductType.Fittings, "Фитинги"),
        new ProductTypeItem(ProductType.ShutoffValves, "Запорная арматура"),
        new ProductTypeItem(ProductType.ElectricActuators, "Электроцилиндры и электрокомпоненты")
    };

    public ProductTypeSelectionViewModel(Action<ProductType> openProductType)
    {
        _openProductType = openProductType;
    }

    [RelayCommand]
    private void Select(ProductTypeItem? item)
    {
        if (item != null)
            _openProductType(item.ProductType);
    }
}

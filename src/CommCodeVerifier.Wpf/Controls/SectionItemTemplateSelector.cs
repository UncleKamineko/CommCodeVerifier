using System.Windows;
using System.Windows.Controls;
using CommCodeVerifier.Wpf.ViewModels;

namespace CommCodeVerifier.Wpf.Controls;

/// <summary>
/// Выбирает шаблон текста для конкретного раздела.
/// </summary>
public sealed class SectionItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? CommercialCodeTemplate { get; set; }

    public DataTemplate? InternetShopTemplate { get; set; }

    public override DataTemplate? SelectTemplate(
        object item,
        DependencyObject container)
    {
        if (item is not NavigationSection section)
            return base.SelectTemplate(item, container);

        return section.Id switch
        {
            SectionId.CommercialCode => CommercialCodeTemplate,
            SectionId.InternetShop => InternetShopTemplate,
            _ => base.SelectTemplate(item, container)
        };
    }
}

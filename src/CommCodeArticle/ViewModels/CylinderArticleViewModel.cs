using System.Windows;
using CommCodeArticle.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeArticle.ViewModels;

public sealed partial class CylinderArticleViewModel : ObservableObject
{
    private const string Caption = "Генерация артикула";

    [ObservableProperty]
    private string _commercialCode = string.Empty;

    [ObservableProperty]
    private string _article = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _manualAdditionalCode = string.Empty;

    [ObservableProperty]
    private bool _tailTooLong;

    [RelayCommand]
    private void Generate()
    {
        try
        {
            var generator = CylinderArticleGenerator.Load();
            var result = generator.Generate(CommercialCode, ManualAdditionalCode);

            Article = result.Article ?? string.Empty;
            Message = result.Message;
            TailTooLong = result.TailTooLong;

            if (result.Status is CylinderArticleStatus.InvalidInput
                or CylinderArticleStatus.MidNotFound)
            {
                MessageBox.Show(result.Message, Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (result.Status == CylinderArticleStatus.ConfigurationError)
            {
                MessageBox.Show(result.Message, "Ошибка конфигурации", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            Article = string.Empty;
            TailTooLong = false;
            Message = "Не удалось загрузить карты артикула: " + ex.Message;
            MessageBox.Show(Message, "Ошибка конфигурации", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

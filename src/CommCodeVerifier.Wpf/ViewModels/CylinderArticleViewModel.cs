using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeVerifier.Wpf.ViewModels;

public sealed partial class CylinderArticleViewModel : ObservableObject
{
    private readonly AppServices _services;

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

    public CylinderArticleViewModel(AppServices services)
    {
        _services = services;
    }

    [RelayCommand]
    private void Generate()
    {
        try
        {
            var generator =
                CylinderArticleGenerator.Load(_services.Settings.Processing);

            var result =
                generator.Generate(CommercialCode, ManualAdditionalCode);

            Article = result.Article ?? string.Empty;
            Message = result.Message;
            TailTooLong = result.TailTooLong;

            if (result.Status is CylinderArticleStatus.InvalidInput
                or CylinderArticleStatus.MidNotFound)
            {
                _services.Dialogs.Warning(
                    result.Message,
                    "Генерация артикула");
            }
            else if (result.Status == CylinderArticleStatus.ConfigurationError)
            {
                _services.Dialogs.Error(
                    result.Message,
                    "Ошибка конфигурации");
            }
        }
        catch (Exception ex)
        {
            Article = string.Empty;
            TailTooLong = false;
            Message = "Не удалось загрузить карты артикула: " + ex.Message;

            _services.Dialogs.Error(
                Message,
                "Ошибка конфигурации");
        }
    }
}

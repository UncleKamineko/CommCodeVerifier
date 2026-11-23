using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeVerifier.Wpf.ViewModels;

public sealed partial class CylinderArticleViewModel : ObservableObject, IResettableViewModel
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


    /// <summary>
    /// Нормализованный код, в котором отмечены символы, заменённые с кириллицы на латиницу.
    /// null — замен не было или пользователь изменил код после них.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CyrillicReplaced))]
    private IReadOnlyList<TextSegment>? _replacedCodeHighlight;

    /// <summary>
    /// Код в поле ввода записывает программа, а не пользователь.
    /// </summary>
    private bool _commercialCodeNormalizing;

    public bool CyrillicReplaced => ReplacedCodeHighlight != null;

    public CylinderArticleViewModel(AppServices services)
    {
        _services = services;
    }

    /// <summary>
    /// Очистить введённые данные и результат генерации.
    /// </summary>
    public void Reset()
    {
        CommercialCode = string.Empty;
        ManualAdditionalCode = string.Empty;
        Article = string.Empty;
        Message = string.Empty;
        TailTooLong = false;
    }

    partial void OnCommercialCodeChanged(string value)
    {
        // Подсветка и сообщение о замене снимаются при первом изменении кода пользователем.
        if (!_commercialCodeNormalizing)
            ReplacedCodeHighlight = null;
    }

    /// <summary>
    /// Если в коде была кириллица, в поле ввода записывается нормализованный код,
    /// а заменённые символы отмечаются для подсветки.
    /// </summary>
    private void ReplaceCyrillic()
    {
        var normalized = CylinderArticleGenerator.Normalize(CommercialCode, out var replaced);
        if (replaced.Count == 0)
            return;

        _commercialCodeNormalizing = true;
        try
        {
            CommercialCode = normalized;
        }
        finally
        {
            _commercialCodeNormalizing = false;
        }

        ReplacedCodeHighlight = CodeProcessor.Segments(normalized, replaced)
            .Select(segment => new TextSegment(
                normalized.Substring(segment.start, segment.len),
                segment.marked))
            .ToList();
    }
    [RelayCommand]
    private void Generate()
    {
        ReplaceCyrillic();
        try
        {
            var generator =
                CylinderArticleGenerator.Load(_services.Settings.Processing);

            var result =
                generator.Generate(CommercialCode, ManualAdditionalCode);

            Article = result.Article ?? string.Empty;
            Message = result.Message;
            TailTooLong = result.TailTooLong;

            if (result.Status == CylinderArticleStatus.ConfigurationError)
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

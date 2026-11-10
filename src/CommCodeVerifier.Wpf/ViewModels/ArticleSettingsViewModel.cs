using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeVerifier.Wpf.ViewModels;

public sealed partial class ArticleSettingsViewModel : ObservableObject
{
    private const string ExcelFilter =
        "Книги Excel (*.xlsx)|*.xlsx";

    private readonly AppServices _services;

    [ObservableProperty]
    private string _alenMapPath;

    [ObservableProperty]
    private string _midMapPath;

    [ObservableProperty]
    private string _dopCodeMapPath;

    public string InstructionPath => ArticleConfigPaths.Instruction;

    public ArticleSettingsViewModel(AppServices services)
    {
        _services = services;

        var settings = services.Settings.Processing;

        _alenMapPath = ArticleConfigPaths.ResolveAlenMap(settings);
        _midMapPath = ArticleConfigPaths.ResolveMidMap(settings);
        _dopCodeMapPath = ArticleConfigPaths.ResolveDopCodeMap(settings);
    }

    [RelayCommand]
    private void PickAlenMap()
    {
        PickMap(
            "Выберите карту ALEN",
            path =>
            {
                _services.Settings.Processing.ArticleAlenMapFilePath = path;
                AlenMapPath = path;
            });
    }

    [RelayCommand]
    private void PickMidMap()
    {
        PickMap(
            "Выберите карту MID",
            path =>
            {
                _services.Settings.Processing.ArticleMidMapFilePath = path;
                MidMapPath = path;
            });
    }

    [RelayCommand]
    private void PickDopCodeMap()
    {
        PickMap(
            "Выберите карту дополнительного кода",
            path =>
            {
                _services.Settings.Processing.ArticleDopCodeMapFilePath = path;
                DopCodeMapPath = path;
            });
    }

    [RelayCommand]
    private void ResetMaps()
    {
        var settings = _services.Settings.Processing;

        settings.ArticleAlenMapFilePath = null;
        settings.ArticleMidMapFilePath = null;
        settings.ArticleDopCodeMapFilePath = null;

        AlenMapPath = ArticleConfigPaths.AlenMap;
        MidMapPath = ArticleConfigPaths.MidMap;
        DopCodeMapPath = ArticleConfigPaths.DopCodeMap;

        Save();
    }

    private void PickMap(string title, Action<string> setPath)
    {
        var path = _services.Dialogs.OpenFile(
            ExcelFilter,
            ArticleConfigPaths.Folder,
            title);

        if (path is null)
            return;

        setPath(path);
        Save();
    }

    private void Save()
    {
        try
        {
            _services.Settings.SaveProcessing();
        }
        catch (Exception ex)
        {
            _services.Dialogs.Error(
                "Не удалось сохранить настройки карт:" +
                Environment.NewLine +
                ex.Message);
        }
    }
}

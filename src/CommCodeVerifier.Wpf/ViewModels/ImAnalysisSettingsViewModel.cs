namespace CommCodeVerifier.Wpf.ViewModels;

/// <summary>
/// Объединяет настройки анализа пропущенного ИМ и выбор файлов правил ИМ.
/// </summary>
public sealed class ImAnalysisSettingsViewModel
{
    public ImAnalysisSettingsViewModel(
        MissingImViewModel missingIm,
        ImCheckViewModel imCheck)
    {
        MissingIm = missingIm;
        ImCheck = imCheck;
    }

    public MissingImViewModel MissingIm { get; }

    public ImCheckViewModel ImCheck { get; }
}

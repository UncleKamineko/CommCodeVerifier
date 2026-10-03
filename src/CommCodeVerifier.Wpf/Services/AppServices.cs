namespace CommCodeVerifier.Wpf.Services;

/// Сервисы программы — передаются моделям представления вместо прямой работы с окнами.
public sealed record AppServices(
    ISettingsService Settings,
    IDialogService Dialogs,
    IShellService Shell,
    IWindowService Windows);

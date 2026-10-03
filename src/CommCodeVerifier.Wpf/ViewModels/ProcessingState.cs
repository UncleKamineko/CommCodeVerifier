using CommunityToolkit.Mvvm.ComponentModel;

namespace CommCodeVerifier.Wpf.ViewModels;

/// <summary>
/// Идёт обработка (групповая проверка или проверка ИМ). Пока IsBusy, нельзя менять
/// настройки, влияющие на конфигурацию, и запускать вторую обработку.
/// Вызывается только из потока интерфейса.
/// </summary>
public sealed class ProcessingState : ObservableObject
{
    private int _count;

    public bool IsBusy => _count > 0;

    public void Begin()
    {
        _count++;
        if (_count == 1) OnPropertyChanged(nameof(IsBusy));
    }

    public void End()
    {
        if (_count == 0) return;
        _count--;
        if (_count == 0) OnPropertyChanged(nameof(IsBusy));
    }
}

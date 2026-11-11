namespace CommCodeVerifier.Wpf.ViewModels;

/// <summary>
/// ViewModel вкладки, данные которой сбрасываются
/// при возврате к выбору типа изделия.
/// </summary>
public interface IResettableViewModel
{
    void Reset();
}

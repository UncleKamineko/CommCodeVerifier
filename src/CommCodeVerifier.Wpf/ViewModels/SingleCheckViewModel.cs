using System.Collections.ObjectModel;
using CommCodeVerifier.Core;
using CommCodeVerifier.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommCodeVerifier.Wpf.ViewModels;

/// Фрагмент текста для подсветки: Marked — символы, затронутые правилами.
public sealed record TextSegment(string Text, bool Marked);

/// Вкладка «Один код».
public sealed partial class SingleCheckViewModel : ObservableObject
{
    public const string NoRulesText = "• Правила не применялись — значение корректно";

    public const string EmptyByArticleText =
        "УКАЗАННАЯ НОМЕНКЛАТУРА НЕ ПРЕДНАЗНАЧЕНА ДЛЯ РЕАЛИЗАЦИИ, КОММ. КОД УКАЗЫВАТЬ НЕ НУЖНО!";

    public const string EmptyResultText =
        "В РЕЗУЛЬТАТЕ АНАЛИЗА НЕ ОСТАЛОСЬ ЗНАЧАЩИХ СИМВОЛОВ, ОБРАБОТКА ВЕРНУЛА ПУСТОЕ ЗНАЧЕНИЕ! ПРОВЕРЬТЕ ИСХОДНЫЙ КОД!";

    private readonly AppServices _services;

    public SingleCheckViewModel(AppServices services) => _services = services;

    /// Введённый код.
    [ObservableProperty] private string _input = "";

    /// Исходный код с отмеченными изменёнными символами; null — проверки ещё не было или код изменён.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHighlight))]
    private IReadOnlyList<TextSegment>? _highlight;

    /// Исправленный код (если результат не пустой).
    [ObservableProperty] private string _result = "";

    [ObservableProperty] private bool _hasResult;

    /// Сообщение о пустом результате; null — результат не пустой.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmptyMessage))]
    private string? _emptyMessage;

    public bool HasHighlight => Highlight != null;
    public bool HasEmptyMessage => EmptyMessage != null;

    /// Применённые правила в порядке ТЗ.
    public ObservableCollection<string> AppliedRules { get; } = new();

    /// Подсветка относится к проверенному тексту: после правки поля она скрывается.
    partial void OnInputChanged(string value) => Highlight = null;

    [RelayCommand]
    private void Check()
    {
        string src = Input ?? "";
        var opts = _services.Settings.Processing.ToProcessOptions();

        // Артикул в одиночной проверке не вводится — правила 9 и 11 не срабатывают (как в WinForms).
        var res = CodeProcessor.Process(article: "", code: src, opts);

        AppliedRules.Clear();
        foreach (var rule in res.Applied)
            AppliedRules.Add("• " + RuleCatalog.ShortName(rule));
        if (res.Applied.Count == 0)
            AppliedRules.Add(NoRulesText);

        if (res.IsEmptyResult)
        {
            EmptyMessage = res.DeletedByArticle ? EmptyByArticleText : EmptyResultText;
            Result = "";
            HasResult = false;
        }
        else
        {
            EmptyMessage = null;
            Result = res.Result;
            HasResult = true;
        }

        var segments = new List<TextSegment>();
        foreach (var (start, len, marked) in CodeProcessor.Segments(src, res.Highlight))
            segments.Add(new TextSegment(src.Substring(start, len), marked));
        Highlight = segments;
    }
}

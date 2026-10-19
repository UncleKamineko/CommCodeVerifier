namespace CommCodeVerifier.Core;

public enum MissingImFragmentType
{
    Letters,
    Digits,
    LettersAndDigits,
    Fraction,
    Separator,
    Other
}

public sealed record MissingImFragment(
    int Position,
    string Text,
    MissingImFragmentType Type,
    int Length);

public sealed class MissingImCodeStructure
{
    public string Article { get; init; } = "";

    public string CommercialCode { get; init; } = "";
    /// <summary>
    /// Первый буквенный или цифровой фрагмент,
    /// обязательный для допуска референс-кода.
    /// </summary>
    public string? FirstRequiredFragment { get; init; }

    public IReadOnlyList<MissingImFragment> Fragments { get; init; } =
        Array.Empty<MissingImFragment>();

    public IReadOnlyList<string> Separators { get; init; } =
        Array.Empty<string>();

    public int CodeLength =>
        CommercialCode.Length;

    public string Signature =>
        string.Join(
            "|",
            Fragments.Select(fragment =>
                $"{fragment.Type}:{fragment.Length}"));
}

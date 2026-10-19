namespace CommCodeVerifier.Core;

public static class MissingImFirstFragment
{
    /// <summary>
    /// Возвращает первый буквенный или цифровой фрагмент
    /// коммерческого кода.
    ///
    /// Если код начинается с буквы, чтение продолжается
    /// до первой цифры, разделителя или пробела.
    ///
    /// Если код начинается с цифры, чтение продолжается
    /// до первой буквы, разделителя или пробела.
    ///
    /// Возвращается значение в верхнем регистре.
    /// </summary>
    public static string? Extract(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string normalized =
            value.Trim().ToUpperInvariant();

        if (normalized.Length == 0)
            return null;

        char first = normalized[0];

        if (char.IsLetter(first))
            return ReadLetters(normalized);

        if (char.IsDigit(first))
            return ReadDigits(normalized);

        return null;
    }

    /// <summary>
    /// Проверяет обязательное совпадение первых фрагментов.
    /// Пустые первые фрагменты не считаются совпавшими.
    /// </summary>
    public static bool AreEqual(
        string? candidateValue,
        string? referenceValue)
    {
        string? candidate =
            Extract(candidateValue);

        string? reference =
            Extract(referenceValue);

        return candidate != null &&
               reference != null &&
               string.Equals(
                   candidate,
                   reference,
                   StringComparison.Ordinal);
    }

    private static string? ReadLetters(
        string value)
    {
        int length = 0;

        while (length < value.Length &&
               char.IsLetter(value[length]))
        {
            length++;
        }

        return length == 0
            ? null
            : value[..length];
    }

    private static string? ReadDigits(
        string value)
    {
        int length = 0;

        while (length < value.Length &&
               char.IsDigit(value[length]))
        {
            length++;
        }

        return length == 0
            ? null
            : value[..length];
    }
}

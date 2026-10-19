namespace CommCodeVerifier.Core;

public sealed class MissingImSettings
{
    public MissingImWeights Weights { get; set; } = new();

    /// <summary>
    /// Минимальное сходство кандидата и референс-кода.
    /// Значение хранится в диапазоне от 0,00 до 1,00.
    /// </summary>
    public double MinimumCodeSimilarity { get; set; } = 0.80;

    /// <summary>
    /// Минимальная доля кодов с признаком ИМ в семействе.
    /// Значение хранится в диапазоне от 0,00 до 1,00.
    /// </summary>
    public double MinimumImPresenceInFamily { get; set; } = 0.60;

    /// <summary>
    /// Минимальное количество кодов с установленным признаком,
    /// необходимое для подтверждения семейства.
    /// </summary>
    public int MinimumPositiveReferences { get; set; } = 2;

    public void Normalize()
    {
        Weights ??= new MissingImWeights();
        Weights.Normalize();

        MinimumCodeSimilarity = Clamp(
            MinimumCodeSimilarity,
            0.00,
            1.00);

        MinimumImPresenceInFamily = Clamp(
            MinimumImPresenceInFamily,
            0.00,
            1.00);

        MinimumPositiveReferences =
            Math.Max(1, MinimumPositiveReferences);
    }

    public MissingImSettings Clone() =>
        new()
        {
            Weights = Weights.Clone(),
            MinimumCodeSimilarity = MinimumCodeSimilarity,
            MinimumImPresenceInFamily = MinimumImPresenceInFamily,
            MinimumPositiveReferences = MinimumPositiveReferences
        };

    private static double Clamp(
        double value,
        double min,
        double max)
    {
        if (double.IsNaN(value) ||
            double.IsInfinity(value))
        {
            return min;
        }

        return Math.Clamp(
            Math.Round(value, 2),
            min,
            max);
    }
}

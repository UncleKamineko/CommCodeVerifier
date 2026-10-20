namespace CommCodeVerifier.Core;

public sealed class MissingImLocalGroupThreshold
{
    /// <summary>
    /// Верхняя граница диапазона размера локальной группы.
    /// </summary>
    public int MaximumGroupSize { get; set; }

    public int MinimumPositiveReferences { get; set; }

    /// <summary>
    /// Доля от 0,00 до 1,00.
    /// </summary>
    public double MinimumPositiveRatio { get; set; }

    public MissingImLocalGroupThreshold Clone() => new()
    {
        MaximumGroupSize = MaximumGroupSize,
        MinimumPositiveReferences = MinimumPositiveReferences,
        MinimumPositiveRatio = MinimumPositiveRatio
    };
}
public sealed class MissingImSettings
{
    public MissingImWeights Weights { get; set; } = new();

    /// <summary>
    /// Минимальное сходство кандидата и референс-кода.
    /// Значение хранится в диапазоне от 0,00 до 1,00.
    /// </summary>
    public double MinimumCodeSimilarity { get; set; } = 0.80;

    /// <summary>
    /// Пороги отбора для локальной группы, включающей проверяемого кандидата.
    /// </summary>
    public List<MissingImLocalGroupThreshold> LocalGroupThresholds { get; set; } =
        CreateDefaultLocalGroupThresholds();
    public MissingImLocalGroupThreshold GetThresholdForGroupSize(int groupSize)
    {
        if (groupSize < 1)
            groupSize = 1;

        return LocalGroupThresholds
            .OrderBy(rule => rule.MaximumGroupSize)
            .First(rule => groupSize <= rule.MaximumGroupSize);
    }
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

        // Диапазоны фиксированы; настраиваются минимальное число референсов
        // и минимальная доля положительных записей.
        int[] maximumSizes = { 5, 20, 100, int.MaxValue };
        var defaults = CreateDefaultLocalGroupThresholds();

        LocalGroupThresholds ??=
            new List<MissingImLocalGroupThreshold>();

        var normalized =
            new List<MissingImLocalGroupThreshold>(4);

        for (int i = 0; i < maximumSizes.Length; i++)
        {
            var source = i < LocalGroupThresholds.Count
                ? LocalGroupThresholds[i]
                : defaults[i];

            normalized.Add(new MissingImLocalGroupThreshold
            {
                MaximumGroupSize = maximumSizes[i],

                MinimumPositiveReferences = Math.Clamp(
                    source?.MinimumPositiveReferences ??
                    defaults[i].MinimumPositiveReferences,
                    1,
                    maximumSizes[i]),

                MinimumPositiveRatio = Clamp(
                    source?.MinimumPositiveRatio ??
                    defaults[i].MinimumPositiveRatio,
                    0.00,
                    1.00)
            });
        }

        LocalGroupThresholds = normalized;
    }

    public MissingImSettings Clone() =>
        new()
        {
            Weights = (Weights ?? new MissingImWeights()).Clone(),
            MinimumCodeSimilarity = MinimumCodeSimilarity,
            LocalGroupThresholds = (LocalGroupThresholds ??
                CreateDefaultLocalGroupThresholds())
                .Where(rule => rule != null)
                .Select(rule => rule.Clone())
                .ToList()
        };
    public static List<MissingImLocalGroupThreshold>
        CreateDefaultLocalGroupThresholds() =>
        new()
        {
        new()
        {
            MaximumGroupSize = 5,
            MinimumPositiveReferences = 1,
            MinimumPositiveRatio = 0.20
        },
        new()
        {
            MaximumGroupSize = 20,
            MinimumPositiveReferences = 2,
            MinimumPositiveRatio = 0.50
        },
        new()
        {
            MaximumGroupSize = 100,
            MinimumPositiveReferences = 3,
            MinimumPositiveRatio = 0.75
        },
        new()
        {
            MaximumGroupSize = int.MaxValue,
            MinimumPositiveReferences = 5,
            MinimumPositiveRatio = 0.85
        }
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

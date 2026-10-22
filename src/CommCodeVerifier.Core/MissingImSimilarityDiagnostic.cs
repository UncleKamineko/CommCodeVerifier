namespace CommCodeVerifier.Core;

/// <summary>
/// Диагностический вклад характеристики в оценку пары кодов.
/// </summary>
public sealed class MissingImFeatureDiagnostic
{
    public string Name { get; init; } = "";

    public bool IsApplicable { get; init; } = true;
    /// <summary>Оценка характеристики от 0 до 1.</summary>
    public double Similarity { get; init; }

    /// <summary>Настроенный вес характеристики.</summary>
    public double Weight { get; init; }

    /// <summary>Доля веса характеристики от суммы весов, в процентах.</summary>
    public double WeightSharePercent { get; init; }

    /// <summary>Вклад характеристики в итоговый процент сходства.</summary>
    public double ContributionPercent { get; init; }

    /// <summary>
    /// Изменение итоговой оценки в процентных пунктах при наличии
    /// характеристики по сравнению с расчётом без неё.
    /// Положительное значение повышает итог, отрицательное — снижает.
    /// </summary>
    public double EffectOnScorePercentagePoints { get; init; }
}

/// <summary>
/// Диагностика сопоставления блоков после обязательного первого фрагмента.
/// Блоки показываются для объяснения оценки; их вклады не суммируются
/// с характеристиками, поскольку один блок может участвовать
/// в нескольких характеристиках.
/// </summary>
public sealed class MissingImBlockDiagnostic
{
    public int PositionAfterRequiredFragment { get; init; }

    public string CandidateBlock { get; init; } = "";

    public string ReferenceBlock { get; init; } = "";

    public string CandidateType { get; init; } = "";

    public string ReferenceType { get; init; } = "";

    public double BlockSimilarity { get; init; }

    public double PositionWeight { get; init; }
}

/// <summary>
/// Подробная диагностика сравнения кандидата с положительным референсом.
/// Временный файл для отладки и анализа, в релизной версии удалить.
/// </summary>
public sealed class MissingImPairDiagnostic
{
    public string CandidateArticle { get; init; } = "";

    public string CandidateCode { get; init; } = "";

    public string ReferenceArticle { get; init; } = "";

    public string ReferenceCode { get; init; } = "";

    public int LocalGroupSize { get; init; }

    public int PositiveReferenceCount { get; init; }

    public int MinimumPositiveReferences { get; init; }

    public double PositiveRatioPercent { get; init; }

    public double MinimumPositiveRatioPercent { get; init; }

    public double NeighborhoodSimilarityPercent { get; init; }

    public double SimilarityPercent { get; init; }

    public double MinimumSimilarityPercent { get; init; }

    public double ArticleSimilarityPercent { get; init; }

    public double MinimumArticleSimilarityPercent { get; init; }

    public string FirstCandidateNumericFragment { get; init; } = "";

    public string FirstReferenceNumericFragment { get; init; } = "";

    public bool FirstNumericFragmentDiffers { get; init; }

    public string PairDecision { get; set; } = "";

    public string CandidateDecision { get; set; } = "";

    public List<MissingImFeatureDiagnostic> Features { get; init; } = new();

    public List<MissingImBlockDiagnostic> Blocks { get; init; } = new();
}

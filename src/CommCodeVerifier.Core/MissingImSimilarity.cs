namespace CommCodeVerifier.Core;

// Замечание - Приведённая модель возможно потребует доработки в ходе реализации так, чтобы буквенно-цифровые блоки M2, L100
// и дроби 6-1/8 сравнивались по внутренним структурным сегментам, а не только как отдельные простые фрагменты.
public sealed class MissingImSimilarityResult
{
    public double Score { get; init; }

    public double ScorePercent =>
        Score * 100.0;

    public int MatchedFeatures { get; init; }

    public int TotalFeatures { get; init; }

    public string Signature { get; init; } = "";

    public string MatchedSegments { get; init; } = "";

    public string DifferentSegments { get; init; } = "";

    public string Basis { get; init; } = "";

    public List<MissingImFeatureDiagnostic> FeatureDiagnostics { get; init; } = new();

    public List<MissingImBlockDiagnostic> BlockDiagnostics { get; init; } = new();
}

public static class MissingImSimilarity
{
    public static MissingImSimilarityResult Compare(
        MissingImCodeStructure candidate,
        MissingImCodeStructure reference,
        double articleSimilarity,
        double imPresenceInFamily,
        MissingImWeights weights,
        bool includeImPresence = true,
        bool includeArticleSimilarity = true,
        double positionDecay = 0.75,
        bool includeDiagnostics = false)
    {
        weights ??= new MissingImWeights();
        weights.Normalize();
        positionDecay = Clamp(positionDecay);

        var features = new List<(string Name, double Value, double Weight)>
        {
            (
                "Типы фрагментов",
                CompareFragmentTypes(candidate, reference, positionDecay),
                weights.FragmentType
            ),
            (
                "Позиции фрагментов",
                CompareFragmentPositions(candidate, reference, positionDecay),
                weights.FragmentPosition
            ),
            (
                "Буквенные сегменты",
                CompareLetterSegments(candidate, reference, positionDecay),
                weights.LetterSegments
            ),
            (
                "Позиции буквенных сегментов",
                CompareLetterPositions(candidate, reference, positionDecay),
                weights.LetterPositions
            ),
            (
                "Числовые блоки",
                CompareNumericBlocks(candidate, reference, positionDecay),
                weights.NumericBlocks
            ),
            (
                "Позиции числовых блоков",
                CompareNumericPositions(candidate, reference, positionDecay),
                weights.NumericPositions
            ),
            (
                "Длины фрагментов",
                CompareFragmentLengths(candidate, reference, positionDecay),
                weights.FragmentLengths
            ),
            (
                "Разделители",
                CompareSeparators(candidate, reference, positionDecay),
                weights.Separators
            ),
            (
                "Общая длина кода",
                CompareCodeLength(candidate, reference),
                weights.CodeLength
            )
        };

        if (includeArticleSimilarity)
        {
            features.Add((
                "Сходство артикулов",
                Clamp(articleSimilarity),
                weights.ArticleSimilarity));
        }

        if (includeImPresence)
        {
            features.Add((
                "Доля кодов с признаком в локальной группе",
                Clamp(imPresenceInFamily),
                weights.ImPresenceInFamily));
        }

        double weightSum = features.Sum(item => item.Weight);
        double weightedScore = weightSum <= 0.00
            ? 0.00
            : features.Sum(item => item.Value * item.Weight) / weightSum;

        var featureDiagnostics = includeDiagnostics
            ? features
                .Select(feature => CreateFeatureDiagnostic(
                    feature.Name,
                    feature.Value,
                    feature.Weight,
                    weightSum,
                    weightedScore))
                .ToList()
            : new List<MissingImFeatureDiagnostic>();

        var activeFeatures = features
            .Where(item => item.Weight > 0.00)
            .ToList();

        int matched = activeFeatures.Count(
            item => item.Value >= 0.50);

        string matchedSegments = string.Join(
            "; ",
            activeFeatures
                .Where(item => item.Value >= 0.50)
                .Select(item => item.Name));

        string differentSegments = string.Join(
            "; ",
            activeFeatures
                .Where(item => item.Value < 0.50)
                .Select(item => item.Name));

        return new MissingImSimilarityResult
        {
            Score = Clamp(weightedScore),
            MatchedFeatures = matched,
            TotalFeatures = activeFeatures.Count,
            Signature = candidate.Signature,
            MatchedSegments = matchedSegments,
            DifferentSegments = differentSegments,
            Basis = $"Совпало характеристик: {matched} из {activeFeatures.Count}",
            FeatureDiagnostics = featureDiagnostics,
            BlockDiagnostics = includeDiagnostics
                ? CompareBlocks(candidate, reference, positionDecay)
                : new List<MissingImBlockDiagnostic>()
        };
    }

    /// <summary>
    /// Структурная оценка для формирования локального окружения.
    /// Сравнивает последовательности блоков после обязательного префикса;
    /// вклад правых позиций уменьшается в соответствии с positionDecay.
    /// </summary>
    public static double CompareCodeBlocks(
        MissingImCodeStructure candidate,
        MissingImCodeStructure other,
        double positionDecay = 0.75)
    {
        if (!MissingImFirstFragment.AreEqual(
                candidate.CommercialCode,
                other.CommercialCode))
        {
            return 0.00;
        }

        positionDecay = Clamp(positionDecay);
        var left = Tail(candidate);
        var right = Tail(other);
        int count = Math.Max(left.Count, right.Count);

        if (count == 0)
            return 1.00;

        double score = 0.00;
        double weight = 0.00;

        for (int i = 0; i < count; i++)
        {
            double positionWeight = PositionWeight(i, positionDecay);

            if (i < left.Count &&
                i < right.Count &&
                left[i].Type == right[i].Type)
            {
                score += TextSimilarity(left[i].Text, right[i].Text) *
                         positionWeight;
            }

            weight += positionWeight;
        }

        return weight == 0.00
            ? 1.00
            : Clamp(score / weight);
    }

    public static double CompareArticles(
        string candidateArticle,
        string referenceArticle)
    {
        candidateArticle ??= "";
        referenceArticle ??= "";

        if (candidateArticle.Length == 0 &&
            referenceArticle.Length == 0)
        {
            return 1.00;
        }

        if (candidateArticle.Length == 0 ||
            referenceArticle.Length == 0)
        {
            return 0.00;
        }

        int distance = LevenshteinDistance(
            candidateArticle,
            referenceArticle);

        int maxLength = Math.Max(
            candidateArticle.Length,
            referenceArticle.Length);

        return maxLength == 0
            ? 1.00
            : Clamp(1.0 - (double)distance / maxLength);
    }

    private static MissingImFeatureDiagnostic CreateFeatureDiagnostic(
        string name,
        double value,
        double weight,
        double totalWeight,
        double totalScore)
    {
        double weightShare = totalWeight <= 0.00
            ? 0.00
            : weight / totalWeight;

        double contribution = value * weightShare;
        double remainingWeight = totalWeight - weight;

        double effectWithoutFeature =
            weight <= 0.00 || remainingWeight <= 0.00
                ? 0.00
                : totalScore -
                  ((totalScore * totalWeight - value * weight) /
                   remainingWeight);

        return new MissingImFeatureDiagnostic
        {
            Name = name,
            Similarity = value,
            Weight = weight,
            WeightSharePercent = weightShare * 100.00,
            ContributionPercent = contribution * 100.00,
            EffectOnScorePercentagePoints = effectWithoutFeature * 100.00
        };
    }

    private static List<MissingImBlockDiagnostic> CompareBlocks(
        MissingImCodeStructure candidate,
        MissingImCodeStructure reference,
        double positionDecay)
    {
        var left = Tail(candidate);
        var right = Tail(reference);
        int count = Math.Max(left.Count, right.Count);
        var blocks = new List<MissingImBlockDiagnostic>(count);

        for (int i = 0; i < count; i++)
        {
            MissingImFragment? leftFragment =
                i < left.Count ? left[i] : null;

            MissingImFragment? rightFragment =
                i < right.Count ? right[i] : null;

            double blockSimilarity =
                leftFragment == null ||
                rightFragment == null ||
                leftFragment.Type != rightFragment.Type
                    ? 0.00
                    : TextSimilarity(
                        leftFragment.Text,
                        rightFragment.Text);

            blocks.Add(new MissingImBlockDiagnostic
            {
                PositionAfterRequiredFragment = i + 1,
                CandidateBlock = leftFragment?.Text ?? "<нет блока>",
                ReferenceBlock = rightFragment?.Text ?? "<нет блока>",
                CandidateType = leftFragment?.Type.ToString() ?? "<нет блока>",
                ReferenceType = rightFragment?.Type.ToString() ?? "<нет блока>",
                BlockSimilarity = blockSimilarity,
                PositionWeight = PositionWeight(i, positionDecay)
            });
        }

        return blocks;
    }

    // Первый обязательный фрагмент сравнивается отдельно.
    // Здесь обрабатываются блоки, расположенные после него.
    private static IReadOnlyList<MissingImFragment> Tail(
        MissingImCodeStructure code) =>
        code.Fragments
            .Skip(
                string.IsNullOrWhiteSpace(code.FirstRequiredFragment)
                    ? 0
                    : 1)
            .ToList();

    private static double PositionWeight(
        int index,
        double positionDecay) =>
        Math.Pow(positionDecay, Math.Max(0, index));

    private static double CompareFragmentTypes(
        MissingImCodeStructure left,
        MissingImCodeStructure right,
        double positionDecay)
    {
        var a = Tail(left);
        var b = Tail(right);
        int count = Math.Max(a.Count, b.Count);

        if (count == 0)
            return 1.00;

        double score = 0.00;
        double weight = 0.00;

        for (int i = 0; i < count; i++)
        {
            double positionWeight = PositionWeight(i, positionDecay);

            bool matched =
                i < a.Count &&
                i < b.Count &&
                a[i].Type == b[i].Type;

            if (matched)
                score += positionWeight;

            weight += positionWeight;
        }

        return weight <= 0.00
            ? 1.00
            : score / weight;
    }

    private static double CompareFragmentPositions(
        MissingImCodeStructure left,
        MissingImCodeStructure right,
        double positionDecay)
    {
        var a = Tail(left);
        var b = Tail(right);
        int count = Math.Max(a.Count, b.Count);

        if (count == 0)
            return 1.00;

        double score = 0.00;
        double weight = 0.00;

        for (int i = 0; i < count; i++)
        {
            double positionWeight = PositionWeight(i, positionDecay);

            if (i < a.Count && i < b.Count)
                score += positionWeight;

            weight += positionWeight;
        }

        return weight <= 0.00
            ? 1.00
            : score / weight;
    }

    private static double CompareLetterSegments(
        MissingImCodeStructure left,
        MissingImCodeStructure right,
        double positionDecay) =>
        CompareTypedFragments(
            left,
            right,
            MissingImFragmentType.Letters,
            positionDecay,
            (a, b) => TextSimilarity(a.Text, b.Text));

    private static double CompareLetterPositions(
        MissingImCodeStructure left,
        MissingImCodeStructure right,
        double positionDecay) =>
        CompareTypedPositions(
            left,
            right,
            MissingImFragmentType.Letters,
            positionDecay);

    private static double CompareNumericBlocks(
        MissingImCodeStructure left,
        MissingImCodeStructure right,
        double positionDecay) =>
        CompareTypedFragments(
            left,
            right,
            MissingImFragmentType.Digits,
            positionDecay,
            (a, b) => TextSimilarity(a.Text, b.Text));

    private static double CompareNumericPositions(
        MissingImCodeStructure left,
        MissingImCodeStructure right,
        double positionDecay) =>
        CompareTypedPositions(
            left,
            right,
            MissingImFragmentType.Digits,
            positionDecay);

    private static double CompareTypedFragments(
        MissingImCodeStructure left,
        MissingImCodeStructure right,
        MissingImFragmentType type,
        double positionDecay,
        Func<MissingImFragment, MissingImFragment, double> compare)
    {
        var a = Tail(left);
        var b = Tail(right);
        int count = Math.Max(a.Count, b.Count);

        double score = 0.00;
        double weight = 0.00;

        for (int i = 0; i < count; i++)
        {
            MissingImFragment? leftFragment =
                i < a.Count && a[i].Type == type
                    ? a[i]
                    : null;

            MissingImFragment? rightFragment =
                i < b.Count && b[i].Type == type
                    ? b[i]
                    : null;

            // Если искомого типа блока нет с обеих сторон,
            // эта позиция неприменима к характеристике.
            if (leftFragment == null && rightFragment == null)
                continue;

            double positionWeight = PositionWeight(i, positionDecay);

            if (leftFragment != null && rightFragment != null)
            {
                score += compare(leftFragment, rightFragment) *
                         positionWeight;
            }

            weight += positionWeight;
        }

        // Нет применимых блоков — характеристика нейтральна,
        // а не считается совпавшей.
        return weight <= 0.00
            ? 0.50
            : score / weight;
    }

    private static double CompareTypedPositions(
        MissingImCodeStructure left,
        MissingImCodeStructure right,
        MissingImFragmentType type,
        double positionDecay)
    {
        var a = Tail(left);
        var b = Tail(right);
        int count = Math.Max(a.Count, b.Count);

        double score = 0.00;
        double weight = 0.00;

        for (int i = 0; i < count; i++)
        {
            bool leftHasType =
                i < a.Count &&
                a[i].Type == type;

            bool rightHasType =
                i < b.Count &&
                b[i].Type == type;

            // Отсутствие этого типа блока в обоих кодах
            // не является совпадением.
            if (!leftHasType && !rightHasType)
                continue;

            double positionWeight = PositionWeight(i, positionDecay);

            if (leftHasType && rightHasType)
                score += positionWeight;

            weight += positionWeight;
        }

        return weight <= 0.00
            ? 0.50
            : score / weight;
    }

    private static double CompareFragmentLengths(
        MissingImCodeStructure left,
        MissingImCodeStructure right,
        double positionDecay)
    {
        var a = Tail(left);
        var b = Tail(right);
        int count = Math.Max(a.Count, b.Count);

        if (count == 0)
            return 1.00;

        double score = 0.00;
        double weight = 0.00;

        for (int i = 0; i < count; i++)
        {
            double positionWeight = PositionWeight(i, positionDecay);

            if (i < a.Count && i < b.Count)
            {
                int maxLength = Math.Max(
                    a[i].Length,
                    b[i].Length);

                double lengthSimilarity = maxLength == 0
                    ? 1.00
                    : 1.00 -
                      (double)Math.Abs(a[i].Length - b[i].Length) /
                      maxLength;

                score += lengthSimilarity * positionWeight;
            }

            weight += positionWeight;
        }

        return weight <= 0.00
            ? 1.00
            : Clamp(score / weight);
    }

    private static double CompareSeparators(
        MissingImCodeStructure left,
        MissingImCodeStructure right,
        double positionDecay)
    {
        var a = Tail(left);
        var b = Tail(right);
        int count = Math.Max(a.Count, b.Count);

        double score = 0.00;
        double weight = 0.00;

        for (int i = 0; i < count; i++)
        {
            MissingImFragment? leftFragment =
                i < a.Count &&
                a[i].Type == MissingImFragmentType.Separator
                    ? a[i]
                    : null;

            MissingImFragment? rightFragment =
                i < b.Count &&
                b[i].Type == MissingImFragmentType.Separator
                    ? b[i]
                    : null;

            // Нет разделителей с обеих сторон — позиция неприменима.
            if (leftFragment == null && rightFragment == null)
                continue;

            double positionWeight = PositionWeight(i, positionDecay);

            if (leftFragment != null &&
                rightFragment != null &&
                string.Equals(
                    leftFragment.Text,
                    rightFragment.Text,
                    StringComparison.Ordinal))
            {
                score += positionWeight;
            }

            weight += positionWeight;
        }

        return weight <= 0.00
            ? 0.50
            : score / weight;
    }

    private static double CompareCodeLength(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
    {
        int maxLength = Math.Max(
            left.CodeLength,
            right.CodeLength);

        return maxLength == 0
            ? 1.00
            : Clamp(
                1.00 -
                (double)Math.Abs(
                    left.CodeLength - right.CodeLength) /
                maxLength);
    }

    private static double TextSimilarity(
        string left,
        string right)
    {
        left ??= "";
        right ??= "";

        if (left.Length == 0 && right.Length == 0)
            return 1.00;

        int maxLength = Math.Max(left.Length, right.Length);

        return maxLength == 0
            ? 1.00
            : Clamp(1.00 -
                    (double)LevenshteinDistance(
                        left.ToUpperInvariant(),
                        right.ToUpperInvariant()) /
                    maxLength);
    }

    private static int LevenshteinDistance(
        string left,
        string right)
    {
        int[,] matrix = new int[
            left.Length + 1,
            right.Length + 1];

        for (int i = 0; i <= left.Length; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= right.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= left.Length; i++)
        {
            for (int j = 1; j <= right.Length; j++)
            {
                int cost = left[i - 1] == right[j - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(
                        matrix[i - 1, j] + 1,
                        matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[left.Length, right.Length];
    }

    private static double Clamp(double value) =>
        double.IsNaN(value) || double.IsInfinity(value)
            ? 0.00
            : Math.Clamp(value, 0.00, 1.00);
}

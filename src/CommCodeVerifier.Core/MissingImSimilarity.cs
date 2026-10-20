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
        bool includeArticleSimilarity = true)
    {
        weights ??= new MissingImWeights();
        weights.Normalize();

        var features = new List<(string Name, double Value, double Weight)>
        {
            (
                "Типы фрагментов",
                CompareFragmentTypes(candidate, reference),
                weights.FragmentType
            ),
            (
                "Позиции фрагментов",
                CompareFragmentPositions(candidate, reference),
                weights.FragmentPosition
            ),
            (
                "Буквенные сегменты",
                CompareLetterSegments(candidate, reference),
                weights.LetterSegments
            ),
            (
                "Позиции буквенных сегментов",
                CompareLetterPositions(candidate, reference),
                weights.LetterPositions
            ),
            (
                "Числовые блоки",
                CompareNumericBlocks(candidate, reference),
                weights.NumericBlocks
            ),
            (
                "Позиции числовых блоков",
                CompareNumericPositions(candidate, reference),
                weights.NumericPositions
            ),
            (
                "Длины фрагментов",
                CompareFragmentLengths(candidate, reference),
                weights.FragmentLengths
            ),
            (
                "Разделители",
                CompareSeparators(candidate, reference),
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

        double score = weightSum <= 0
            ? 0
            : features.Sum(item => item.Value * item.Weight) /
              weightSum;

        var activeFeatures = features
            .Where(item => item.Weight > 0)
            .ToList();

        int matched = activeFeatures.Count(
            item => item.Value >= 0.5);

        string matchedSegments =
            string.Join(
                "; ",
                activeFeatures
                    .Where(item => item.Value >= 0.5)
                    .Select(item => item.Name));

        string differentSegments =
            string.Join(
                "; ",
                activeFeatures
                    .Where(item => item.Value < 0.5)
                    .Select(item => item.Name));

        return new MissingImSimilarityResult
        {
            Score = Clamp(score),
            MatchedFeatures = matched,
            TotalFeatures = activeFeatures.Count,
            Signature = candidate.Signature,
            MatchedSegments = matchedSegments,
            DifferentSegments = differentSegments,
            Basis =
                $"Совпало характеристик: {matched} из {activeFeatures.Count}"
        };
    }

    /// <summary>
    /// Структурная оценка для формирования локального окружения.
    /// Сравнивает последовательности блоков после обязательного префикса;
    /// вклад правых позиций экспоненциально уменьшается.
    /// </summary>
    public static double CompareCodeBlocks(
        MissingImCodeStructure candidate,
        MissingImCodeStructure other)
    {
        if (!MissingImFirstFragment.AreEqual(
                candidate.CommercialCode,
                other.CommercialCode))
        {
            return 0.00;
        }

        var left = Tail(candidate);
        var right = Tail(other);
        int count = Math.Max(left.Count, right.Count);

        if (count == 0)
            return 1.00;

        double score = 0.00;
        double weight = 0.00;

        for (int i = 0; i < count; i++)
        {
            double positionWeight = PositionWeight(i);

            if (i < left.Count &&
                i < right.Count &&
                left[i].Type == right[i].Type)
            {
                score += TextSimilarity(
                             left[i].Text,
                             right[i].Text) *
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

    // Первый обязательный фрагмент уже проверяется отдельно.
    // Для следующих позиций используется геометрическое затухание:
    // правые блоки имеют меньший вклад.
    private const double PositionDecay = 0.75;

    private static IReadOnlyList<MissingImFragment> Tail(
        MissingImCodeStructure code) =>
        code.Fragments
            .Skip(
                string.IsNullOrWhiteSpace(code.FirstRequiredFragment)
                    ? 0
                    : 1)
            .ToList();

    private static double PositionWeight(int index) =>
        Math.Pow(PositionDecay, Math.Max(0, index));

    private static double CompareFragmentTypes(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
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
            double positionWeight = PositionWeight(i);

            bool matched =
                i < a.Count &&
                i < b.Count &&
                a[i].Type == b[i].Type;

            if (matched)
                score += positionWeight;

            weight += positionWeight;
        }

        return weight == 0.00
            ? 1.00
            : score / weight;
    }

    private static double CompareFragmentPositions(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
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
            double positionWeight = PositionWeight(i);

            if (i < a.Count && i < b.Count)
                score += positionWeight;

            weight += positionWeight;
        }

        return weight == 0.00
            ? 1.00
            : score / weight;
    }

    private static double CompareLetterSegments(
        MissingImCodeStructure left,
        MissingImCodeStructure right) =>
        CompareTypedFragments(
            left,
            right,
            MissingImFragmentType.Letters,
            (a, b) => TextSimilarity(a.Text, b.Text));

    private static double CompareLetterPositions(
        MissingImCodeStructure left,
        MissingImCodeStructure right) =>
        CompareTypedPositions(
            left,
            right,
            MissingImFragmentType.Letters);

    private static double CompareNumericBlocks(
        MissingImCodeStructure left,
        MissingImCodeStructure right) =>
        CompareTypedFragments(
            left,
            right,
            MissingImFragmentType.Digits,
            (a, b) => TextSimilarity(a.Text, b.Text));

    private static double CompareNumericPositions(
        MissingImCodeStructure left,
        MissingImCodeStructure right) =>
        CompareTypedPositions(
            left,
            right,
            MissingImFragmentType.Digits);

    private static double CompareTypedFragments(
        MissingImCodeStructure left,
        MissingImCodeStructure right,
        MissingImFragmentType type,
        Func<MissingImFragment, MissingImFragment, double> compare)
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
            MissingImFragment? leftFragment =
                i < a.Count && a[i].Type == type
                    ? a[i]
                    : null;

            MissingImFragment? rightFragment =
                i < b.Count && b[i].Type == type
                    ? b[i]
                    : null;

            double positionWeight = PositionWeight(i);

            if (leftFragment == null && rightFragment == null)
            {
                score += positionWeight;
            }
            else if (leftFragment != null &&
                     rightFragment != null)
            {
                score += compare(leftFragment, rightFragment) *
                         positionWeight;
            }

            weight += positionWeight;
        }

        return weight == 0.00
            ? 1.00
            : score / weight;
    }

    private static double CompareTypedPositions(
        MissingImCodeStructure left,
        MissingImCodeStructure right,
        MissingImFragmentType type)
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
            bool leftHasType =
                i < a.Count &&
                a[i].Type == type;

            bool rightHasType =
                i < b.Count &&
                b[i].Type == type;

            double positionWeight = PositionWeight(i);

            if (leftHasType == rightHasType)
                score += positionWeight;

            weight += positionWeight;
        }

        return weight == 0.00
            ? 1.00
            : score / weight;
    }

    private static double CompareFragmentLengths(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
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
            double positionWeight = PositionWeight(i);

            if (i < a.Count && i < b.Count)
            {
                int maxLength = Math.Max(
                    a[i].Length,
                    b[i].Length);

                double lengthSimilarity = maxLength == 0
                    ? 1.00
                    : 1.00 -
                      (double)Math.Abs(
                          a[i].Length - b[i].Length) /
                      maxLength;

                score += lengthSimilarity * positionWeight;
            }

            weight += positionWeight;
        }

        return weight == 0.00
            ? 1.00
            : Clamp(score / weight);
    }

    private static double CompareSeparators(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
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

            double positionWeight = PositionWeight(i);

            if (leftFragment == null &&
                rightFragment == null)
            {
                score += positionWeight;
            }
            else if (leftFragment != null &&
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

        return weight == 0.00
            ? 1.00
            : score / weight;
    }

    private static double CompareCodeLength(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
    {
        int maxLength = Math.Max(
            left.CodeLength,
            right.CodeLength);

        if (maxLength == 0)
            return 1.00;

        return Clamp(
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

        if (left.Length == 0 &&
            right.Length == 0)
        {
            return 1.00;
        }

        int maxLength = Math.Max(
            left.Length,
            right.Length);

        if (maxLength == 0)
            return 1.00;

        return Clamp(
            1.00 -
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
                int cost =
                    left[i - 1] == right[j - 1]
                        ? 0
                        : 1;

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

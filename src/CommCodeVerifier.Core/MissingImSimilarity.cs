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
        MissingImWeights weights)
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
            ),
            (
                "Сходство артикулов",
                Clamp(articleSimilarity),
                weights.ArticleSimilarity
            ),
            (
                "Доля кодов с признаком в семействе",
                Clamp(imPresenceInFamily),
                weights.ImPresenceInFamily
            )
        };

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

    private static double CompareFragmentTypes(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
    {
        int count = Math.Max(
            left.Fragments.Count,
            right.Fragments.Count);

        if (count == 0)
            return 1.00;

        int matches = left.Fragments
            .Zip(
                right.Fragments,
                (a, b) => a.Type == b.Type)
            .Count(value => value);

        return (double)matches / count;
    }

    private static double CompareFragmentPositions(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
    {
        return left.Fragments.Count ==
               right.Fragments.Count
            ? 1.00
            : 0.00;
    }

    private static double CompareLetterSegments(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
    {
        var leftLetters = left.Fragments
            .Where(fragment =>
                fragment.Type == MissingImFragmentType.Letters)
            .Select(fragment => fragment.Text)
            .ToList();

        var rightLetters = right.Fragments
            .Where(fragment =>
                fragment.Type == MissingImFragmentType.Letters)
            .Select(fragment => fragment.Text)
            .ToList();

        if (leftLetters.Count == 0 &&
            rightLetters.Count == 0)
        {
            return 1.00;
        }

        if (leftLetters.Count != rightLetters.Count)
            return 0.00;

        int matches = leftLetters
            .Zip(rightLetters, string.Equals)
            .Count(value => value);

        return leftLetters.Count == 0
            ? 1.00
            : (double)matches / leftLetters.Count;
    }

    private static double CompareLetterPositions(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
    {
        var leftPositions = left.Fragments
            .Where(fragment =>
                fragment.Type == MissingImFragmentType.Letters)
            .Select(fragment => fragment.Position)
            .ToList();

        var rightPositions = right.Fragments
            .Where(fragment =>
                fragment.Type == MissingImFragmentType.Letters)
            .Select(fragment => fragment.Position)
            .ToList();

        return leftPositions.SequenceEqual(rightPositions)
            ? 1.00
            : 0.00;
    }

    private static double CompareNumericBlocks(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
    {
        var leftNumbers = left.Fragments
            .Where(fragment =>
                fragment.Type == MissingImFragmentType.Digits)
            .Select(fragment => fragment.Text)
            .ToList();

        var rightNumbers = right.Fragments
            .Where(fragment =>
                fragment.Type == MissingImFragmentType.Digits)
            .Select(fragment => fragment.Text)
            .ToList();

        if (leftNumbers.Count == 0 &&
            rightNumbers.Count == 0)
        {
            return 1.00;
        }

        if (leftNumbers.Count != rightNumbers.Count)
            return 0.00;

        return 1.00;
    }

    private static double CompareNumericPositions(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
    {
        var leftPositions = left.Fragments
            .Where(fragment =>
                fragment.Type == MissingImFragmentType.Digits)
            .Select(fragment => fragment.Position)
            .ToList();

        var rightPositions = right.Fragments
            .Where(fragment =>
                fragment.Type == MissingImFragmentType.Digits)
            .Select(fragment => fragment.Position)
            .ToList();

        return leftPositions.SequenceEqual(rightPositions)
            ? 1.00
            : 0.00;
    }

    private static double CompareFragmentLengths(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
    {
        int count = Math.Max(
            left.Fragments.Count,
            right.Fragments.Count);

        if (count == 0)
            return 1.00;

        double sum = 0.00;

        for (int i = 0; i < count; i++)
        {
            if (i >= left.Fragments.Count ||
                i >= right.Fragments.Count)
            {
                continue;
            }

            int leftLength = left.Fragments[i].Length;
            int rightLength = right.Fragments[i].Length;

            int maxLength = Math.Max(
                leftLength,
                rightLength);

            sum += maxLength == 0
                ? 1.00
                : 1.00 -
                  (double)Math.Abs(
                      leftLength - rightLength) /
                  maxLength;
        }

        return Clamp(sum / count);
    }

    private static double CompareSeparators(
        MissingImCodeStructure left,
        MissingImCodeStructure right)
    {
        return left.Separators.SequenceEqual(
                right.Separators,
                StringComparer.Ordinal)
            ? 1.00
            : 0.00;
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

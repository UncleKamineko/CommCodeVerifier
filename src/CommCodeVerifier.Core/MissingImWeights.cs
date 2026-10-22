namespace CommCodeVerifier.Core;

public sealed class MissingImWeights
{
    public double FragmentType { get; set; } = 0.10;
    public double FragmentPosition { get; set; } = 0.25;
    public double LetterSegments { get; set; } = 1.00;
    public double LetterPositions { get; set; } = 0.25;
    public double NumericBlocks { get; set; } = 1.00;
    public double NumericPositions { get; set; } = 0.25;
    public double FragmentLengths { get; set; } = 0.80;
    public double Separators { get; set; } = 0.20;
    public double CodeLength { get; set; } = 0.10;
    public double ArticleSimilarity { get; set; } = 1.00;
    public double ImPresenceInFamily { get; set; } = 0.10;

    public MissingImWeights Clone() =>
        new()
        {
            FragmentType = FragmentType,
            FragmentPosition = FragmentPosition,
            LetterSegments = LetterSegments,
            LetterPositions = LetterPositions,
            NumericBlocks = NumericBlocks,
            NumericPositions = NumericPositions,
            FragmentLengths = FragmentLengths,
            Separators = Separators,
            CodeLength = CodeLength,
            ArticleSimilarity = ArticleSimilarity,
            ImPresenceInFamily = ImPresenceInFamily
        };

    public void Normalize()
    {
        FragmentType = NormalizeValue(FragmentType);
        FragmentPosition = NormalizeValue(FragmentPosition);
        LetterSegments = NormalizeValue(LetterSegments);
        LetterPositions = NormalizeValue(LetterPositions);
        NumericBlocks = NormalizeValue(NumericBlocks);
        NumericPositions = NormalizeValue(NumericPositions);
        FragmentLengths = NormalizeValue(FragmentLengths);
        Separators = NormalizeValue(Separators);
        CodeLength = NormalizeValue(CodeLength);
        ArticleSimilarity = NormalizeValue(ArticleSimilarity);
        ImPresenceInFamily = NormalizeValue(ImPresenceInFamily);
    }

    private static double NormalizeValue(double value)
    {
        if (double.IsNaN(value) ||
            double.IsInfinity(value))
        {
            return 0.00;
        }

        return Math.Clamp(
            Math.Round(value, 2),
            0.00,
            1.00);
    }
}

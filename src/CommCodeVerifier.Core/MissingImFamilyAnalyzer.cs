namespace CommCodeVerifier.Core;

public sealed class MissingImFamilyItem
{
    public string Article { get; init; } = "";

    public string CommercialCode { get; init; } = "";

    public string Group { get; init; } = "";

    public bool HasIm { get; init; }

    public MissingImCodeStructure Structure { get; init; } = new();
}

public sealed class MissingImFamily
{
    public string Signature { get; init; } = "";

    public List<MissingImFamilyItem> Items { get; } = new();

    public int TotalCount =>
        Items.Count;

    public int PositiveCount =>
        Items.Count(item => item.HasIm);

    public int NegativeCount =>
        Items.Count(item => !item.HasIm);

    public double ImPresenceRatio =>
        TotalCount == 0
            ? 0.00
            : (double)PositiveCount / TotalCount;
}

public static class MissingImFamilyAnalyzer
{
    // В дальнейшем необходимо расширить так, чтобы семейство учитывало не только строку Signature,
    // но и длину кода; количество фрагментов; тип фрагментов; позиции буквенных сегментов; длины буквенных сегментов;
    //позиции числовых блоков; разделители; структуру дроби; сходство артикулов.
    public static List<MissingImFamily> BuildFamilies(
        IEnumerable<MissingImFamilyItem> items)
    {
        var families = items
            .GroupBy(item => item.Structure.Signature)
            .Select(group =>
            {
                var family = new MissingImFamily
                {
                    Signature = group.Key
                };

                family.Items.AddRange(group);
                return family;
            })
            .ToList();

        return families;
    }
}

namespace CommCodeVerifier.Core;

public sealed class MissingImCandidate
{
    public string Article { get; init; } = "";

    public string CommercialCode { get; init; } = "";

    public string ReferenceCode { get; init; } = "";

    public string ReferenceArticle { get; init; } = "";

    public double SimilarityPercent { get; init; }

    public string Basis { get; init; } = "";

    public string Signature { get; init; } = "";

    public string MatchedSegments { get; init; } = "";

    public string DifferentSegments { get; init; } = "";

    public int PositiveFamilyCount { get; init; }

    public int NegativeFamilyCount { get; init; }

    public double ImPresencePercent { get; init; }
}

public sealed class MissingImReport
{
    public string OutputFolder { get; init; } = "";

    public string OutputFile { get; init; } = "";

    public int ScannedCount { get; init; }

    public int CandidateCount { get; init; }

    public List<MissingImCandidate> Candidates { get; } = new();
}

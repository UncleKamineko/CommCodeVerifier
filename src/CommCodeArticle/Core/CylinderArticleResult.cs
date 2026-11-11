namespace CommCodeArticle.Core;

public enum CylinderArticleStatus
{
    Generated,
    ManualAdditionalCode,
    MidNotFound,
    ExcludedSeries,
    InvalidInput,
    ConfigurationError
}

public sealed record CylinderArticleResult(
    CylinderArticleStatus Status,
    string? Article,
    string Message,
    string? Mid = null,
    string? Alen = null,
    string? Execution = null,
    string? AdditionalCode = null,
    bool TailTooLong = false);

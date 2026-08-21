namespace EPFOptimizerPro.Services.Models;

public enum AdaptiveTaskVerdict
{
    Unknown,
    Healthy,
    Attention,
    Unhealthy,
    NotApplicable
}

public enum AdaptiveTaskSeverity
{
    Information,
    Success,
    Warning,
    Critical,
    Unknown
}

public sealed record AdaptiveTaskEvidence(
    string Label,
    string Value,
    string? Unit = null);

public sealed record AdaptiveTaskStructuredResult(
    string TaskName,
    AdaptiveTaskVerdict Verdict,
    AdaptiveTaskSeverity Severity,
    string Summary,
    IReadOnlyList<AdaptiveTaskEvidence> Evidence,
    string? RecommendedTaskName,
    DateTimeOffset EvaluatedAt)
{
    public static AdaptiveTaskStructuredResult Unknown(
        string taskName,
        string summary,
        DateTimeOffset? evaluatedAt = null)
    {
        return new(
            taskName,
            AdaptiveTaskVerdict.Unknown,
            AdaptiveTaskSeverity.Unknown,
            summary,
            Array.Empty<AdaptiveTaskEvidence>(),
            null,
            evaluatedAt ?? DateTimeOffset.Now);
    }
}
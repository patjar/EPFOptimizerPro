namespace EPFOptimizerPro.Services.Models;

public sealed record TaskExecutionCycleSummary(
    int ExecutedCount,
    int ReusedCount,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt)
{
    public int TotalCount => ExecutedCount + ReusedCount;
    public TimeSpan? Duration => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : null;
}
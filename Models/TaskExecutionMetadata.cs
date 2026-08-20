namespace EPFOptimizerPro.Services.Models;

public enum TaskExecutionOrigin
{
    Audit,
    Optimize,
    ManualRerun
}

public enum TaskExecutionDisposition
{
    Executed,
    Reused
}

public sealed record TaskExecutionMetadata(
    string TaskName,
    TaskExecutionOrigin Origin,
    TaskExecutionDisposition Disposition,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ReusedAt,
    TimeSpan? Duration,
    int ExecutionCount,
    int ReuseCount);
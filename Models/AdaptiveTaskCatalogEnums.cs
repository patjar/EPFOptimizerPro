namespace EPFOptimizerPro.Services.Models;

public enum AdaptiveTaskCategory
{
    SystemHealth,
    Security,
    Network,
    Maintenance,
    Updates,
    Applications
}

public enum AdaptiveTaskExecutionKind
{
    Diagnostic,
    Cleanup,
    Repair,
    Inventory
}

public enum AdaptiveTaskRiskLevel
{
    None,
    Low,
    Medium,
    High
}

public enum AdaptiveTaskDurationKind
{
    Short,
    Medium,
    Long
}

public enum AdaptiveTaskHaloKind
{
    None,
    Specialized,
    Orange,
    Violet,
    Red
}
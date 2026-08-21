namespace EPFOptimizerPro.Services.Models;

public sealed record AdaptiveTaskDefinition(
    string Name,
    string Command,
    int TimeoutSeconds,
    bool AvailableInAudit,
    bool AvailableInOptimize,
    AdaptiveTaskCategory Category,
    string Description,
    AdaptiveTaskExecutionKind ExecutionKind,
    AdaptiveTaskRiskLevel RiskLevel,
    AdaptiveTaskDurationKind DurationKind,
    bool CanManualRerun,
    bool CanAutoRun,
    bool RequiresConfirmation,
    bool RequiresAdministrator,
    AdaptiveTaskHaloKind HaloKind,
    string ResultParserKey,
    string RecommendationKey);
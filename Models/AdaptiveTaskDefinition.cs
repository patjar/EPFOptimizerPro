namespace EPFOptimizerPro.Services.Models;

public sealed record AdaptiveTaskDefinition(
    string Name,
    string Command,
    int TimeoutSeconds,
    bool AvailableInAudit,
    bool AvailableInOptimize);
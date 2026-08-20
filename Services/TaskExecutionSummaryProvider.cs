using EPFOptimizerPro.Services.Models;

namespace EPFOptimizerPro.Services;

public static class TaskExecutionSummaryProvider
{
    public static string Build(TaskExecutionCycleSummary summary)
    {
        string executedLabel = summary.ExecutedCount > 1 ? "tâches exécutées" : "tâche exécutée";
        string reusedLabel = summary.ReusedCount > 1 ? "résultats réutilisés" : "résultat réutilisé";
        return $"{summary.ExecutedCount} {executedLabel} · {summary.ReusedCount} {reusedLabel}";
    }
}
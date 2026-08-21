using EPFOptimizerPro.Models;
using EPFOptimizerPro.Services.Models;

namespace EPFOptimizerPro.Services;

public sealed record CompletedTaskManualRerunResult(
    bool Handled,
    bool Completed,
    string Message);

public static class CompletedTaskManualRerunActionService
{
    public static async Task<CompletedTaskManualRerunResult> TryRunAsync(
        System.Windows.Window owner,
        TaskProgressInfo task,
        AdaptiveTaskEngine engine)
    {
        if (!AdaptiveTaskCatalog.TryGetDefinition(
                task.Name,
                out AdaptiveTaskDefinition? definition) ||
            definition is null ||
            !definition.CanManualRerun)
        {
            return new(false, false, string.Empty);
        }

        string taskName = definition.Name;
        bool isLongTask =
            definition.DurationKind == AdaptiveTaskDurationKind.Long;
        string warning = BuildWarning(definition);

        System.Windows.MessageBoxResult answer = System.Windows.MessageBox.Show(
            owner,
            $"Relancer uniquement {taskName} ?\n\n" +
            warning + "\n\n" +
            $"Le résultat {taskName} précédent sera remplacé. " +
            "Les autres résultats seront conservés.",
            $"Relance ciblée {taskName}",
            System.Windows.MessageBoxButton.YesNo,
            isLongTask
                ? System.Windows.MessageBoxImage.Warning
                : System.Windows.MessageBoxImage.Question,
            System.Windows.MessageBoxResult.No);

        if (answer != System.Windows.MessageBoxResult.Yes)
        {
            return new(
                true,
                false,
                $"Relance manuelle {taskName} annulée.");
        }

        bool completed = await engine.RunSingleTaskAsync(taskName);
        return completed
            ? new(true, true, $"Relance manuelle {taskName} terminée.")
            : new(
                true,
                false,
                $"Relance manuelle {taskName} non exécutée. Consultez le journal.");
    }

    private static string BuildWarning(AdaptiveTaskDefinition definition)
    {
        if (definition.DurationKind != AdaptiveTaskDurationKind.Long)
        {
            return "Cette opération est normalement rapide.";
        }

        return definition.Category == AdaptiveTaskCategory.Maintenance
            ? "Cette opération peut prendre plusieurs minutes et solliciter les volumes de stockage."
            : "Cette analyse peut être longue et solliciter le système jusqu'à sa fin.";
    }
}
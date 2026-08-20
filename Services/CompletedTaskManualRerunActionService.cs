using EPFOptimizerPro.Models;

namespace EPFOptimizerPro.Services;

public sealed record CompletedTaskManualRerunResult(
    bool Handled,
    bool Completed,
    string Message);

public static class CompletedTaskManualRerunActionService
{
    private static readonly HashSet<string> ShortTaskNames = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "DNS",
        "Temp User",
        "Temp Win",
        "Corbeille"
    };

    public static async Task<CompletedTaskManualRerunResult> TryRunAsync(
        System.Windows.Window owner,
        TaskProgressInfo task,
        AdaptiveTaskEngine engine)
    {
        if (!ShortTaskNames.Contains(task.Name))
        {
            return new(false, false, string.Empty);
        }

        string taskName = task.Name;
        System.Windows.MessageBoxResult answer = System.Windows.MessageBox.Show(
            owner,
            $"Relancer uniquement {taskName} ?\n\n" +
            $"Le résultat {taskName} précédent sera remplacé. " +
            "Les autres résultats seront conservés.",
            $"Relance ciblée {taskName}",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question,
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
}
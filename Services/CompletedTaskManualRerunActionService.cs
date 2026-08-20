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

    private static readonly IReadOnlyDictionary<string, string> LongTaskPrompts =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Volumes"] =
                "Cette opération peut prendre plusieurs minutes et solliciter les volumes de stockage.",
            ["SFC"] =
                "Cette analyse peut être longue et solliciter le système jusqu'à sa fin."
        };

    public static async Task<CompletedTaskManualRerunResult> TryRunAsync(
        System.Windows.Window owner,
        TaskProgressInfo task,
        AdaptiveTaskEngine engine)
    {
        bool isShortTask = ShortTaskNames.Contains(task.Name);
        bool isLongTask = LongTaskPrompts.TryGetValue(
            task.Name,
            out string? longWarning);

        if (!isShortTask && !isLongTask)
        {
            return new(false, false, string.Empty);
        }

        string taskName = task.Name;
        string warning = isLongTask
            ? longWarning!
            : "Cette opération est normalement rapide.";

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
            ? new(true,true,$"Relance manuelle {taskName} terminée.")
            : new(
                true,
                false,
                $"Relance manuelle {taskName} non exécutée. Consultez le journal.");
    }
}
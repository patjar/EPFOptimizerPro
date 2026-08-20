using EPFOptimizerPro.Models;

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
        if (!task.Name.Equals("DNS", StringComparison.OrdinalIgnoreCase))
        {
            return new(false, false, string.Empty);
        }

        System.Windows.MessageBoxResult answer = System.Windows.MessageBox.Show(
            owner,
            "Relancer uniquement DNS ?\n\n" +
            "Le résultat DNS précédent sera remplacé. " +
            "Les sept autres résultats seront conservés.",
            "Relance ciblée DNS",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question,
            System.Windows.MessageBoxResult.No);

        if (answer != System.Windows.MessageBoxResult.Yes)
        {
            return new(true, false, "Relance manuelle DNS annulée.");
        }

        bool completed = await engine.RunSingleTaskAsync("DNS");
        return completed
            ? new(true, true, "Relance manuelle DNS terminée.")
            : new(true, false, "Relance manuelle DNS non exécutée. Consultez le journal.");
    }
}
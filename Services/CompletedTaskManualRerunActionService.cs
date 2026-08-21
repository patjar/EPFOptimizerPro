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

        if (!definition.RequiresConfirmation)
        {
            return new(
                true,
                false,
                $"Relance manuelle {taskName} bloquée : confirmation non configurée dans le catalogue.");
        }

        System.Windows.MessageBoxResult answer = System.Windows.MessageBox.Show(
            owner,
            BuildConfirmationMessage(definition),
            $"Relance ciblée {taskName}",
            System.Windows.MessageBoxButton.YesNo,
            GetConfirmationIcon(definition),
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

    private static string BuildConfirmationMessage(
        AdaptiveTaskDefinition definition)
    {
        string description = string.IsNullOrWhiteSpace(definition.Description)
            ? "Action ciblée du catalogue."
            : definition.Description;

        return $"Relancer uniquement {definition.Name} ?\n\n" +
            $"Action : {description}\n" +
            $"Niveau de risque : {GetRiskLabel(definition.RiskLevel)}\n\n" +
            BuildDurationWarning(definition) + "\n\n" +
            $"Le résultat {definition.Name} précédent sera remplacé. " +
            "Les autres résultats seront conservés.";
    }

    private static string BuildDurationWarning(
        AdaptiveTaskDefinition definition)
    {
        if (definition.DurationKind != AdaptiveTaskDurationKind.Long)
        {
            return "Cette opération est normalement rapide.";
        }

        return definition.Category == AdaptiveTaskCategory.Maintenance
            ? "Cette opération peut prendre plusieurs minutes et solliciter les volumes de stockage."
            : "Cette analyse peut être longue et solliciter le système jusqu'à sa fin.";
    }

    private static string GetRiskLabel(AdaptiveTaskRiskLevel riskLevel)
    {
        return riskLevel switch
        {
            AdaptiveTaskRiskLevel.None => "Aucun",
            AdaptiveTaskRiskLevel.Low => "Faible",
            AdaptiveTaskRiskLevel.Medium => "Moyen",
            AdaptiveTaskRiskLevel.High => "Élevé",
            _ => "Non défini"
        };
    }

    private static System.Windows.MessageBoxImage GetConfirmationIcon(
        AdaptiveTaskDefinition definition)
    {
        return definition.DurationKind == AdaptiveTaskDurationKind.Long ||
               definition.RiskLevel >= AdaptiveTaskRiskLevel.Medium
            ? System.Windows.MessageBoxImage.Warning
            : System.Windows.MessageBoxImage.Question;
    }
}
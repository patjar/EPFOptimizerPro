using System.Text;

namespace EPFOptimizerPro.Services;

public static class AuditManagementSummaryProvider
{
    public static string Build(
        string name,
        string status,
        string progress,
        string message,
        IReadOnlyList<AuditProblemSummary> problems)
    {
        int errors = problems.Count(p => p.Severity == AuditProblemSeverity.Error);
        int warnings = problems.Count(p => p.Severity == AuditProblemSeverity.Warning);

        var builder = new StringBuilder();
        builder.AppendLine("RESUME DE L'AUDIT");
        builder.AppendLine();
        builder.AppendLine($"Tache          : {ValueOrDefault(name, "Audit")}");
        builder.AppendLine($"Statut         : {ValueOrDefault(status, "Inconnu")}");
        builder.AppendLine($"Progression    : {ValueOrDefault(progress, "0")} %");
        builder.AppendLine($"Erreurs        : {errors}");
        builder.AppendLine($"Avertissements : {warnings}");
        builder.AppendLine();
        builder.AppendLine("RESULTAT ACTUEL");
        builder.AppendLine();
        builder.AppendLine(ValueOrDefault(message, "Aucun detail disponible."));
        builder.AppendLine();
        builder.AppendLine(errors == 0 && warnings == 0
            ? "Etat global : aucun probleme detecte."
            : "Etat global : une attention est requise.");
        return builder.ToString().TrimEnd();
    }

    private static string ValueOrDefault(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
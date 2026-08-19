using System.Text;

namespace EPFOptimizerPro.Services;

public static class AuditProblemsSummaryProvider
{
    public static string Format(IReadOnlyList<AuditProblemSummary> problems)
    {
        if (problems.Count == 0)
        {
            return "Aucune erreur détectée.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{problems.Count} erreur(s) ou avertissement(s) détecté(s)");
        builder.AppendLine();

        foreach (AuditProblemSummary problem in problems)
        {
            string marker = problem.Severity switch
            {
                AuditProblemSeverity.Error => "[ERREUR]",
                AuditProblemSeverity.Warning => "[AVERT.]",
                _ => "[INFO]"
            };

            builder.AppendLine($"{marker} {problem.Name}");

            if (!string.IsNullOrWhiteSpace(problem.Status))
            {
                builder.AppendLine($"  Statut : {problem.Status}");
            }

            if (!string.IsNullOrWhiteSpace(problem.Progress))
            {
                builder.AppendLine($"  Progression : {problem.Progress}%");
            }

            if (!string.IsNullOrWhiteSpace(problem.Message))
            {
                builder.AppendLine($"  Message : {problem.Message}");
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}
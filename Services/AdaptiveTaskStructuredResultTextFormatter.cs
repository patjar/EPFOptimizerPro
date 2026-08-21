using System.Text;
using EPFOptimizerPro.Services.Models;

namespace EPFOptimizerPro.Services;

public static class AdaptiveTaskStructuredResultTextFormatter
{
    public static string Format(
        IReadOnlyList<AdaptiveTaskStructuredResult> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine("R\u00C9SULTATS STRUCTUR\u00C9S");
        builder.AppendLine("====================");
        builder.AppendLine();

        if (results.Count == 0)
        {
            builder.AppendLine(
                "Aucun resultat structure disponible. Lancez un audit ou une optimisation.");
            return builder.ToString().TrimEnd();
        }

        foreach (AdaptiveTaskStructuredResult result in results
                     .OrderBy(item => item.TaskName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(result.TaskName);
            builder.AppendLine(new string('-', result.TaskName.Length));
            builder.AppendLine($"Verdict        : {VerdictLabel(result.Verdict)}");
            builder.AppendLine($"Gravit\u00E9        : {SeverityLabel(result.Severity)}");
            builder.AppendLine($"R\u00E9sum\u00E9         : {result.Summary}");

            foreach (AdaptiveTaskEvidence evidence in result.Evidence)
            {
                string unit = string.IsNullOrWhiteSpace(evidence.Unit)
                    ? string.Empty
                    : " " + evidence.Unit;
                builder.AppendLine($"{evidence.Label,-15}: {evidence.Value}{unit}");
            }

            builder.AppendLine(
                "Recommandation : " +
                (string.IsNullOrWhiteSpace(result.RecommendedTaskName)
                    ? "Aucune"
                    : result.RecommendedTaskName));
            builder.AppendLine($"\u00C9valu\u00E9 le      : {result.EvaluatedAt:dd/MM/yyyy HH:mm:ss}");
            builder.AppendLine();
        }

        builder.AppendLine("Lecture seule : aucune recommandation n\u2019est ex\u00E9cut\u00E9e automatiquement.");
        return builder.ToString().TrimEnd();
    }

    private static string VerdictLabel(AdaptiveTaskVerdict verdict)
    {
        return verdict switch
        {
            AdaptiveTaskVerdict.Healthy => "Sain",
            AdaptiveTaskVerdict.Attention => "Attention",
            AdaptiveTaskVerdict.Unhealthy => "Critique",
            AdaptiveTaskVerdict.NotApplicable => "Non applicable",
            _ => "Inconnu"
        };
    }

    private static string SeverityLabel(AdaptiveTaskSeverity severity)
    {
        return severity switch
        {
            AdaptiveTaskSeverity.Success => "Succ\u00E8s",
            AdaptiveTaskSeverity.Warning => "Avertissement",
            AdaptiveTaskSeverity.Critical => "Critique",
            AdaptiveTaskSeverity.Information => "Information",
            _ => "Inconnue"
        };
    }
}
using System.Collections.Generic;
using System.Text;

using EPFOptimizerPro.Models;

namespace EPFOptimizerPro;

public static class AiDashboardTextFormatter
{
    public static string FormatSubScore(HealthScore health)
    {
        return $"Perf {health.Performance} | S\u00e9cu {health.Security} | Stockage {health.Storage} | Update {health.WindowsUpdate} | Stabilit\u00e9 {health.Stability}";
    }

    public static string FormatAdvice(HealthScore health, string trendText)
    {
        return health.Summary + System.Environment.NewLine + trendText;
    }

    public static string FormatRecommendations(IReadOnlyList<AiRecommendation> recommendations)
    {
        StringBuilder sb = new();
        sb.AppendLine("Assistant IA local");
        sb.AppendLine("==================");
        sb.AppendLine();
        sb.AppendLine("Lance un audit ou une optimisation pour g\u00e9n\u00e9rer la synth\u00e8se IA.");
        sb.AppendLine();

        foreach (AiRecommendation item in recommendations)
        {
            sb.AppendLine($"[{item.Severity}] {item.Title}");
            sb.AppendLine(item.Detail);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string FormatDetailedSynthesis(string healthText, string tipsText)
    {
        StringBuilder sb = new();
        sb.AppendLine("Synth\u00e8se IA d\u00e9taill\u00e9e");
        sb.AppendLine("====================");
        sb.AppendLine();
        sb.Append(healthText);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("Conseils");
        sb.AppendLine("--------");
        sb.Append(tipsText);
        return sb.ToString();
    }

    public static string FormatDetailedSynthesis(string healthText, string trendText, string tipsText)
    {
        StringBuilder sb = new();
        sb.AppendLine("Synth\u00e8se IA d\u00e9taill\u00e9e");
        sb.AppendLine("====================");
        sb.AppendLine();
        sb.Append(healthText);
        sb.AppendLine();
        sb.AppendLine(trendText);
        sb.AppendLine();
        sb.AppendLine("Conseils");
        sb.AppendLine("--------");
        sb.Append(tipsText);
        return sb.ToString();
    }
}

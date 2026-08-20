using System.Text;
using EPFOptimizerPro.Models;

namespace EPFOptimizerPro.Services;

public static class AuditDashboardMarkdownSummaryProvider
{
    public static string Build(IEnumerable<AuditDashboardCardModel> models)
    {
        List<AuditDashboardCardModel> list = models.ToList();
        AuditDashboardGlobalVerdict verdict =
            AuditDashboardGlobalVerdictService.Build(list, false);

        int success = list.Count(model => model.Status == AuditDashboardStatus.Success);
        int warning = list.Count(model => model.Status == AuditDashboardStatus.Warning);
        int error = list.Count(model => model.Status == AuditDashboardStatus.Error);
        int notRun = list.Count(model => model.Status == AuditDashboardStatus.NotRun);

        var builder = new StringBuilder();
        builder.AppendLine("VERDICT GLOBAL");
        builder.AppendLine();
        builder.AppendLine($"Etat global        : {verdict.Title}");
        builder.AppendLine($"Controles conformes: {success}");
        builder.AppendLine($"Attentions         : {warning}");
        builder.AppendLine($"Erreurs            : {error}");
        builder.AppendLine($"Non executes       : {notRun}");
        builder.AppendLine();
        builder.AppendLine("ETAT DES CONTROLES");
        builder.AppendLine();

        foreach (AuditDashboardCardModel model in Ordered(list))
        {
            builder.AppendLine($"[{StatusLabel(model.Status)}] {model.Title}");
            builder.AppendLine($"  Verdict : {model.StatusText}");
            builder.AppendLine($"  Detail  : {model.DetailText}");
        }

        return builder.ToString().TrimEnd();
    }

    private static IEnumerable<AuditDashboardCardModel> Ordered(
        IEnumerable<AuditDashboardCardModel> models)
    {
        string[] order = { "system", "updates", "versions", "msi", "git", "deadcode" };
        var byId = models.ToDictionary(model => model.Id, StringComparer.OrdinalIgnoreCase);
        foreach (string id in order)
        {
            if (byId.TryGetValue(id, out AuditDashboardCardModel? model))
                yield return model;
        }
    }

    private static string StatusLabel(AuditDashboardStatus status)
    {
        return status switch
        {
            AuditDashboardStatus.Success => "OK",
            AuditDashboardStatus.Warning => "ATTENTION",
            AuditDashboardStatus.Error => "ERREUR",
            AuditDashboardStatus.Running => "EN COURS",
            _ => "NON EXECUTE"
        };
    }
}
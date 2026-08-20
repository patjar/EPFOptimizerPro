using System.Windows.Media;
using EPFOptimizerPro.Models;

namespace EPFOptimizerPro.Services;

public sealed record AuditDashboardGlobalVerdict(
    string Title,
    string Summary,
    Color Accent,
    Color Background);

public static class AuditDashboardGlobalVerdictService
{
    public static AuditDashboardGlobalVerdict Build(
        IEnumerable<AuditDashboardCardModel> models,
        bool isRunning)
    {
        List<AuditDashboardCardModel> list = models.ToList();
        int success = list.Count(model => model.Status == AuditDashboardStatus.Success);
        int warning = list.Count(model => model.Status == AuditDashboardStatus.Warning);
        int error = list.Count(model => model.Status == AuditDashboardStatus.Error);
        int notRun = list.Count(model => model.Status == AuditDashboardStatus.NotRun);
        int running = list.Count(model => model.Status == AuditDashboardStatus.Running);

        string counts = $"{success} conforme(s) · {warning} attention · {error} erreur(s) · {notRun} non exécuté(s)";

        if (isRunning || running > 0)
        {
            return new("Vérification en cours", counts,
                Color.FromRgb(37, 99, 235), Color.FromRgb(239, 246, 255));
        }

        if (error > 0)
        {
            return new("Action requise", counts,
                Color.FromRgb(220, 38, 38), Color.FromRgb(254, 242, 242));
        }

        if (warning > 0)
        {
            return new("Attention requise", counts,
                Color.FromRgb(217, 119, 6), Color.FromRgb(255, 251, 235));
        }

        if (notRun > 0)
        {
            return new("Contrôles incomplets", counts,
                Color.FromRgb(100, 116, 139), Color.FromRgb(248, 250, 252));
        }

        return new("Environnement conforme", counts,
            Color.FromRgb(22, 163, 74), Color.FromRgb(240, 253, 244));
    }
}
using EPFOptimizerPro.Models;

namespace EPFOptimizerPro.Services;

public static class AuditDashboardStatusInterpreter
{
    public static AuditDashboardCardModel FromSystemAudit(int problemCount)
    {
        return problemCount == 0
            ? Success("system", "Audit système", "Résumé et problèmes détectés", "Conforme", "0 problème détecté")
            : Warning("system", "Audit système", "Résumé et problèmes détectés", "Attention", $"{problemCount} problème(s) détecté(s)");
    }

    public static AuditDashboardCardModel FromUpdateChannel(string report)
    {
        if (Contains(report, "Erreur GitHub") || Contains(report, "aucune release stable exploitable"))
            return Error("updates", "Canal stable", "Release GitHub et mises à jour", "Erreur", "Canal stable non exploitable");
        if (Contains(report, "aucune mise a jour disponible"))
            return Success("updates", "Canal stable", "Release GitHub et mises à jour", "À jour", ReadValue(report, "Release selectionnee"));
        if (Contains(report, "mise a jour disponible"))
            return Warning("updates", "Canal stable", "Release GitHub et mises à jour", "Mise à jour disponible", ReadValue(report, "Version distante"));
        return Warning("updates", "Canal stable", "Release GitHub et mises à jour", "À vérifier", ReadValue(report, "Resultat"));
    }

    public static AuditDashboardCardModel FromVersions(string report)
    {
        return Contains(report, "VERDICT : COHÉRENT")
            ? Success("versions", "Versions", "Projet, assembly, EXE et MSI", "Cohérentes", ReadValue(report, "Projet Version"))
            : Error("versions", "Versions", "Projet, assembly, EXE et MSI", "Incohérentes", ReadVerdict(report));
    }

    public static AuditDashboardCardModel FromMsi(string report)
    {
        return Contains(report, "VERDICT : PRET POUR PUBLICATION")
            ? Success("msi", "MSI et signature", "Authenticode et préparation publication", "Prêt à publier", "Signature valide et horodatée")
            : Error("msi", "MSI et signature", "Authenticode et préparation publication", "Attention requise", ReadVerdict(report));
    }

    public static AuditDashboardCardModel FromWindowsCertificate(string report)
    {
        return Contains(report, "VERDICT : CERTIFICAT INSTALLE ET VALIDE")
            ? Success("certificate", "Certificat Windows", "Présence du certificat de confiance", "Installé", "PROD_CLEARPASS présent et valide")
            : Error("certificate", "Certificat Windows", "Présence du certificat de confiance", "Non installé", ReadVerdict(report));
    }
    public static AuditDashboardCardModel FromGit(string report)
    {
        if (Contains(report, "VERDICT : DEPOT PROPRE ET SYNCHRONISE"))
            return Success("git", "Dépôt Git", "Branche, synchronisation et working tree", "Synchronisé", ReadValue(report, "Commit HEAD"));
        if (Contains(report, "PRET POUR PUSH") || Contains(report, "CHANGEMENTS A CONTROLER"))
            return Warning("git", "Dépôt Git", "Branche, synchronisation et working tree", "Attention", ReadVerdict(report));
        return Error("git", "Dépôt Git", "Branche, synchronisation et working tree", "Erreur", ReadVerdict(report));
    }

    public static AuditDashboardCardModel FromDeadCode(string report)
    {
        return Contains(report, "Aucun candidat evident detecte")
            ? Success("deadcode", "Code mort", "Analyse conservatrice en lecture seule", "Aucun candidat", "Analyse terminée")
            : Warning("deadcode", "Code mort", "Analyse conservatrice en lecture seule", "Candidats détectés", ReadValue(report, "Candidats detectes"));
    }

    public static AuditDashboardCardModel Running(string id, string title, string subtitle)
    {
        return new(
            id,
            title,
            subtitle,
            AuditDashboardStatus.Running,
            "Vérification...",
            $"Contrôle en cours · {DateTime.Now:HH:mm:ss}");
    }

    public static AuditDashboardCardModel Failed(
        string id,
        string title,
        string subtitle,
        string message)
    {
        return Error(id, title, subtitle, "Erreur", message);
    }
    public static AuditDashboardCardModel NotRun(string id, string title, string subtitle)
    {
        return new(id, title, subtitle, AuditDashboardStatus.NotRun, "Non exécuté", "Contrôle manuel disponible");
    }

    private static AuditDashboardCardModel Success(string id, string title, string subtitle, string status, string detail)
        => new(id, title, subtitle, AuditDashboardStatus.Success, status, WithTime(detail));

    private static AuditDashboardCardModel Warning(string id, string title, string subtitle, string status, string detail)
        => new(id, title, subtitle, AuditDashboardStatus.Warning, status, WithTime(detail));

    private static AuditDashboardCardModel Error(string id, string title, string subtitle, string status, string detail)
        => new(id, title, subtitle, AuditDashboardStatus.Error, status, WithTime(detail));

    private static string WithTime(string detail)
        => $"{Fallback(detail)} · {DateTime.Now:HH:mm:ss}";

    private static string ReadVerdict(string report)
    {
        string line = report.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(item => item.TrimStart().StartsWith("VERDICT", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        int separator = line.IndexOf(':');
        return separator >= 0 ? line[(separator + 1)..].Trim() : "Vérification requise";
    }

    private static string ReadValue(string report, string label)
    {
        string line = report.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(item => item.TrimStart().StartsWith(label, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        int separator = line.IndexOf(':');
        return separator >= 0 ? line[(separator + 1)..].Trim() : string.Empty;
    }

    private static bool Contains(string value, string expected)
        => value.Contains(expected, StringComparison.OrdinalIgnoreCase);

    private static string Fallback(string value)
        => string.IsNullOrWhiteSpace(value) ? "Contrôle terminé" : value;
}
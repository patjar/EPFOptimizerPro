namespace EPFOptimizerPro;

public static class UpdateLogTextProvider
{
    public const string PublicGitHubMode = "[INFO] Mode update : GitHub public sans token personnel.";
    public const string CheckingGitHubUpdates = "[INFO] V\u00e9rification GitHub des mises \u00e0 jour...";

    public static string NoUpdateAvailable(string currentVersion, string latestVersion)
    {
        return $"[OK] Aucune mise \u00e0 jour disponible. Version locale : {currentVersion}, GitHub : {latestVersion}";
    }

    public static string UpdateAvailable(string latestVersion)
    {
        return $"[INFO] Mise \u00e0 jour disponible : {latestVersion}";
    }

    public static string GitHubUpdateError(string message)
    {
        return "[ERROR] Erreur GitHub update : " + message;
    }

    public static string DownloadingUpdate(string assetName)
    {
        return "[INFO] T\u00e9l\u00e9chargement update : " + assetName;
    }

    public static string MsiValid(string msiPath)
    {
        return "[OK] MSI valide : " + msiPath;
    }

    public static string MsiDownloaded(string msiPath)
    {
        return "[OK] MSI t\u00e9l\u00e9charg\u00e9 : " + msiPath;
    }

    public const string AutomaticUpdateInstall = "[INFO] Installation automatique de l'update...";

    public const string VerifyMsiSignatureBeforeInstall = "[INFO] Verification signature MSI avant installation...";

    public static string UpdateInstallLogPath(string logPath)
    {
        return "[INFO] Log installation update : " + logPath;
    }

    public const string UpdateDownloadCanceled = "[WARN] T\u00e9l\u00e9chargement update annul\u00e9.";

    public static string UpdateDownloadError(string message)
    {
        return "[ERROR] Erreur t\u00e9l\u00e9chargement update : " + message;
    }
}

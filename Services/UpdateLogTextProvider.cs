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
}
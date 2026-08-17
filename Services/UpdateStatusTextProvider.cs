namespace EPFOptimizerPro;

public static class UpdateStatusTextProvider
{
    public const string DownloadingGithubMsi = "T\u00e9l\u00e9chargement du MSI GitHub...";
    public const string UpdateDownloadCanceled = "T\u00e9l\u00e9chargement update annul\u00e9";
    public const string UpdateDownloadError = "Erreur t\u00e9l\u00e9chargement update";

    public static string Downloaded(string msiFileName)
    {
        return "Update t\u00e9l\u00e9charg\u00e9e : " + msiFileName;
    }

    public static string Installation()
    {
        return UpdateStatusFormatter.Format("installation...");
    }

    public const string CheckingGithub = "Mise \u00e0 jour : v\u00e9rification GitHub...";

    public const string GithubError = "Mise \u00e0 jour : erreur GitHub";

    public static string CheckResult(bool updateAvailable, string latestVersion)
    {
        return updateAvailable
            ? "Mise \u00e0 jour disponible : " + latestVersion
            : "Aucune mise \u00e0 jour disponible";
    }
}

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
}
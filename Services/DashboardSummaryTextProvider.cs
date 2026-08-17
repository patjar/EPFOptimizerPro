namespace EPFOptimizerPro;

public static class DashboardSummaryTextProvider
{
    public static string Format(int done, int total, int running, string activeNames, int waiting, int warnings, int errors)
    {
        return $"  |  {done}/{total} termin\u00e9es  |  {running} en cours : {activeNames}  |  {waiting} attente  |  {warnings} avert.  |  {errors} erreur";
    }
}
namespace EPFOptimizerPro;

public static class ActionStatusTextProvider
{
    public const string CompletedTasksReportAvailable = "T\u00e2ches termin\u00e9es. Rapport disponible.";
    public const string OperationCanceled = "Op\u00e9ration annul\u00e9e.";
    public const string CancellationRequested = "Annulation demand\u00e9e.";
    public const string NonAdminLimitations = "Mode non administrateur : certaines optimisations syst\u00e8me peuvent \u00eatre limit\u00e9es.";
    public const string AdaptiveAuditRunning = "Audit adaptatif en cours.";
    public const string AdaptiveOptimizationRunning = "Optimisation adaptative en cours.";

    public static string AdaptiveRunStatus(bool optimize)
    {
        return optimize ? AdaptiveOptimizationRunning : AdaptiveAuditRunning;
    }
}
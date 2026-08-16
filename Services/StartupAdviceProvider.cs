namespace EPFOptimizerPro;

public static class StartupAdviceProvider
{
    public const string StepTitle = "Conseil de d\u00e9marrage";

    public const string ActionHint = "Conseil : lancez Audit seul pour analyser le poste, ou Optimiser pour corriger automatiquement.";

    public static string GetAssistantText()
    {
        return "Conseil de d\u00e9marrage" + System.Environment.NewLine + System.Environment.NewLine +
            "Lancez Audit seul pour obtenir un diagnostic du poste. Utilisez Optimiser quand vous voulez appliquer les corrections automatiquement." +
            System.Environment.NewLine;
    }
}
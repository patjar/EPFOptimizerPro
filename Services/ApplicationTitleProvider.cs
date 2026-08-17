namespace EPFOptimizerPro;

public static class ApplicationTitleProvider
{
    public static string CurrentTitle()
    {
        return "EPF Optimizer Pro Premium IA v" + ApplicationVersionProvider.GetDisplayVersion();
    }
}
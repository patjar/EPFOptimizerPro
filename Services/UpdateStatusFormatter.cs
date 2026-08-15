namespace EPFOptimizerPro;

public static class UpdateStatusFormatter
{
    public static string Format(string state)
    {
        return "Mode update : GitHub public sans token" + System.Environment.NewLine + "\u00c9tat update : " + state;
    }
}
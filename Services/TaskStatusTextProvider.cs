namespace EPFOptimizerPro;

public static class TaskStatusTextProvider
{
    public const string Completed = "Termin\u00e9";
    public const string Running = "En cours";
    public const string Waiting = "En attente";
    public const string Warning = "Avertissement";
    public const string Error = "Erreur";

    public static bool IsCompleted(string status)
    {
        return string.Equals(status, Completed, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRunning(string status)
    {
        return string.Equals(status, Running, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWaiting(string status)
    {
        return string.Equals(status, Waiting, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWarning(string status)
    {
        return string.Equals(status, Warning, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsError(string status)
    {
        return string.Equals(status, Error, StringComparison.OrdinalIgnoreCase);
    }
}

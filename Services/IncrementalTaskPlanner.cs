namespace EPFOptimizerPro.Services;

public static class IncrementalTaskPlanner
{
    public static ISet<string> CreateCompletedTaskNames(
        IEnumerable<string?> taskNames)
    {
        return new HashSet<string>(
            taskNames.Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!),
            StringComparer.OrdinalIgnoreCase);
    }

    public static bool ShouldSchedule(
        ISet<string> completedTaskNames,
        string taskName)
    {
        ArgumentNullException.ThrowIfNull(completedTaskNames);
        return !string.IsNullOrWhiteSpace(taskName)
            && !completedTaskNames.Contains(taskName);
    }
}
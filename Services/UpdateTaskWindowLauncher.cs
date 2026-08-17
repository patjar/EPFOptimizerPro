using System.Windows;
using EPFOptimizerPro.Windows;

namespace EPFOptimizerPro.Services;

public static class UpdateTaskWindowLauncher
{
    public static void ShowIfUpdateTask(Window owner, object? task)
    {
        if (task is null)
        {
            return;
        }

        string name = ReadTaskValue(task, "Name");
        if (!IsUpdateTask(name))
        {
            return;
        }

        var window = new UpdateManagementWindow(
            name,
            ReadTaskValue(task, "Status"),
            ReadTaskValue(task, "Progress"),
            ReadTaskValue(task, "Message"))
        {
            Owner = owner
        };

        window.ShowDialog();
    }

    private static bool IsUpdateTask(string name)
    {
        return name.Equals("Updates", StringComparison.OrdinalIgnoreCase)
            || name.Contains("update", StringComparison.OrdinalIgnoreCase)
            || name.Contains("mise", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadTaskValue(object task, string propertyName)
    {
        object? value = task.GetType().GetProperty(propertyName)?.GetValue(task);
        return value?.ToString() ?? string.Empty;
    }
}
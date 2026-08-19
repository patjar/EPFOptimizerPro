using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using EPFOptimizerPro.Windows;

namespace EPFOptimizerPro.Services;

public static class CompletedTaskActionLauncher
{
    public static void Show(Window owner, object? task, IEnumerable<object> completedTasks)
    {
        if (task is null)
        {
            return;
        }

        if (IsUpdateTask(task))
        {
            UpdateTaskWindowLauncher.ShowIfUpdateTask(owner, task);
            return;
        }

        if (IsAuditTask(task))
        {
            string name = ReadValue(task, "Name");
            string status = ReadValue(task, "Status");
            string progress = ReadValue(task, "Progress");
            string message = ReadValue(task, "Message");

            var window = new AuditManagementWindow(name, status, progress, message, completedTasks)
            {
                Owner = owner
            };

            window.ShowDialog();
        }
    }

    private static bool IsUpdateTask(object task)
    {
        string name = ReadValue(task, "Name");
        string icon = ReadValue(task, "Icon");

        return name.Equals("Updates", StringComparison.OrdinalIgnoreCase)
            || name.Contains("update", StringComparison.OrdinalIgnoreCase)
            || name.Contains("mise", StringComparison.OrdinalIgnoreCase)
            || icon.Contains("↑", StringComparison.OrdinalIgnoreCase)
            || icon.Contains("⬆", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAuditTask(object task)
    {
        string name = ReadValue(task, "Name");
        string icon = ReadValue(task, "Icon");

        return name.Contains("audit", StringComparison.OrdinalIgnoreCase)
            || name.Contains("analyse", StringComparison.OrdinalIgnoreCase)
            || name.Contains("diagnostic", StringComparison.OrdinalIgnoreCase)
            || icon.Contains("🔎", StringComparison.OrdinalIgnoreCase)
            || icon.Contains("🔍", StringComparison.OrdinalIgnoreCase)
            || icon.Contains("loupe", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadValue(object item, string propertyName)
    {
        PropertyInfo? property = item.GetType().GetProperty(propertyName);
        object? value = property?.GetValue(item);
        return value?.ToString() ?? string.Empty;
    }
}
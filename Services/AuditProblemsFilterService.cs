using System.Globalization;
using System.Reflection;
using System.Text;

namespace EPFOptimizerPro.Services;

public enum AuditProblemSeverity
{
    Info,
    Warning,
    Error
}

public sealed record AuditProblemSummary(
    string Name,
    string Status,
    string Progress,
    string Message,
    AuditProblemSeverity Severity);

public static class AuditProblemsFilterService
{
    public static IReadOnlyList<AuditProblemSummary> GetErrors(IEnumerable<object> tasks)
    {
        return tasks
            .Select(ToSummary)
            .Where(IsErrorOrWarning)
            .OrderByDescending(t => t.Severity)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AuditProblemSummary ToSummary(object task)
    {
        string name = ReadValue(task, "Name");
        string status = ReadValue(task, "Status");
        string progress = ReadValue(task, "Progress");
        string message = ReadValue(task, "Message");
        AuditProblemSeverity severity = DetermineSeverity(status, message);

        return new AuditProblemSummary(name, status, progress, message, severity);
    }

    private static bool IsErrorOrWarning(AuditProblemSummary task)
    {
        return task.Severity is AuditProblemSeverity.Error or AuditProblemSeverity.Warning;
    }

    private static AuditProblemSeverity DetermineSeverity(string status, string message)
    {
        string combined = Normalize(status + " " + message);

        if (combined.Contains("erreur") || combined.Contains("error") || combined.Contains("failed") || combined.Contains("echec"))
        {
            return AuditProblemSeverity.Error;
        }

        if (combined.Contains("avert") || combined.Contains("warning") || combined.Contains("attention"))
        {
            return AuditProblemSeverity.Warning;
        }

        return AuditProblemSeverity.Info;
    }

    private static string ReadValue(object item, string propertyName)
    {
        PropertyInfo? property = item.GetType().GetProperty(propertyName);
        object? value = property?.GetValue(item);
        return value?.ToString() ?? string.Empty;
    }

    private static string Normalize(string value)
    {
        string lower = value.Trim().ToLowerInvariant();
        string decomposed = lower.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (char c in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
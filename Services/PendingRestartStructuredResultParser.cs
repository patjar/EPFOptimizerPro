using EPFOptimizerPro.Services.Models;

namespace EPFOptimizerPro.Services;

public static class PendingRestartStructuredResultParser
{
    public const string TaskName = "Red\u00E9marrage en attente";
    private const string Marker = "EPF_STRUCTURED_PENDING_RESTART";

    public static bool TryParse(
        string output,
        out AdaptiveTaskStructuredResult? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(output) ||
            !output.Contains(Marker, StringComparison.Ordinal))
        {
            return false;
        }

        Dictionary<string, string> values = output
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        if (!TryReadBoolean(values, "Pending", out bool pending) ||
            !TryReadBoolean(values, "CBS", out bool cbs) ||
            !TryReadBoolean(values, "WindowsUpdate", out bool windowsUpdate) ||
            !TryReadBoolean(values, "PendingFileRename", out bool pendingFileRename))
        {
            result = AdaptiveTaskStructuredResult.Unknown(
                TaskName,
                "L'\u00E9tat de red\u00E9marrage est incomplet.");
            return true;
        }

        AdaptiveTaskVerdict verdict = pending
            ? AdaptiveTaskVerdict.Attention
            : AdaptiveTaskVerdict.Healthy;
        AdaptiveTaskSeverity severity = pending
            ? AdaptiveTaskSeverity.Warning
            : AdaptiveTaskSeverity.Success;
        string summary = pending
            ? "Un red\u00E9marrage Windows est requis."
            : "Aucun red\u00E9marrage Windows n'est requis.";

        result = new AdaptiveTaskStructuredResult(
            TaskName,
            verdict,
            severity,
            summary,
            new[]
            {
                new AdaptiveTaskEvidence("Red\u00E9marrage requis", ToFrenchBoolean(pending)),
                new AdaptiveTaskEvidence("Maintenance des composants", ToFrenchBoolean(cbs)),
                new AdaptiveTaskEvidence("Windows Update", ToFrenchBoolean(windowsUpdate)),
                new AdaptiveTaskEvidence("Renommage de fichiers", ToFrenchBoolean(pendingFileRename))
            },
            null,
            DateTimeOffset.Now);
        return true;
    }

    public static string BuildDisplayMessage(AdaptiveTaskStructuredResult result)
    {
        return result.Summary;
    }

    private static bool TryReadBoolean(
        IReadOnlyDictionary<string, string> values,
        string key,
        out bool value)
    {
        value = false;
        return values.TryGetValue(key, out string? text) &&
            bool.TryParse(text, out value);
    }

    private static string ToFrenchBoolean(bool value)
    {
        return value ? "Oui" : "Non";
    }
}

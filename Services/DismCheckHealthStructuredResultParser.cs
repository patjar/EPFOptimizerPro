using EPFOptimizerPro.Services.Models;

namespace EPFOptimizerPro.Services;

public static class DismCheckHealthStructuredResultParser
{
    public const string TaskName = "DISM CheckHealth";
    private const string Marker = "EPF_STRUCTURED_DISM_CHECKHEALTH";

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

        if (!values.TryGetValue("ImageHealthState", out string? healthState) ||
            !TryReadBoolean(values, "Online", out bool online) ||
            !TryReadBoolean(values, "RestartNeeded", out bool restartNeeded))
        {
            result = AdaptiveTaskStructuredResult.Unknown(
                TaskName,
                "Le r\u00E9sultat DISM CheckHealth est incomplet.");
            return true;
        }

        AdaptiveTaskVerdict verdict;
        AdaptiveTaskSeverity severity;
        string summary;

        switch (healthState)
        {
            case "Healthy":
                verdict = AdaptiveTaskVerdict.Healthy;
                severity = AdaptiveTaskSeverity.Success;
                summary = "Le magasin de composants Windows est sain.";
                break;

            case "Repairable":
                verdict = AdaptiveTaskVerdict.Attention;
                severity = AdaptiveTaskSeverity.Warning;
                summary = "Le magasin de composants Windows est r\u00E9parable.";
                break;

            case "NonRepairable":
                verdict = AdaptiveTaskVerdict.Unhealthy;
                severity = AdaptiveTaskSeverity.Critical;
                summary = "Le magasin de composants Windows n'est pas r\u00E9parable.";
                break;

            default:
                result = AdaptiveTaskStructuredResult.Unknown(
                    TaskName,
                    "L'\u00E9tat du magasin de composants Windows est inconnu.");
                return true;
        }

        values.TryGetValue("LogPath", out string? logPath);

        var evidence = new List<AdaptiveTaskEvidence>
        {
            new("\u00C9tat de l'image", healthState),
            new("Image en ligne", ToFrenchBoolean(online)),
            new("Red\u00E9marrage requis", ToFrenchBoolean(restartNeeded))
        };

        if (!string.IsNullOrWhiteSpace(logPath))
        {
            evidence.Add(new AdaptiveTaskEvidence("Journal DISM", logPath));
        }

        result = new AdaptiveTaskStructuredResult(
            TaskName,
            verdict,
            severity,
            summary,
            evidence,
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

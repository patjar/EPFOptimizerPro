using System.Globalization;
using EPFOptimizerPro.Services.Models;

namespace EPFOptimizerPro.Services;

public static class SystemDiskStructuredResultParser
{
    public const string TaskName = "Espace disque système";
    private const string Marker = "EPF_STRUCTURED_SYSTEM_DISK";

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

        if (!values.TryGetValue("Drive", out string? drive) ||
            !TryReadLong(values, "TotalBytes", out long totalBytes) ||
            !TryReadLong(values, "FreeBytes", out long freeBytes) ||
            totalBytes <= 0 || freeBytes < 0)
        {
            result = AdaptiveTaskStructuredResult.Unknown(
                TaskName,
                "Mesure de l'espace disque système incomplète.");
            return true;
        }

        double totalGiB = totalBytes / 1024d / 1024d / 1024d;
        double freeGiB = freeBytes / 1024d / 1024d / 1024d;
        double freePercent = freeBytes * 100d / totalBytes;

        AdaptiveTaskVerdict verdict;
        AdaptiveTaskSeverity severity;
        string summary;
        string? recommendedTaskName = null;

        if (freePercent < 8d || freeGiB < 10d)
        {
            verdict = AdaptiveTaskVerdict.Unhealthy;
            severity = AdaptiveTaskSeverity.Critical;
            summary = "Espace disque système critique.";
            recommendedTaskName = "Temp User";
        }
        else if (freePercent < 15d || freeGiB < 20d)
        {
            verdict = AdaptiveTaskVerdict.Attention;
            severity = AdaptiveTaskSeverity.Warning;
            summary = "Espace disque système à surveiller.";
            recommendedTaskName = "Temp User";
        }
        else
        {
            verdict = AdaptiveTaskVerdict.Healthy;
            severity = AdaptiveTaskSeverity.Success;
            summary = "Espace disque système suffisant.";
        }

        result = new AdaptiveTaskStructuredResult(
            TaskName,
            verdict,
            severity,
            summary,
            new[]
            {
                new AdaptiveTaskEvidence("Lecteur", drive),
                new AdaptiveTaskEvidence("Capacité totale", totalBytes.ToString(CultureInfo.InvariantCulture), "octets"),
                new AdaptiveTaskEvidence("Espace disponible", freeBytes.ToString(CultureInfo.InvariantCulture), "octets"),
                new AdaptiveTaskEvidence("Capacité totale", totalGiB.ToString("0.00", CultureInfo.InvariantCulture), "Gio"),
                new AdaptiveTaskEvidence("Espace disponible", freeGiB.ToString("0.00", CultureInfo.InvariantCulture), "Gio"),
                new AdaptiveTaskEvidence("Pourcentage libre", freePercent.ToString("0.00", CultureInfo.InvariantCulture), "%")
            },
            recommendedTaskName,
            DateTimeOffset.Now);
        return true;
    }

    public static string BuildDisplayMessage(AdaptiveTaskStructuredResult result)
    {
        AdaptiveTaskEvidence? drive = result.Evidence.FirstOrDefault(item => item.Label == "Lecteur");
        AdaptiveTaskEvidence? free = result.Evidence.FirstOrDefault(item =>
            item.Label == "Espace disponible" && item.Unit == "Gio");
        AdaptiveTaskEvidence? percent = result.Evidence.FirstOrDefault(item => item.Label == "Pourcentage libre");

        return $"{result.Summary} {drive?.Value ?? "?"} : " +
            $"{free?.Value ?? "?"} Gio libres ({percent?.Value ?? "?"} %).";
    }

    private static bool TryReadLong(
        IReadOnlyDictionary<string, string> values,
        string key,
        out long value)
    {
        value = 0;
        return values.TryGetValue(key, out string? text) &&
            long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
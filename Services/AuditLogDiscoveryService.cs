using System.IO;

namespace EPFOptimizerPro.Services;

public sealed record AuditLogFileInfo(
    string Name,
    string FullPath,
    long Length,
    DateTime LastWriteTime)
{
    public string DisplayName => $"{Name}  ({FormatSize(Length)}, {LastWriteTime:dd/MM/yyyy HH:mm:ss})";

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024L) return $"{bytes / (1024d * 1024d):0.##} Mo";
        if (bytes >= 1024L) return $"{bytes / 1024d:0.##} Ko";
        return $"{bytes} octets";
    }
}

public static class AuditLogDiscoveryService
{
    private const int MaximumDisplayedCharacters = 524288;

    public static IReadOnlyList<AuditLogFileInfo> FindLogs()
    {
        string[] roots =
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EPFOptimizerPro"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EPFOptimizerPro"),
            Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath()
        };

        var results = new Dictionary<string, AuditLogFileInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (string root in roots.Where(Directory.Exists))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*.log", SearchOption.AllDirectories).ToList();
            }
            catch
            {
                continue;
            }

            foreach (string file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    if (!IsRelevantLog(info.Name)) continue;

                    results[file] = new AuditLogFileInfo(
                        info.Name,
                        info.FullName,
                        info.Length,
                        info.LastWriteTime);
                }
                catch
                {
                    // Un fichier verrouille ou inaccessible est simplement ignore.
                }
            }
        }

        return results.Values
            .OrderByDescending(item => item.LastWriteTime)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ReadLog(AuditLogFileInfo log)
    {
        if (!File.Exists(log.FullPath))
        {
            return "Le journal n'existe plus. Cliquez sur Actualiser.";
        }

        try
        {
            string content = File.ReadAllText(log.FullPath);
            if (content.Length <= MaximumDisplayedCharacters)
            {
                return string.IsNullOrWhiteSpace(content)
                    ? "Le journal est vide."
                    : content;
            }

            return "[Affichage limite aux 512 derniers Ko du journal]" +
                Environment.NewLine + Environment.NewLine +
                content[^MaximumDisplayedCharacters..];
        }
        catch (Exception ex)
        {
            return "Impossible de lire le journal : " + ex.Message;
        }
    }

    private static bool IsRelevantLog(string fileName)
    {
        return fileName.Contains("EPFOptimizerPro", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("update", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("install", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("crash", StringComparison.OrdinalIgnoreCase);
    }
}
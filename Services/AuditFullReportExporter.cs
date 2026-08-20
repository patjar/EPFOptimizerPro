using System.Diagnostics;
using System.IO;
using System.Text;

namespace EPFOptimizerPro.Services;

public static class AuditFullReportExporter
{
    public static async Task<string> ExportAsync(
        string name,
        string status,
        string progress,
        string message,
        IEnumerable<object> completedTasks,
        CancellationToken token = default)
    {
        string projectRoot = FindProjectRoot();
        IReadOnlyList<AuditProblemSummary> problems =
            AuditProblemsFilterService.GetErrors(completedTasks);

        string summary = AuditManagementSummaryProvider.Build(
            name, status, progress, message, problems);
        string problemReport = AuditProblemsSummaryProvider.Format(problems);
        string developer = AuditDeveloperInfoProvider.Build();
        string deadCode = AuditDeadCodeScannerService.Scan(projectRoot);
        string updateChannel = await SafeAsync(
            () => AuditUpdateChannelDiagnosticService.BuildReportAsync(token),
            "Diagnostic du canal de mise a jour indisponible.");
        string versions = Safe(
            AuditVersionConsistencyService.BuildReport,
            "Controle de coherence des versions indisponible.");
        string msi = await SafeAsync(
            () => AuditMsiSignatureService.BuildReportAsync(token),
            "Verification du MSI indisponible.");
        string git = Safe(
            AuditGitRepositoryHealthService.BuildReport,
            "Diagnostic Git indisponible.");

        string report = BuildMarkdown(
            summary, problemReport, developer, deadCode,
            updateChannel, versions, msi, git);

        string folder = EnsureReportFolder();
        string fileName = $"EPFOptimizerPro-Audit-{DateTime.Now:yyyyMMdd-HHmmss}.md";
        string path = Path.Combine(folder, fileName);
        await File.WriteAllTextAsync(path, report, new UTF8Encoding(false), token);
        return path;
    }

    public static void OpenReport(string path)
    {
        if (!File.Exists(path)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    public static void OpenReportFolder(string path)
    {
        string? folder = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    private static string BuildMarkdown(params string[] sections)
    {
        string[] titles =
        {
            "Resume de l'audit",
            "Problemes detectes",
            "Informations developpeur",
            "Analyse du code mort",
            "Canal de mise a jour",
            "Coherence des versions",
            "MSI et signature",
            "Sante du depot Git"
        };

        var builder = new StringBuilder();
        builder.AppendLine("# Rapport d'audit EPFOptimizerPro");
        builder.AppendLine();
        builder.AppendLine($"- Genere le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        builder.AppendLine($"- Machine : {Environment.MachineName}");
        builder.AppendLine($"- Utilisateur : {Environment.UserName}");
        builder.AppendLine("- Mode : lecture seule");
        builder.AppendLine();
        builder.AppendLine("> Ce rapport ne modifie aucun fichier source, commit, tag, release, MSI ou certificat.");

        for (int index = 0; index < sections.Length && index < titles.Length; index++)
        {
            builder.AppendLine();
            builder.AppendLine($"## {titles[index]}");
            builder.AppendLine();
            builder.AppendLine("```text");
            builder.AppendLine(SanitizeCodeFence(sections[index]));
            builder.AppendLine("```");
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string SanitizeCodeFence(string value)
    {
        return (value ?? string.Empty).Replace("```", "` ` `", StringComparison.Ordinal);
    }

    private static async Task<string> SafeAsync(
        Func<Task<string>> action,
        string fallback)
    {
        try { return await action(); }
        catch (Exception ex) { return fallback + Environment.NewLine + ex.Message; }
    }

    private static string Safe(Func<string> action, string fallback)
    {
        try { return action(); }
        catch (Exception ex) { return fallback + Environment.NewLine + ex.Message; }
    }

    private static string EnsureReportFolder()
    {
        string programData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EPFOptimizerPro", "Reports");
        string localData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EPFOptimizerPro", "Reports");

        try
        {
            Directory.CreateDirectory(programData);
            string probe = Path.Combine(programData, ".write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(probe, "test");
            File.Delete(probe);
            return programData;
        }
        catch
        {
            Directory.CreateDirectory(localData);
            return localData;
        }
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EPFOptimizerPro.csproj")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return Environment.CurrentDirectory;
    }
}
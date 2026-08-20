using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace EPFOptimizerPro.Services;

public static class AuditDeadCodeScannerService
{
    private static readonly string[] IgnoredFolders =
    {
        ".git", "bin", "obj", "publish", "dist", ".vs"
    };

    public static string Scan(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot))
        {
            return "Analyse indisponible : dossier du projet introuvable.";
        }

        var findings = new List<string>();
        ScanBackupAndTemporaryFiles(projectRoot, findings);
        ScanMarkers(projectRoot, findings);
        ScanPotentiallyUnreferencedClasses(projectRoot, findings);
        ScanMissingMainWindowHandlers(projectRoot, findings);

        var builder = new StringBuilder();
        builder.AppendLine("ANALYSE CONSERVATRICE DU CODE MORT");
        builder.AppendLine();
        builder.AppendLine($"Dossier analyse : {projectRoot}");
        builder.AppendLine("Mode            : lecture seule");
        builder.AppendLine("Suppression     : aucune");
        builder.AppendLine();

        if (findings.Count == 0)
        {
            builder.AppendLine("Aucun candidat evident detecte par les controles V2.");
        }
        else
        {
            builder.AppendLine($"Candidats detectes : {findings.Count}");
            builder.AppendLine();
            foreach (string finding in findings)
            {
                builder.AppendLine(finding);
            }
        }

        builder.AppendLine();
        builder.AppendLine("IMPORTANT");
        builder.AppendLine("Ces resultats sont indicatifs. XAML, reflection, serialisation,");
        builder.AppendLine("injection et conventions peuvent produire des faux positifs.");
        return builder.ToString().TrimEnd();
    }

    private static void ScanBackupAndTemporaryFiles(string root, ICollection<string> findings)
    {
        foreach (string file in EnumerateFiles(root, "*.*"))
        {
            string name = Path.GetFileName(file);
            if (name.Contains(".bak", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".old", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".orig", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add($"[FICHIER TEMPORAIRE] {Relative(root, file)}");
            }
        }
    }

    private static void ScanMarkers(string root, ICollection<string> findings)
    {
        string[] extensions = { ".cs", ".xaml", ".ps1", ".csproj", ".md" };
        foreach (string file in EnumerateFiles(root, "*.*")
                     .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                     .Where(file => !IsMarkerScanExcluded(root, file)))
        {
            string[] lines;
            try { lines = File.ReadAllLines(file); }
            catch { continue; }

            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                if (line.Contains("TODO", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("FIXME", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("HACK", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add($"[MARQUEUR] {Relative(root, file)}:{index + 1}  {line.Trim()}");
                }
            }
        }
    }

    private static bool IsMarkerScanExcluded(string root, string file)
    {
        string relative = Relative(root, file).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        return relative.Equals(
                Path.Combine("Services", "AuditDeadCodeScannerService.cs"),
                StringComparison.OrdinalIgnoreCase)
            || relative.Equals(
                Path.Combine("Services", "AuditDeadCodeInfoProvider.cs"),
                StringComparison.OrdinalIgnoreCase);
    }
    private static void ScanPotentiallyUnreferencedClasses(string root, ICollection<string> findings)
    {
        List<string> sourceFiles = EnumerateFiles(root, "*.cs").ToList();
        var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in sourceFiles)
        {
            try { contents[file] = File.ReadAllText(file); }
            catch { }
        }

        var declarationPattern = new Regex(
            @"\b(?:public|internal)\s+(?:static\s+|sealed\s+|abstract\s+|partial\s+)*(?:class|record)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

        foreach ((string file, string content) in contents)
        {
            foreach (Match match in declarationPattern.Matches(content))
            {
                string className = match.Groups["name"].Value;
                if (className is "App" or "MainWindow") continue;

                var typePattern = new Regex(
                    $@"\b{Regex.Escape(className)}\b",
                    RegexOptions.Compiled);

                int totalOccurrences = contents.Values
                    .Sum(source => typePattern.Matches(source).Count);

                int declarationOccurrences = contents.Values
                    .Sum(source => declarationPattern.Matches(source)
                        .Count(declaration => declaration.Groups["name"].Value.Equals(
                            className,
                            StringComparison.Ordinal)));

                int nonDeclarationOccurrences = totalOccurrences - declarationOccurrences;

                if (nonDeclarationOccurrences == 0)
                {
                    findings.Add($"[CLASSE A VERIFIER] {className} dans {Relative(root, file)}");
                }
            }
        }
    }

    private static void ScanMissingMainWindowHandlers(string root, ICollection<string> findings)
    {
        string xamlPath = Path.Combine(root, "MainWindow.xaml");
        string codePath = Path.Combine(root, "MainWindow.xaml.cs");
        if (!File.Exists(xamlPath) || !File.Exists(codePath)) return;

        string xaml;
        string code;
        try
        {
            xaml = File.ReadAllText(xamlPath);
            code = File.ReadAllText(codePath);
        }
        catch
        {
            return;
        }

        var eventPattern = new Regex(
            @"\b(?:Click|Loaded|Unloaded|MouseLeftButtonUp|MouseDoubleClick|SelectionChanged|Checked|Unchecked|TextChanged)\s*=\s*""(?<handler>[A-Za-z_][A-Za-z0-9_]*)""",
            RegexOptions.Compiled);

        foreach (string handler in eventPattern.Matches(xaml)
                     .Select(match => match.Groups["handler"].Value)
                     .Distinct(StringComparer.Ordinal))
        {
            if (!Regex.IsMatch(code, $@"\b{Regex.Escape(handler)}\s*\("))
            {
                findings.Add($"[HANDLER XAML INTROUVABLE] {handler} dans MainWindow.xaml");
            }
        }
    }

    private static IEnumerable<string> EnumerateFiles(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                .Where(path => !IsIgnored(root, path))
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsIgnored(string root, string path)
    {
        string relative = Relative(root, path);
        string[] parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => IgnoredFolders.Contains(part, StringComparer.OrdinalIgnoreCase));
    }

    private static string Relative(string root, string path)
    {
        return Path.GetRelativePath(root, path);
    }
}
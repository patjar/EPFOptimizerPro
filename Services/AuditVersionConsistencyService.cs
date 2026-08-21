using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace EPFOptimizerPro.Services;

public static class AuditVersionConsistencyService
{
    public static string BuildReport()
    {
        string projectRoot = FindProjectRoot();
        string projectFile = Path.Combine(projectRoot, "EPFOptimizerPro.csproj");
        string projectText = File.Exists(projectFile) ? File.ReadAllText(projectFile) : string.Empty;

        string projectVersion = ReadXmlValue(projectText, "Version");
        string projectAssemblyVersion = ReadXmlValue(projectText, "AssemblyVersion");
        string projectFileVersion = ReadXmlValue(projectText, "FileVersion");
        string projectInformationalVersion = ReadXmlValue(projectText, "InformationalVersion");

        Assembly assembly = Assembly.GetExecutingAssembly();
        string runtimeAssemblyVersion = assembly.GetName().Version?.ToString() ?? "Inconnue";
        string runtimeInformationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "Inconnue";

        string executablePath = Environment.ProcessPath ?? assembly.Location;
        string executableFileVersion = File.Exists(executablePath)
            ? FileVersionInfo.GetVersionInfo(executablePath).FileVersion ?? "Inconnue"
            : "Inconnue";

        MsiInfo? latestMsi = FindLatestMsi(projectRoot);
        string gitBranch = RunGit(projectRoot, "branch --show-current");
        string gitCommit = RunGit(projectRoot, "rev-parse --short HEAD");
        string gitTags = RunGit(projectRoot, "tag --points-at HEAD");

        if (string.IsNullOrWhiteSpace(gitBranch) || string.IsNullOrWhiteSpace(gitCommit))
        {
            GitRepositoryInfo fallback = ReadGitRepositoryInfo(projectRoot);
            if (string.IsNullOrWhiteSpace(gitBranch)) gitBranch = fallback.Branch;
            if (string.IsNullOrWhiteSpace(gitCommit)) gitCommit = ShortHash(fallback.Commit);
            if (string.IsNullOrWhiteSpace(gitTags)) gitTags = string.Join(Environment.NewLine, fallback.Tags);
        }

        var checks = new List<(string Label, bool Ok, string Details)>();
        string normalizedProject = Normalize(projectVersion);

        checks.Add((
            "AssemblyVersion du projet",
            SameVersion(normalizedProject, projectAssemblyVersion),
            $"Version={Value(projectVersion)}, AssemblyVersion={Value(projectAssemblyVersion)}"));
        checks.Add((
            "FileVersion du projet",
            SameVersion(normalizedProject, projectFileVersion),
            $"Version={Value(projectVersion)}, FileVersion={Value(projectFileVersion)}"));
        checks.Add((
            "InformationalVersion du projet",
            SameVersion(normalizedProject, projectInformationalVersion),
            $"Version={Value(projectVersion)}, InformationalVersion={Value(projectInformationalVersion)}"));
        checks.Add((
            "Assembly en cours",
            SameVersion(normalizedProject, runtimeAssemblyVersion),
            $"Projet={Value(projectVersion)}, Assembly={runtimeAssemblyVersion}"));
        checks.Add((
            "Exécutable en cours",
            SameVersion(normalizedProject, executableFileVersion),
            $"Projet={Value(projectVersion)}, Exécutable={executableFileVersion}"));

        if (latestMsi is not null)
        {
            checks.Add((
                "Dernier MSI dans dist",
                SameVersion(normalizedProject, latestMsi.Version),
                $"Projet={Value(projectVersion)}, MSI={latestMsi.Name}"));
        }

        if (!string.IsNullOrWhiteSpace(gitTags))
        {
            bool matchingTag = gitTags
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(tag => SameVersion(normalizedProject, ExtractVersion(tag)));
            checks.Add((
                "Tag Git sur HEAD",
                matchingTag,
                gitTags.Replace(Environment.NewLine, ", ").Trim()));
        }

        int errors = checks.Count(check => !check.Ok);
        var builder = new StringBuilder();
        builder.AppendLine("COHÉRENCE DES VERSIONS");
        builder.AppendLine();
        builder.AppendLine($"Dossier projet          : {projectRoot}");
        builder.AppendLine($"Branche Git             : {Value(gitBranch)}");
        builder.AppendLine($"Commit HEAD             : {Value(gitCommit)}");
        builder.AppendLine($"Tag(s) sur HEAD         : {Value(gitTags.Replace(Environment.NewLine, ", ").Trim())}");
        builder.AppendLine();
        builder.AppendLine("VERSIONS DÉTECTÉES");
        builder.AppendLine();
        builder.AppendLine($"Projet Version          : {Value(projectVersion)}");
        builder.AppendLine($"Projet AssemblyVersion  : {Value(projectAssemblyVersion)}");
        builder.AppendLine($"Projet FileVersion      : {Value(projectFileVersion)}");
        builder.AppendLine($"Projet Informational    : {Value(projectInformationalVersion)}");
        builder.AppendLine($"Assembly exécutée       : {runtimeAssemblyVersion}");
        builder.AppendLine($"Information exécutée    : {runtimeInformationalVersion}");
        builder.AppendLine($"Fichier exécutable      : {executableFileVersion}");
        builder.AppendLine($"Dernier MSI             : {(latestMsi is null ? "Aucun MSI dans dist" : latestMsi.Name)}");
        builder.AppendLine();
        builder.AppendLine("CONTRÔLES");
        builder.AppendLine();

        foreach ((string label, bool ok, string details) in checks)
        {
            builder.AppendLine($"[{(ok ? "OK" : "ERREUR")}] {label}");
            builder.AppendLine($"  {details}");
        }

        builder.AppendLine();
        builder.AppendLine($"VERDICT : {(errors == 0 ? "COHÉRENT" : $"INCOHÉRENT ({errors} contrôle(s) en erreur)")}");
        builder.AppendLine("Lecture seule : aucun fichier, tag ou commit n'a été modifié.");
        return builder.ToString().TrimEnd();
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

    private static string ReadXmlValue(string text, string element)
    {
        Match match = Regex.Match(
            text,
            $@"<{Regex.Escape(element)}>\s*(?<value>[^<]+)\s*</{Regex.Escape(element)}>",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
    }

    private static MsiInfo? FindLatestMsi(string projectRoot)
    {
        string dist = Path.Combine(projectRoot, "dist");
        if (!Directory.Exists(dist)) return null;

        FileInfo? file = new DirectoryInfo(dist)
            .EnumerateFiles("EPFOptimizerPro-Setup-v*.msi", SearchOption.TopDirectoryOnly)
            .OrderByDescending(item => item.LastWriteTime)
            .FirstOrDefault();
        if (file is null) return null;

        return new MsiInfo(file.Name, ExtractVersion(file.Name));
    }

    private static string ExtractVersion(string value)
    {
        Match match = Regex.Match(value, @"(?<version>\d+\.\d+(?:\.\d+){0,2})");
        return match.Success ? match.Groups["version"].Value : string.Empty;
    }

    private static bool SameVersion(string left, string right)
    {
        string normalizedLeft = Normalize(left);
        string normalizedRight = Normalize(right);
        return !string.IsNullOrWhiteSpace(normalizedLeft)
            && normalizedLeft.Equals(normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        if (!Version.TryParse(ExtractVersion(value), out Version? version)) return string.Empty;
        int build = version.Build >= 0 ? version.Build : 0;
        return $"{version.Major}.{version.Minor}.{build}";
    }

    private static string RunGit(string workingDirectory, string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = ResolveGitExecutable(),
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(3000);
            return process.ExitCode == 0 ? output : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static GitRepositoryInfo ReadGitRepositoryInfo(string projectRoot)
    {
        string gitPath = Path.Combine(projectRoot, ".git");
        string gitDirectory = ResolveGitDirectory(gitPath, projectRoot);
        if (string.IsNullOrWhiteSpace(gitDirectory) || !Directory.Exists(gitDirectory))
        {
            return new GitRepositoryInfo(string.Empty, string.Empty, Array.Empty<string>());
        }

        string headPath = Path.Combine(gitDirectory, "HEAD");
        if (!File.Exists(headPath))
        {
            return new GitRepositoryInfo(string.Empty, string.Empty, Array.Empty<string>());
        }

        string head = File.ReadAllText(headPath).Trim();
        string branch = string.Empty;
        string commit = string.Empty;

        if (head.StartsWith("ref:", StringComparison.OrdinalIgnoreCase))
        {
            string reference = head[4..].Trim().Replace('/', Path.DirectorySeparatorChar);
            const string headsPrefix = "refs/heads/";
            string normalizedHead = head[4..].Trim();
            if (normalizedHead.StartsWith(headsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                branch = normalizedHead[headsPrefix.Length..];
            }

            string looseRefPath = Path.Combine(gitDirectory, reference);
            if (File.Exists(looseRefPath))
            {
                commit = File.ReadAllText(looseRefPath).Trim();
            }
            else
            {
                commit = ReadPackedReference(gitDirectory, normalizedHead);
            }
        }
        else
        {
            commit = head;
        }

        IReadOnlyList<string> tags = FindTagsForCommit(gitDirectory, commit);
        return new GitRepositoryInfo(branch, commit, tags);
    }

    private static string ResolveGitDirectory(string gitPath, string projectRoot)
    {
        if (Directory.Exists(gitPath)) return gitPath;
        if (!File.Exists(gitPath)) return string.Empty;

        string text = File.ReadAllText(gitPath).Trim();
        const string prefix = "gitdir:";
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return string.Empty;

        string value = text[prefix.Length..].Trim();
        return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(projectRoot, value));
    }

    private static string ReadPackedReference(string gitDirectory, string reference)
    {
        string packedRefs = Path.Combine(gitDirectory, "packed-refs");
        if (!File.Exists(packedRefs)) return string.Empty;

        foreach (string line in File.ReadLines(packedRefs))
        {
            if (string.IsNullOrWhiteSpace(line) || line[0] is '#' or '^') continue;
            string[] parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1].Equals(reference, StringComparison.Ordinal))
            {
                return parts[0];
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> FindTagsForCommit(string gitDirectory, string commit)
    {
        if (string.IsNullOrWhiteSpace(commit)) return Array.Empty<string>();
        var tags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        string tagsDirectory = Path.Combine(gitDirectory, "refs", "tags");

        if (Directory.Exists(tagsDirectory))
        {
            foreach (string file in Directory.EnumerateFiles(tagsDirectory, "*", SearchOption.AllDirectories))
            {
                string target = File.ReadAllText(file).Trim();
                if (!target.Equals(commit, StringComparison.OrdinalIgnoreCase)) continue;
                tags.Add(Path.GetRelativePath(tagsDirectory, file).Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        string packedRefs = Path.Combine(gitDirectory, "packed-refs");
        if (File.Exists(packedRefs))
        {
            foreach (string line in File.ReadLines(packedRefs))
            {
                if (string.IsNullOrWhiteSpace(line) || line[0] is '#' or '^') continue;
                string[] parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || !parts[0].Equals(commit, StringComparison.OrdinalIgnoreCase)) continue;
                const string tagPrefix = "refs/tags/";
                if (parts[1].StartsWith(tagPrefix, StringComparison.Ordinal))
                {
                    tags.Add(parts[1][tagPrefix.Length..]);
                }
            }
        }

        return tags.ToList();
    }

    private static string ShortHash(string commit)
    {
        return string.IsNullOrWhiteSpace(commit)
            ? string.Empty
            : commit[..Math.Min(7, commit.Length)];
    }

    private sealed record GitRepositoryInfo(
        string Branch,
        string Commit,
        IReadOnlyList<string> Tags);
    private static string ResolveGitExecutable()
    {
        string[] candidates =
        {
            "git.exe",
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Git", "cmd", "git.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Git", "cmd", "git.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Git", "cmd", "git.exe")
        };

        foreach (string candidate in candidates.Skip(1))
        {
            if (File.Exists(candidate)) return candidate;
        }

        try
        {
            using var where = new Process();
            where.StartInfo = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = "git.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            where.Start();
            string located = where.StandardOutput.ReadLine() ?? string.Empty;
            where.WaitForExit(2000);
            if (where.ExitCode == 0 && File.Exists(located)) return located;
        }
        catch
        {
            // Le fallback PATH ci-dessous reste disponible.
        }

        return candidates[0];
    }
    private static string Value(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Non disponible" : value;
    }

    private sealed record MsiInfo(string Name, string Version);
}

using System.Diagnostics;
using System.IO;
using System.Text;

namespace EPFOptimizerPro.Services;

public static class AuditGitRepositoryHealthService
{
    public static string BuildReport()
    {
        string projectRoot = FindProjectRoot();
        GitCommandResult status = RunGit(projectRoot, "status --porcelain=v1 --branch");
        GitCommandResult branchResult = RunGit(projectRoot, "branch --show-current");
        GitCommandResult commitResult = RunGit(projectRoot, "rev-parse --short HEAD");
        GitCommandResult upstreamResult = RunGit(projectRoot, "rev-parse --abbrev-ref --symbolic-full-name @{upstream}");
        GitCommandResult tagsResult = RunGit(projectRoot, "tag --points-at HEAD");
        GitCommandResult countsResult = RunGit(projectRoot, "rev-list --left-right --count HEAD...@{upstream}");

        string branch = FirstNonEmpty(branchResult.Output, ReadBranchFromGitMetadata(projectRoot));
        string commit = FirstNonEmpty(commitResult.Output, ReadCommitFromGitMetadata(projectRoot));
        string upstream = upstreamResult.Success ? upstreamResult.Output.Trim() : string.Empty;
        (int ahead, int behind) = ParseAheadBehind(countsResult.Output);

        List<GitChange> changes = ParseChanges(status.Output);
        List<GitChange> staged = changes.Where(change => change.IndexStatus != ' ' && change.IndexStatus != '?').ToList();
        List<GitChange> modified = changes.Where(change => change.WorkTreeStatus != ' ' && change.WorkTreeStatus != '?').ToList();
        List<GitChange> untracked = changes.Where(change => change.IndexStatus == '?' && change.WorkTreeStatus == '?').ToList();
        List<string> backupFiles = FindBackupFiles(projectRoot);
        string tags = string.IsNullOrWhiteSpace(tagsResult.Output)
            ? "Aucun"
            : tagsResult.Output.Replace(Environment.NewLine, ", ").Trim();

        bool repositoryMetadataAvailable = Directory.Exists(Path.Combine(projectRoot, ".git"));
        bool gitAvailable = status.Success || repositoryMetadataAvailable;
        bool workingTreeKnown = status.Success;
        bool clean = workingTreeKnown && changes.Count == 0 && backupFiles.Count == 0;
        bool synchronized = clean && ahead == 0 && behind == 0 && !string.IsNullOrWhiteSpace(upstream);

        var builder = new StringBuilder();
        builder.AppendLine("SANTE DU DEPOT GIT");
        builder.AppendLine();
        builder.AppendLine($"Dossier projet          : {projectRoot}");
        builder.AppendLine($"Branche locale          : {Value(branch)}");
        builder.AppendLine($"Commit HEAD             : {Value(commit)}");
        builder.AppendLine($"Branche distante        : {Value(upstream)}");
        builder.AppendLine($"Avance locale           : {ahead} commit(s)");
        builder.AppendLine($"Retard local            : {behind} commit(s)");
        builder.AppendLine($"Tag(s) sur HEAD         : {tags}");
        builder.AppendLine();
        builder.AppendLine("ETAT DU WORKING TREE");
        builder.AppendLine();
        builder.AppendLine($"Fichiers indexes        : {staged.Count}");
        builder.AppendLine($"Fichiers modifies       : {modified.Count}");
        builder.AppendLine($"Fichiers non suivis     : {untracked.Count}");
        builder.AppendLine($"Sauvegardes temporaires : {backupFiles.Count}");

        AppendSection(builder, "FICHIERS INDEXES", staged.Select(FormatChange));
        AppendSection(builder, "FICHIERS MODIFIES", modified.Select(FormatChange));
        AppendSection(builder, "FICHIERS NON SUIVIS", untracked.Select(change => change.Path));
        AppendSection(builder, "SAUVEGARDES TEMPORAIRES", backupFiles);

        builder.AppendLine();
        builder.AppendLine("CONTROLES");
        builder.AppendLine();
        AppendCheck(builder, "Depot Git detecte", gitAvailable, status.Error);
        AppendCheck(builder, "Aucun fichier .bak/.tmp/.old/.orig", backupFiles.Count == 0, $"{backupFiles.Count} fichier(s)");
        AppendCheck(
            builder,
            "Working tree propre",
            workingTreeKnown && changes.Count == 0,
            workingTreeKnown ? $"{changes.Count} changement(s)" : "Etat inconnu : " + Value(status.Error));
        AppendCheck(builder, "Aucun retard sur le distant", behind == 0, $"{behind} commit(s)");
        AppendCheck(builder, "Aucun commit local a pousser", ahead == 0, $"{ahead} commit(s)");
        builder.AppendLine();
        builder.AppendLine($"VERDICT : {GetVerdict(gitAvailable, clean, ahead, behind, upstream)}");
        builder.AppendLine("Lecture seule : aucun fichier, commit, tag ou distant n'a ete modifie.");
        return builder.ToString().TrimEnd();
    }

    private static string GetVerdict(bool gitAvailable, bool clean, int ahead, int behind, string upstream)
    {
        if (!gitAvailable) return "DEPOT GIT NON DISPONIBLE";
        if (!clean) return "CHANGEMENTS A CONTROLER AVANT COMMIT";
        if (behind > 0) return "MISE A JOUR DU DEPOT DISTANT REQUISE";
        if (ahead > 0) return "PRET POUR PUSH";
        if (string.IsNullOrWhiteSpace(upstream)) return "DEPOT PROPRE, AUCUN DISTANT SUIVI";
        return "DEPOT PROPRE ET SYNCHRONISE";
    }

    private static void AppendSection(StringBuilder builder, string title, IEnumerable<string> items)
    {
        List<string> list = items.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().ToList();
        if (list.Count == 0) return;
        builder.AppendLine();
        builder.AppendLine(title);
        builder.AppendLine();
        foreach (string item in list.Take(50)) builder.AppendLine("- " + item);
        if (list.Count > 50) builder.AppendLine($"... {list.Count - 50} autre(s) element(s).");
    }

    private static void AppendCheck(StringBuilder builder, string label, bool ok, string details)
    {
        builder.AppendLine($"[{(ok ? "OK" : "ATTENTION")}] {label}");
        if (!string.IsNullOrWhiteSpace(details)) builder.AppendLine("  " + details.Trim());
    }

    private static string FormatChange(GitChange change)
    {
        return $"[{change.IndexStatus}{change.WorkTreeStatus}] {change.Path}";
    }

    private static List<GitChange> ParseChanges(string output)
    {
        var changes = new List<GitChange>();
        foreach (string line in SplitLines(output))
        {
            if (line.StartsWith("##", StringComparison.Ordinal) || line.Length < 3) continue;
            changes.Add(new GitChange(line[0], line[1], line[3..].Trim()));
        }
        return changes;
    }

    private static (int Ahead, int Behind) ParseAheadBehind(string output)
    {
        string[] parts = output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && int.TryParse(parts[0], out int ahead) && int.TryParse(parts[1], out int behind)
            ? (ahead, behind)
            : (0, 0);
    }

    private static List<string> FindBackupFiles(string root)
    {
        string[] ignored = { ".git", "bin", "obj", "publish", "dist", ".vs" };
        try
        {
            return Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(path => !Path.GetRelativePath(root, path)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(part => ignored.Contains(part, StringComparer.OrdinalIgnoreCase)))
                .Where(path =>
                {
                    string name = Path.GetFileName(path);
                    return name.Contains(".bak", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".old", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".orig", StringComparison.OrdinalIgnoreCase);
                })
                .Select(path => Path.GetRelativePath(root, path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static GitCommandResult RunGit(string projectRoot, string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = ResolveGitExecutable(),
                Arguments = BuildGitArguments(projectRoot, arguments),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();
            bool exited = process.WaitForExit(4000);
            return new GitCommandResult(exited && process.ExitCode == 0, output, error);
        }
        catch (Exception ex)
        {
            return new GitCommandResult(false, string.Empty, ex.Message);
        }
    }

    private static string BuildGitArguments(string projectRoot, string arguments)
    {
        string safeDirectory = projectRoot.Replace('\\', '/');
        string escapedSafeDirectory = safeDirectory.Replace("\"", "\\\"");
        string escapedProjectRoot = projectRoot.Replace("\"", "\\\"");
        return $"-c safe.directory=\"{escapedSafeDirectory}\" -C \"{escapedProjectRoot}\" {arguments}";
    }
    private static string ResolveGitExecutable()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "cmd", "git.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "cmd", "git.exe")
        };
        return candidates.FirstOrDefault(File.Exists) ?? "git.exe";
    }

    private static string ReadBranchFromGitMetadata(string projectRoot)
    {
        string head = ReadHead(projectRoot);
        const string prefix = "ref: refs/heads/";
        return head.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? head[prefix.Length..].Trim() : string.Empty;
    }

    private static string ReadCommitFromGitMetadata(string projectRoot)
    {
        string git = Path.Combine(projectRoot, ".git");
        string head = ReadHead(projectRoot);
        if (string.IsNullOrWhiteSpace(head)) return string.Empty;
        if (!head.StartsWith("ref:", StringComparison.OrdinalIgnoreCase)) return ShortHash(head);

        string reference = head[4..].Trim();
        string refPath = Path.Combine(git, reference.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(refPath)) return ShortHash(File.ReadAllText(refPath).Trim());

        string packedRefs = Path.Combine(git, "packed-refs");
        if (File.Exists(packedRefs))
        {
            foreach (string line in File.ReadLines(packedRefs))
            {
                if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("^", StringComparison.Ordinal)) continue;
                string[] parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && parts[1].Equals(reference, StringComparison.Ordinal)) return ShortHash(parts[0]);
            }
        }
        return string.Empty;
    }

    private static string ReadHead(string projectRoot)
    {
        string path = Path.Combine(projectRoot, ".git", "HEAD");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
    }

    private static string ShortHash(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value[..Math.Min(7, value.Length)];
    private static string FirstNonEmpty(string first, string second) => string.IsNullOrWhiteSpace(first) ? second : first.Trim();
    private static string Value(string value) => string.IsNullOrWhiteSpace(value) ? "Non disponible" : value.Trim();
    private static IEnumerable<string> SplitLines(string value) => value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EPFOptimizerPro.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }
        return Environment.CurrentDirectory;
    }

    private sealed record GitChange(char IndexStatus, char WorkTreeStatus, string Path);
    private sealed record GitCommandResult(bool Success, string Output, string Error);
}
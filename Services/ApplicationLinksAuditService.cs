using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace EPFOptimizerPro.Services;

public enum ApplicationLinkKind
{
    UserWebPage,
    TechnicalEndpoint,
    WindowsProtocol,
    Executable,
    FileOrFolder,
    Template
}

public enum ApplicationLinkStatus
{
    Ignored,
    Ok,
    Warning,
    Error
}

public enum ApplicationLinkAction
{
    None,
    OpenBrowser,
    RetestHttp,
    LaunchProtocol,
    LaunchExecutable,
    OpenPath
}

public sealed record ApplicationLinkTarget(
    string Name,
    ApplicationLinkKind Kind,
    string Target,
    string Source,
    ApplicationLinkAction Action);

public sealed record ApplicationLinkCheckResult(
    ApplicationLinkTarget Target,
    ApplicationLinkStatus Status,
    string Details,
    DateTime CheckedAt)
{
    public ApplicationLinkCheckResult(
        ApplicationLinkTarget target,
        ApplicationLinkStatus status,
        string details)
        : this(target, status, details, DateTime.Now)
    {
    }

    public string DisplayName => $"[{StatusLabel}] {Target.Name}";

    public string StatusLabel => Status switch
    {
        ApplicationLinkStatus.Ok => "OK",
        ApplicationLinkStatus.Warning => "ATTENTION",
        ApplicationLinkStatus.Error => "ERREUR",
        _ => "IGNORE"
    };

    public string ActionLabel => Target.Action switch
    {
        ApplicationLinkAction.OpenBrowser => "Ouvrir dans le navigateur",
        ApplicationLinkAction.RetestHttp => "Retester la requête HTTP",
        ApplicationLinkAction.LaunchProtocol => "Tester le protocole Windows",
        ApplicationLinkAction.LaunchExecutable => "Lancer l'exécutable",
        ApplicationLinkAction.OpenPath => "Ouvrir la cible locale",
        _ => "Action indisponible"
    };
}

public static class ApplicationLinksAuditService
{
    private static readonly HttpClient Client = CreateClient();

    public static async Task<IReadOnlyList<ApplicationLinkCheckResult>> CheckAllAsync(
        CancellationToken token = default)
    {
        string projectRoot = FindProjectRoot();
        IReadOnlyList<ApplicationLinkTarget> targets = BuildCatalog(projectRoot);
        var results = new List<ApplicationLinkCheckResult>();

        foreach (ApplicationLinkTarget target in targets)
        {
            results.Add(await CheckAsync(target, token));
        }

        return results
            .OrderByDescending(result => result.Status)
            .ThenBy(result => result.Target.Kind)
            .ThenBy(result => result.Target.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static Task<ApplicationLinkCheckResult> RetestAsync(
        ApplicationLinkTarget target,
        CancellationToken token = default)
    {
        return CheckAsync(target, token);
    }

    public static void ExecuteAction(ApplicationLinkTarget target)
    {
        if (target.Action is ApplicationLinkAction.None or ApplicationLinkAction.RetestHttp)
        {
            throw new InvalidOperationException("Cette cible ne doit pas être ouverte extérieurement.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = ResolveLaunchTarget(target),
            UseShellExecute = true
        });
    }

    public static string FormatReport(IReadOnlyList<ApplicationLinkCheckResult> results)
    {
        int ok = results.Count(result => result.Status == ApplicationLinkStatus.Ok);
        int warnings = results.Count(result => result.Status == ApplicationLinkStatus.Warning);
        int errors = results.Count(result => result.Status == ApplicationLinkStatus.Error);
        int ignored = results.Count(result => result.Status == ApplicationLinkStatus.Ignored);

        var builder = new StringBuilder();
        builder.AppendLine("VÉRIFICATION DES LIENS ET LANCEURS");
        builder.AppendLine();
        builder.AppendLine($"Cibles analysées : {results.Count}");
        builder.AppendLine($"Fonctionnelles   : {ok}");
        builder.AppendLine($"À vérifier       : {warnings}");
        builder.AppendLine($"En erreur        : {errors}");
        builder.AppendLine($"Ignorées         : {ignored}");
        builder.AppendLine();

        foreach (ApplicationLinkCheckResult result in results)
        {
            builder.AppendLine($"[{result.StatusLabel}] {result.Target.Name}");
            builder.AppendLine($"  Type   : {result.Target.Kind}");
            builder.AppendLine($"  Cible  : {result.Target.Target}");
            builder.AppendLine($"  Source : {result.Target.Source}");
            builder.AppendLine($"  Action : {result.ActionLabel}");
            builder.AppendLine($"  Détail : {result.Details}");
            builder.AppendLine($"  Vérifié le : {result.CheckedAt:dd/MM/yyyy HH:mm:ss}");
            builder.AppendLine();
        }

        builder.AppendLine("Vérification seule : aucune cible n'a été ouverte automatiquement.");
        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<ApplicationLinkTarget> BuildCatalog(string root)
    {
        var targets = new Dictionary<string, ApplicationLinkTarget>(StringComparer.OrdinalIgnoreCase);

        Add(targets, new("Microsoft Store - mises à jour", ApplicationLinkKind.WindowsProtocol,
            "ms-windows-store://downloadsandupdates", "UpdateActionScriptService.cs", ApplicationLinkAction.LaunchProtocol));
        Add(targets, new("Microsoft Store - accueil", ApplicationLinkKind.WindowsProtocol,
            "ms-windows-store:", "UpdateActionScriptService.cs", ApplicationLinkAction.LaunchProtocol));
        Add(targets, new("GitHub Releases", ApplicationLinkKind.UserWebPage,
            "https://github.com/patjar/EPFOptimizerPro/releases/latest", "MainWindow.xaml.cs", ApplicationLinkAction.OpenBrowser));
        Add(targets, new("API GitHub Releases", ApplicationLinkKind.TechnicalEndpoint,
            "https://api.github.com/repos/patjar/EPFOptimizerPro/releases", "GitHubUpdateService.cs", ApplicationLinkAction.RetestHttp));
        Add(targets, new("Service d'horodatage DigiCert", ApplicationLinkKind.TechnicalEndpoint,
            "http://timestamp.digicert.com", "Scripts de signature", ApplicationLinkAction.RetestHttp));

        foreach (string executable in new[] { "powershell.exe", "explorer.exe", "cmd.exe", "git.exe", "wt.exe" })
        {
            Add(targets, new(executable, ApplicationLinkKind.Executable, executable,
                "Catalogue développeur", ApplicationLinkAction.LaunchExecutable));
        }

        string reports = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EPFOptimizerPro", "Reports");
        string logs = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EPFOptimizerPro");
        Add(targets, new("Dossier des rapports", ApplicationLinkKind.FileOrFolder, reports,
            "AuditFullReportExporter.cs", ApplicationLinkAction.OpenPath));
        Add(targets, new("Dossier des journaux", ApplicationLinkKind.FileOrFolder, logs,
            "AuditDeveloperInfoProvider.cs", ApplicationLinkAction.OpenPath));

        DiscoverSourceTargets(root, targets);
        return targets.Values.ToList();
    }

    private static void DiscoverSourceTargets(
        string root,
        IDictionary<string, ApplicationLinkTarget> targets)
    {
        string[] ignoredFolders = { ".git", "bin", "obj", "publish", "dist", ".vs" };
        Regex webPattern = new(@"https?://[^\s'""<>]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Regex protocolPattern = new(@"\b(?:ms-windows-store|ms-settings):[^\s'""<>]*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path) is ".cs" or ".ps1")
                .Where(path => !Path.GetRelativePath(root, path)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(part => ignoredFolders.Contains(part, StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }
        catch
        {
            return;
        }

        foreach (string file in files)
        {
            string text;
            try { text = File.ReadAllText(file); }
            catch { continue; }
            string source = Path.GetRelativePath(root, file);

            foreach (Match match in webPattern.Matches(text))
            {
                string value = TrimTarget(match.Value);
                if (value.Contains("schemas.microsoft.com", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("wixtoolset.org/schemas", StringComparison.OrdinalIgnoreCase)) continue;

                if (ContainsTemplateVariables(value))
                {
                    Add(targets, new(value, ApplicationLinkKind.Template, value, source, ApplicationLinkAction.None));
                    continue;
                }

                bool technical = IsTechnicalEndpoint(value);
                Add(targets, new(value,
                    technical ? ApplicationLinkKind.TechnicalEndpoint : ApplicationLinkKind.UserWebPage,
                    value,
                    source,
                    technical ? ApplicationLinkAction.RetestHttp : ApplicationLinkAction.OpenBrowser));
            }

            foreach (Match match in protocolPattern.Matches(text))
            {
                string value = TrimTarget(match.Value);
                Add(targets, new(value, ApplicationLinkKind.WindowsProtocol, value,
                    source, ApplicationLinkAction.LaunchProtocol));
            }
        }
    }

    private static Task<ApplicationLinkCheckResult> CheckAsync(
        ApplicationLinkTarget target,
        CancellationToken token)
    {
        return target.Kind switch
        {
            ApplicationLinkKind.UserWebPage => CheckUserWebPageAsync(target, token),
            ApplicationLinkKind.TechnicalEndpoint => CheckTechnicalEndpointAsync(target, token),
            ApplicationLinkKind.WindowsProtocol => Task.FromResult(CheckProtocol(target)),
            ApplicationLinkKind.Executable => Task.FromResult(CheckExecutable(target)),
            ApplicationLinkKind.FileOrFolder => Task.FromResult(CheckPath(target)),
            ApplicationLinkKind.Template => Task.FromResult(new ApplicationLinkCheckResult(
                target, ApplicationLinkStatus.Ignored,
                "URL modèle contenant des variables. La cible concrète est contrôlée séparément.",
                DateTime.Now)),
            _ => Task.FromResult(new ApplicationLinkCheckResult(
                target, ApplicationLinkStatus.Warning, "Type de cible non pris en charge.", DateTime.Now))
        };
    }

    private static async Task<ApplicationLinkCheckResult> CheckUserWebPageAsync(
        ApplicationLinkTarget target,
        CancellationToken token)
    {
        if (!TryGetHttpUri(target.Target, out Uri? uri) || uri is null)
        {
            return new(target, ApplicationLinkStatus.Error, "URL invalide.", DateTime.Now);
        }

        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, uri);
            using HttpResponseMessage headResponse = await Client.SendAsync(head, token);
            if ((int)headResponse.StatusCode < 400)
            {
                return new(target, ApplicationLinkStatus.Ok, $"Page accessible : HTTP {(int)headResponse.StatusCode} {headResponse.ReasonPhrase}.", DateTime.Now);
            }

            using var get = new HttpRequestMessage(HttpMethod.Get, uri);
            using HttpResponseMessage getResponse = await Client.SendAsync(
                get, HttpCompletionOption.ResponseHeadersRead, token);
            return CreateStrictHttpResult(target, getResponse, "Page utilisateur");
        }
        catch (Exception ex)
        {
            return new(target, ApplicationLinkStatus.Warning, "Réseau non vérifiable : " + ex.Message, DateTime.Now);
        }
    }

    private static async Task<ApplicationLinkCheckResult> CheckTechnicalEndpointAsync(
        ApplicationLinkTarget target,
        CancellationToken token)
    {
        if (!TryGetHttpUri(target.Target, out Uri? uri) || uri is null)
        {
            return new(target, ApplicationLinkStatus.Error, "Endpoint invalide.", DateTime.Now);
        }

        try
        {
            using var get = new HttpRequestMessage(HttpMethod.Get, uri);
            if (uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
            {
                get.Headers.Accept.ParseAdd("application/vnd.github+json");
                get.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
            }

            using HttpResponseMessage response = await Client.SendAsync(
                get, HttpCompletionOption.ResponseHeadersRead, token);

            int code = (int)response.StatusCode;
            bool serverResponded = code >= 100 && code <= 599;
            ApplicationLinkStatus status = serverResponded
                ? ApplicationLinkStatus.Ok
                : ApplicationLinkStatus.Warning;
            string meaning = response.IsSuccessStatusCode
                ? "service joignable et réponse réussie"
                : "serveur joignable, ressource non destinée à la navigation";

            return new(target, status,
                $"Requête HTTP interne : HTTP {code} {response.ReasonPhrase}, {meaning}. Navigateur désactivé.");
        }
        catch (Exception ex)
        {
            return new(target, ApplicationLinkStatus.Warning, "Endpoint non vérifiable actuellement : " + ex.Message, DateTime.Now);
        }
    }

    private static ApplicationLinkCheckResult CreateStrictHttpResult(
        ApplicationLinkTarget target,
        HttpResponseMessage response,
        string label)
    {
        int code = (int)response.StatusCode;
        ApplicationLinkStatus status = code < 400
            ? ApplicationLinkStatus.Ok
            : ApplicationLinkStatus.Warning;
        return new(target, status, $"{label} : HTTP {code} {response.ReasonPhrase}.", DateTime.Now);
    }

    private static ApplicationLinkCheckResult CheckProtocol(ApplicationLinkTarget target)
    {
        if (!Uri.TryCreate(target.Target, UriKind.Absolute, out Uri? uri))
        {
            return new(target, ApplicationLinkStatus.Error, "URI invalide.", DateTime.Now);
        }

        try
        {
            using RegistryKey? key = Registry.ClassesRoot.OpenSubKey(uri.Scheme);
            return key is null
                ? new(target, ApplicationLinkStatus.Error, $"Protocole {uri.Scheme} non enregistré.")
                : new(target, ApplicationLinkStatus.Ok, $"Protocole {uri.Scheme} enregistré dans Windows.", DateTime.Now);
        }
        catch (Exception ex)
        {
            return new(target, ApplicationLinkStatus.Warning, "Registre non vérifiable : " + ex.Message, DateTime.Now);
        }
    }

    private static ApplicationLinkCheckResult CheckExecutable(ApplicationLinkTarget target)
    {
        string? resolved = ResolveExecutable(target.Target);
        return resolved is null
            ? new(target, ApplicationLinkStatus.Warning, "Exécutable non trouvé dans les emplacements connus.")
            : new(target, ApplicationLinkStatus.Ok, "Résolu : " + resolved, DateTime.Now);
    }

    private static ApplicationLinkCheckResult CheckPath(ApplicationLinkTarget target)
    {
        bool exists = File.Exists(target.Target) || Directory.Exists(target.Target);
        return exists
            ? new(target, ApplicationLinkStatus.Ok, "Cible locale disponible.")
            : new(target, ApplicationLinkStatus.Warning, "Cible locale absente actuellement.", DateTime.Now);
    }

    private static string ResolveLaunchTarget(ApplicationLinkTarget target)
    {
        if (target.Action == ApplicationLinkAction.LaunchExecutable)
        {
            return ResolveExecutable(target.Target)
                ?? throw new FileNotFoundException("Exécutable introuvable.", target.Target);
        }
        return target.Target;
    }

    private static string? ResolveExecutable(string name)
    {
        string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string[] direct =
        {
            Path.Combine(system, name),
            Path.Combine(windows, name),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", name),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "cmd", name),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", name)
        };
        string? directMatch = direct.FirstOrDefault(File.Exists);
        if (directMatch is not null) return directMatch;

        foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(folder.Trim(), name);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    private static bool TryGetHttpUri(string value, out Uri? uri)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out uri)
            && uri is not null
            && uri.Scheme is "http" or "https";
    }

    private static bool ContainsTemplateVariables(string value)
    {
        return value.Contains('{') || value.Contains('}');
    }

    private static bool IsTechnicalEndpoint(string value)
    {
        return value.Contains("api.github.com", StringComparison.OrdinalIgnoreCase)
            || value.Contains("timestamp.digicert.com", StringComparison.OrdinalIgnoreCase);
    }

    private static void Add(
        IDictionary<string, ApplicationLinkTarget> targets,
        ApplicationLinkTarget target)
    {
        targets[$"{target.Kind}|{target.Target}"] = target;
    }

    private static string TrimTarget(string value)
    {
        return value.Trim().TrimEnd(';', ',', ')', ']', '}', '"', '\'');
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EPFOptimizerPro-LinkAudit/1.1");
        return client;
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
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using EPFOptimizerPro.Models;

namespace EPFOptimizerPro.Services;

public sealed class GitHubUpdateService
{
    private const string GitHubApiVersion = "2026-03-10";
    private const string GitHubUserAgent = "EPFOptimizerPro-Updater";

    private const string Owner = "patjar";
    private const string Repository = "EPFOptimizerPro";
    private readonly HttpClient _client = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GitHubUpdateService()
    {
        ConfigureGitHubHeaders();
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("EPFOptimizerPro/3.7.3.4");
    }

    public string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    public async Task<UpdateCheckResult> CheckLatestAsync(CancellationToken token)
    {
        string url = $"https://api.github.com/repos/{Owner}/{Repository}/releases";
        string json = await _client.GetStringAsync(url, token);
        List<GitHubRelease>? releases = JsonSerializer.Deserialize<List<GitHubRelease>>(json, _jsonOptions);

        if (releases is null || releases.Count == 0)
        {
            return NoEpfRelease("Aucune release GitHub trouvée.");
        }

        var candidates = releases
            .Where(r => !r.Draft && !r.Prerelease && !IsPreviewRelease(r))
            .Select(r => new { Release = r, Asset = FindEpfUpdateAsset(r), Version = TryNormalizeVersion(r.TagName) })
            .Where(x => x.Asset is not null && x.Version is not null)
            .OrderByDescending(x => x.Version)
            .ToList();

        var selected = candidates.FirstOrDefault();

        if (selected is null)
        {
            return NoEpfRelease("Aucune release contenant un MSI ou ZIP EPFOptimizerPro n'a été trouvée. Les releases WinOptimia sont ignorées.");
        }

        string latest = selected.Version!.ToString();
        bool available = IsNewer(latest, CurrentVersion);

        return new UpdateCheckResult
        {
            CurrentVersion = CurrentVersion,
            LatestVersion = latest,
            UpdateAvailable = available,
            ReleaseUrl = selected.Release.HtmlUrl,
            Notes = selected.Release.Body,
            Asset = selected.Asset
        };
    }

    public async Task<string> DownloadAsync(GitHubAsset asset, IProgress<double>? progress, CancellationToken token)
    {
        string programDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EPFOptimizerPro",
            "Updates");

        string localFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EPFOptimizerPro",
            "Updates");

        string targetFolder = EnsureWritableFolder(programDataFolder, localFolder);

        string safeName = string.IsNullOrWhiteSpace(asset.Name)
            ? "EPFOptimizerPro-update.msi"
            : SanitizeFileName(asset.Name);

        string outputPath = GetAvailableFilePath(targetFolder, safeName);

        using HttpResponseMessage response = await _client.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
        EnsureGitHubSuccess(response, "GitHub update");

        long? totalLength = response.Content.Headers.ContentLength;
        await using Stream source = await response.Content.ReadAsStreamAsync(token);
        await using FileStream target = new(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);

        byte[] buffer = new byte[81920];
        long totalRead = 0;

        while (true)
        {
            int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            if (read == 0) break;

            await target.WriteAsync(buffer.AsMemory(0, read), token);
            totalRead += read;

            if (totalLength.HasValue && totalLength.Value > 0)
            {
                progress?.Report(totalRead * 100.0 / totalLength.Value);
            }
        }

        progress?.Report(100);
        return outputPath;
    }

    private void ConfigureGitHubHeaders()
    {
        string currentUserAgent = _client.DefaultRequestHeaders.UserAgent.ToString();
        if (!currentUserAgent.Contains(GitHubUserAgent, StringComparison.OrdinalIgnoreCase))
        {
            _client.DefaultRequestHeaders.UserAgent.ParseAdd(GitHubUserAgent);
        }

        if (!_client.DefaultRequestHeaders.Accept.ToString().Contains("application/vnd.github+json", StringComparison.OrdinalIgnoreCase))
        {
            _client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }

        if (!_client.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"))
        {
            _client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", GitHubApiVersion);
        }
    }

    private static void EnsureGitHubSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if ((int)response.StatusCode == 403 || (int)response.StatusCode == 429)
        {
            string details = BuildRateLimitDetails(response);
            throw new InvalidOperationException(operation + " impossible : limite GitHub ou acces refuse (" + (int)response.StatusCode + " " + response.ReasonPhrase + "). " + details);
        }

        response.EnsureSuccessStatusCode();
    }

    private static string BuildRateLimitDetails(HttpResponseMessage response)
    {
        string limit = GetHeaderValue(response, "x-ratelimit-limit");
        string remaining = GetHeaderValue(response, "x-ratelimit-remaining");
        string reset = GetHeaderValue(response, "x-ratelimit-reset");
        string retryAfter = GetHeaderValue(response, "retry-after");

        return "RateLimit(limit=" + limit + ", remaining=" + remaining + ", reset=" + reset + ", retry-after=" + retryAfter + ")";
    }

    private static string GetHeaderValue(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out IEnumerable<string>? values))
        {
            return string.Join(",", values);
        }

        return "n/a";
    }
    private UpdateCheckResult NoEpfRelease(string notes)
    {
        return new UpdateCheckResult
        {
            CurrentVersion = CurrentVersion,
            LatestVersion = CurrentVersion,
            UpdateAvailable = false,
            Notes = notes
        };
    }

    private static bool IsPreviewRelease(GitHubRelease release)
    {
        string tag = release.TagName ?? string.Empty;
        string name = release.Name ?? string.Empty;
        string combined = tag + " " + name;

        return combined.Contains("preview", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("pre-release", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("prerelease", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("beta", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("alpha", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("-rc", StringComparison.OrdinalIgnoreCase)
            || combined.Contains(" rc", StringComparison.OrdinalIgnoreCase);
    }
    private static GitHubAsset? FindEpfUpdateAsset(GitHubRelease release)
    {
        return release.Assets.FirstOrDefault(a =>
            (a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                || a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            && (a.Name.Contains("EPFOptimizerPro", StringComparison.OrdinalIgnoreCase)
                || a.Name.Contains("EPF-Optimizer", StringComparison.OrdinalIgnoreCase)
                || a.Name.Contains("EPFOptimizer", StringComparison.OrdinalIgnoreCase)));
    }

    private static Version? TryNormalizeVersion(string tag)
    {
        string clean = NormalizeVersion(tag);
        return Version.TryParse(clean, out Version? version) ? version : null;
    }

    private static string NormalizeVersion(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return "0.0.0";

        string clean = tag.Trim();
        if (clean.StartsWith("v", StringComparison.OrdinalIgnoreCase)) clean = clean[1..];
        if (clean.StartsWith("epf-v", StringComparison.OrdinalIgnoreCase)) clean = clean[5..];
        if (clean.StartsWith("epf-", StringComparison.OrdinalIgnoreCase)) clean = clean[4..];
        if (clean.StartsWith("EPFOptimizerPro-", StringComparison.OrdinalIgnoreCase)) clean = clean[16..];

        int suffixIndex = clean.IndexOf('-');
        if (suffixIndex >= 0) clean = clean[..suffixIndex];

        return clean;
    }

    private static bool IsNewer(string latest, string current)
    {
        if (!Version.TryParse(latest, out Version? latestVersion)) return false;
        if (!Version.TryParse(current, out Version? currentVersion)) return true;
        return latestVersion > currentVersion;
    }

    private static string EnsureWritableFolder(string preferredFolder, string fallbackFolder)
    {
        try
        {
            Directory.CreateDirectory(preferredFolder);
            string probe = Path.Combine(preferredFolder, ".write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(probe, "test");
            File.Delete(probe);
            return preferredFolder;
        }
        catch
        {
            Directory.CreateDirectory(fallbackFolder);
            return fallbackFolder;
        }
    }

    private static string GetAvailableFilePath(string folder, string fileName)
    {
        string candidate = Path.Combine(folder, fileName);
        if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;

        string name = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(folder, name + "-" + stamp + extension);
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');
        return fileName;
    }
}

using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace EPFOptimizerPro.Services;

public static class AuditUpdateChannelDiagnosticService
{
    private const string ReleasesUrl = "https://api.github.com/repos/patjar/EPFOptimizerPro/releases";

    public static async Task<string> BuildReportAsync(CancellationToken token = default)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EPFOptimizerPro-AuditDiagnostic/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");

        using HttpResponseMessage response = await client.GetAsync(ReleasesUrl, token);
        string json = await response.Content.ReadAsStringAsync(token);

        if (!response.IsSuccessStatusCode)
        {
            return $"DIAGNOSTIC DU CANAL DE MISE A JOUR{Environment.NewLine}{Environment.NewLine}" +
                   $"Erreur GitHub : {(int)response.StatusCode} {response.ReasonPhrase}";
        }

        using JsonDocument document = JsonDocument.Parse(json);
        var accepted = new List<ReleaseCandidate>();
        var rejected = new List<string>();

        foreach (JsonElement release in document.RootElement.EnumerateArray())
        {
            string tag = ReadString(release, "tag_name");
            string name = ReadString(release, "name");
            bool draft = ReadBoolean(release, "draft");
            bool prerelease = ReadBoolean(release, "prerelease");
            string combined = tag + " " + name;
            string? rejectionReason = GetRejectionReason(combined, draft, prerelease);

            if (rejectionReason is not null)
            {
                rejected.Add($"{ValueOrUnknown(tag)} -> ignoree : {rejectionReason}");
                continue;
            }

            Version? version = TryNormalizeVersion(tag);
            if (version is null)
            {
                rejected.Add($"{ValueOrUnknown(tag)} -> ignoree : version non reconnue");
                continue;
            }

            AssetInfo? asset = FindUpdateAsset(release);
            if (asset is null)
            {
                rejected.Add($"{ValueOrUnknown(tag)} -> ignoree : aucun MSI ou ZIP EPFOptimizerPro");
                continue;
            }

            accepted.Add(new ReleaseCandidate(tag, version, asset.Name, draft, prerelease));
        }

        ReleaseCandidate? selected = accepted
            .OrderByDescending(candidate => candidate.Version)
            .FirstOrDefault();

        Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        var builder = new StringBuilder();
        builder.AppendLine("DIAGNOSTIC DU CANAL DE MISE A JOUR");
        builder.AppendLine();
        builder.AppendLine("Canal                 : Stable");
        builder.AppendLine($"Version locale         : {currentVersion}");

        if (selected is null)
        {
            builder.AppendLine("Release selectionnee   : aucune");
            builder.AppendLine("Resultat                : aucune release stable exploitable");
        }
        else
        {
            bool updateAvailable = selected.Version > currentVersion;
            builder.AppendLine($"Release selectionnee   : {selected.Tag}");
            builder.AppendLine($"Version distante       : {selected.Version}");
            builder.AppendLine($"Asset selectionne      : {selected.AssetName}");
            builder.AppendLine($"Draft                  : {(selected.Draft ? "oui" : "non")}");
            builder.AppendLine($"Prerelease             : {(selected.Prerelease ? "oui" : "non")}");
            builder.AppendLine($"Resultat               : {(updateAvailable ? "mise a jour disponible" : "aucune mise a jour disponible")}");
        }

        builder.AppendLine();
        builder.AppendLine("RELEASES REJETEES");
        builder.AppendLine();

        if (rejected.Count == 0)
        {
            builder.AppendLine("Aucune release rejetee.");
        }
        else
        {
            foreach (string item in rejected.Take(20))
            {
                builder.AppendLine(item);
            }

            if (rejected.Count > 20)
            {
                builder.AppendLine($"... {rejected.Count - 20} autre(s) release(s) rejetee(s).");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Lecture seule : aucun MSI telecharge, aucune release modifiee.");
        return builder.ToString().TrimEnd();
    }

    private static string? GetRejectionReason(string combined, bool draft, bool prerelease)
    {
        if (draft) return "draft";
        if (prerelease) return "prerelease GitHub";
        if (combined.Contains("preview", StringComparison.OrdinalIgnoreCase)) return "tag ou nom preview";
        if (combined.Contains("pre-release", StringComparison.OrdinalIgnoreCase)) return "tag ou nom pre-release";
        if (combined.Contains("prerelease", StringComparison.OrdinalIgnoreCase)) return "tag ou nom prerelease";
        if (combined.Contains("beta", StringComparison.OrdinalIgnoreCase)) return "tag ou nom beta";
        if (combined.Contains("alpha", StringComparison.OrdinalIgnoreCase)) return "tag ou nom alpha";
        if (combined.Contains("-rc", StringComparison.OrdinalIgnoreCase)
            || combined.Contains(" rc", StringComparison.OrdinalIgnoreCase)) return "tag ou nom RC";
        return null;
    }

    private static AssetInfo? FindUpdateAsset(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out JsonElement assets)
            || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string name = ReadString(asset, "name");
            bool validExtension = name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            bool validProduct = name.Contains("EPFOptimizerPro", StringComparison.OrdinalIgnoreCase)
                || name.Contains("EPF-Optimizer", StringComparison.OrdinalIgnoreCase)
                || name.Contains("EPFOptimizer", StringComparison.OrdinalIgnoreCase);

            if (validExtension && validProduct)
            {
                return new AssetInfo(name);
            }
        }

        return null;
    }

    private static Version? TryNormalizeVersion(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        string clean = tag.Trim();
        if (clean.StartsWith("v", StringComparison.OrdinalIgnoreCase)) clean = clean[1..];
        if (clean.StartsWith("epf-v", StringComparison.OrdinalIgnoreCase)) clean = clean[5..];
        if (clean.StartsWith("epf-", StringComparison.OrdinalIgnoreCase)) clean = clean[4..];
        if (clean.StartsWith("EPFOptimizerPro-", StringComparison.OrdinalIgnoreCase)) clean = clean[16..];

        int suffixIndex = clean.IndexOf('-');
        if (suffixIndex >= 0) clean = clean[..suffixIndex];

        return Version.TryParse(clean, out Version? version) ? version : null;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool ReadBoolean(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            && value.GetBoolean();
    }

    private static string ValueOrUnknown(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "tag inconnu" : value;
    }

    private sealed record AssetInfo(string Name);

    private sealed record ReleaseCandidate(
        string Tag,
        Version Version,
        string AssetName,
        bool Draft,
        bool Prerelease);
}
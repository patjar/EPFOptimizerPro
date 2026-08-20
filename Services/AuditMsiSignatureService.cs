using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EPFOptimizerPro.Services;

public static class AuditMsiSignatureService
{
    private const string ExpectedSigner = "PROD_CLEARPASS";
    private const string ExpectedThumbprint = "A2AA77761B29B66D7F67C5E272F0797954DEB101";

    public static async Task<string> BuildReportAsync(CancellationToken token = default)
    {
        string projectRoot = FindProjectRoot();
        FileInfo? msi = FindLatestMsi(projectRoot);

        if (msi is null)
        {
            return "VERIFICATION DU MSI" + Environment.NewLine + Environment.NewLine +
                   "Aucun fichier EPFOptimizerPro-Setup-v*.msi trouve dans le dossier dist.";
        }

        string sha256 = await ComputeSha256Async(msi.FullName, token);
        SignatureInfo signature = await ReadSignatureAsync(msi.FullName, token);
        string version = ExtractVersion(msi.Name);
        bool validSignature = signature.Status.Equals("Valid", StringComparison.OrdinalIgnoreCase);
        bool expectedSubject = signature.Subject.Contains(ExpectedSigner, StringComparison.OrdinalIgnoreCase);
        bool expectedThumbprint = NormalizeThumbprint(signature.Thumbprint)
            .Equals(ExpectedThumbprint, StringComparison.OrdinalIgnoreCase);
        bool certificateNotExpired = signature.NotAfter is null || signature.NotAfter.Value >= DateTime.Now;
        bool ready = validSignature && expectedSubject && expectedThumbprint && certificateNotExpired;

        var builder = new StringBuilder();
        builder.AppendLine("VERIFICATION DU DERNIER MSI");
        builder.AppendLine();
        builder.AppendLine($"Fichier                : {msi.Name}");
        builder.AppendLine($"Chemin                 : {msi.FullName}");
        builder.AppendLine($"Version du nom         : {Value(version)}");
        builder.AppendLine($"Taille                 : {msi.Length:N0} octets");
        builder.AppendLine($"Modifie le             : {msi.LastWriteTime:dd/MM/yyyy HH:mm:ss}");
        builder.AppendLine($"SHA-256                : {sha256}");
        builder.AppendLine();
        builder.AppendLine("SIGNATURE AUTHENTICODE");
        builder.AppendLine();
        builder.AppendLine($"Statut                 : {Value(signature.Status)}");
        builder.AppendLine($"Message                : {GetLocalizedStatusMessage(signature.Status, signature.StatusMessage)}");
        builder.AppendLine($"Sujet                  : {Value(signature.Subject)}");
        builder.AppendLine($"Emetteur               : {Value(signature.Issuer)}");
        builder.AppendLine($"Empreinte SHA1         : {Value(signature.Thumbprint)}");
        builder.AppendLine($"Valide du              : {FormatDate(signature.NotBefore)}");
        builder.AppendLine($"Valide jusqu'au        : {FormatDate(signature.NotAfter)}");
        builder.AppendLine($"Horodatage             : {(signature.IsTimestamped ? "present" : "non expose ou absent")}");
        builder.AppendLine($"Certificat horodatage  : {Value(signature.TimeStamperSubject)}");
        builder.AppendLine();
        builder.AppendLine("CONTROLES");
        builder.AppendLine();
        AddCheck(builder, "Signature Authenticode valide", validSignature, signature.Status);
        AddCheck(builder, $"Sujet attendu {ExpectedSigner}", expectedSubject, signature.Subject);
        AddCheck(builder, "Empreinte PROD_CLEARPASS attendue", expectedThumbprint, signature.Thumbprint);
        AddCheck(builder, "Certificat non expire", certificateNotExpired, FormatDate(signature.NotAfter));
        AddCheck(builder, "SHA-256 calcule", !string.IsNullOrWhiteSpace(sha256), sha256);
        builder.AppendLine();
        builder.AppendLine($"VERDICT : {(ready ? "PRET POUR PUBLICATION" : "ATTENTION REQUISE")}");
        builder.AppendLine("Lecture seule : le MSI et le certificat n'ont pas ete modifies.");
        return builder.ToString().TrimEnd();
    }

    private static string GetLocalizedStatusMessage(string status, string fallback)
    {
        return status switch
        {
            "Valid" => "Signature vérifiée.",
            "NotSigned" => "Le fichier n'est pas signé.",
            "HashMismatch" => "La signature ne correspond pas au contenu du fichier.",
            "NotTrusted" => "La chaîne de certification n'est pas approuvée.",
            "UnknownError" => "La vérification de la signature a retourné une erreur inconnue.",
            _ => Value(fallback)
        };
    }
    private static void AddCheck(StringBuilder builder, string label, bool ok, string details)
    {
        builder.AppendLine($"[{(ok ? "OK" : "ERREUR")}] {label}");
        builder.AppendLine($"  {Value(details)}");
    }

    private static FileInfo? FindLatestMsi(string projectRoot)
    {
        string dist = Path.Combine(projectRoot, "dist");
        if (!Directory.Exists(dist)) return null;

        return new DirectoryInfo(dist)
            .EnumerateFiles("EPFOptimizerPro-Setup-v*.msi", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.LastWriteTime)
            .FirstOrDefault();
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken token)
    {
        await using FileStream stream = new(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, token);
        return Convert.ToHexString(hash);
    }

    private static async Task<SignatureInfo> ReadSignatureAsync(string path, CancellationToken token)
    {
        string executable = ResolvePowerShellExecutable();
        string escapedPath = path.Replace("'", "''", StringComparison.Ordinal);
        string command =
            "$s=Get-AuthenticodeSignature -LiteralPath '" + escapedPath + "';" +
            "$o=[ordered]@{" +
            "Status=$s.Status.ToString();" +
            "StatusMessage=$s.StatusMessage;" +
            "Subject=if($s.SignerCertificate){$s.SignerCertificate.Subject}else{''};" +
            "Issuer=if($s.SignerCertificate){$s.SignerCertificate.Issuer}else{''};" +
            "Thumbprint=if($s.SignerCertificate){$s.SignerCertificate.Thumbprint}else{''};" +
            "NotBefore=if($s.SignerCertificate){$s.SignerCertificate.NotBefore.ToString('o')}else{''};" +
            "NotAfter=if($s.SignerCertificate){$s.SignerCertificate.NotAfter.ToString('o')}else{''};" +
            "IsTimestamped=($null -ne $s.TimeStamperCertificate);" +
            "TimeStamperSubject=if($s.TimeStamperCertificate){$s.TimeStamperCertificate.Subject}else{''}" +
            "};$o|ConvertTo-Json -Compress";

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" +
                command.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync(token);
        string error = await process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            return new SignatureInfo(
                "Erreur", string.IsNullOrWhiteSpace(error) ? "PowerShell n'a retourne aucun resultat." : error.Trim(),
                string.Empty, string.Empty, string.Empty, null, null, false, string.Empty);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(output.Trim());
            JsonElement root = document.RootElement;
            return new SignatureInfo(
                ReadString(root, "Status"),
                ReadString(root, "StatusMessage"),
                ReadString(root, "Subject"),
                ReadString(root, "Issuer"),
                ReadString(root, "Thumbprint"),
                ReadDate(root, "NotBefore"),
                ReadDate(root, "NotAfter"),
                ReadBoolean(root, "IsTimestamped"),
                ReadString(root, "TimeStamperSubject"));
        }
        catch (Exception ex)
        {
            return new SignatureInfo(
                "Erreur", "Lecture du resultat PowerShell impossible : " + ex.Message,
                string.Empty, string.Empty, string.Empty, null, null, false, string.Empty);
        }
    }

    private static string ResolvePowerShellExecutable()
    {
        string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string windowsPowerShell = Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(windowsPowerShell)) return windowsPowerShell;
        return "powershell.exe";
    }

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

    private static string ExtractVersion(string value)
    {
        Match match = Regex.Match(value, @"(?<version>\d+\.\d+\.\d+(?:\.\d+)?)");
        return match.Success ? match.Groups["version"].Value : string.Empty;
    }

    private static string NormalizeThumbprint(string value)
    {
        return Regex.Replace(value ?? string.Empty, "[^A-Fa-f0-9]", string.Empty);
    }

    private static string ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool ReadBoolean(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            && value.GetBoolean();
    }

    private static DateTime? ReadDate(JsonElement element, string name)
    {
        string value = ReadString(element, name);
        return DateTime.TryParse(value, out DateTime date) ? date : null;
    }

    private static string FormatDate(DateTime? value)
    {
        return value is null ? "Non disponible" : value.Value.ToString("dd/MM/yyyy HH:mm:ss");
    }

    private static string Value(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Non disponible" : value;
    }

    private sealed record SignatureInfo(
        string Status,
        string StatusMessage,
        string Subject,
        string Issuer,
        string Thumbprint,
        DateTime? NotBefore,
        DateTime? NotAfter,
        bool IsTimestamped,
        string TimeStamperSubject);
}
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace EPFOptimizerPro.Services;

public static class AuditWindowsCertificateStoreService
{
    private const string ExpectedName = "PROD_CLEARPASS";
    private const string ExpectedThumbprint = "A2AA77761B29B66D7F67C5E272F0797954DEB101";

    private static readonly IReadOnlyList<(StoreLocation Location, StoreName Name)> Stores =
        new List<(StoreLocation, StoreName)>
        {
            (StoreLocation.LocalMachine, StoreName.Root),
            (StoreLocation.LocalMachine, StoreName.TrustedPublisher),
            (StoreLocation.CurrentUser, StoreName.Root),
            (StoreLocation.CurrentUser, StoreName.TrustedPublisher)
        };

    public static string BuildReport()
    {
        var matches = new List<CertificateLocation>();
        var errors = new List<string>();

        foreach ((StoreLocation location, StoreName name) in Stores)
        {
            try
            {
                using var store = new X509Store(name, location);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                foreach (X509Certificate2 certificate in store.Certificates)
                {
                    if (!NormalizeThumbprint(certificate.Thumbprint)
                            .Equals(ExpectedThumbprint, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    matches.Add(new CertificateLocation(
                        location,
                        name,
                        certificate.Subject,
                        certificate.Issuer,
                        certificate.Thumbprint ?? string.Empty,
                        certificate.NotBefore,
                        certificate.NotAfter));
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{location}\\{name} : {ex.Message}");
            }
        }

        DateTime now = DateTime.Now;
        bool found = matches.Count > 0;
        bool valid = matches.Any(item => item.NotBefore <= now && item.NotAfter >= now);

        var builder = new StringBuilder();
        builder.AppendLine("CONTROLE DU CERTIFICAT WINDOWS");
        builder.AppendLine();
        builder.AppendLine($"Nom attendu           : {ExpectedName}");
        builder.AppendLine($"Empreinte attendue    : {ExpectedThumbprint}");
        builder.AppendLine($"Certificat detecte    : {(found ? "Oui" : "Non")}");
        builder.AppendLine($"Certificat valide     : {(valid ? "Oui" : "Non")}");
        builder.AppendLine();
        builder.AppendLine("MAGASINS CONTROLES");
        builder.AppendLine();

        foreach ((StoreLocation location, StoreName name) in Stores)
        {
            builder.AppendLine($"- {location}\\{name}");
        }

        builder.AppendLine();
        builder.AppendLine("RESULTATS");
        builder.AppendLine();

        if (matches.Count == 0)
        {
            builder.AppendLine("Aucun certificat correspondant a l'empreinte attendue n'a ete trouve.");
        }
        else
        {
            foreach (CertificateLocation match in matches)
            {
                bool itemValid = match.NotBefore <= now && match.NotAfter >= now;
                builder.AppendLine($"Magasin               : {match.Location}\\{match.Name}");
                builder.AppendLine($"Sujet                 : {match.Subject}");
                builder.AppendLine($"Emetteur              : {match.Issuer}");
                builder.AppendLine($"Empreinte SHA1        : {match.Thumbprint}");
                builder.AppendLine($"Valide du             : {match.NotBefore:dd/MM/yyyy HH:mm:ss}");
                builder.AppendLine($"Valide jusqu'au       : {match.NotAfter:dd/MM/yyyy HH:mm:ss}");
                builder.AppendLine($"Etat                  : {(itemValid ? "Installe et valide" : "Installe mais expire ou pas encore valide")}");
                builder.AppendLine();
            }
        }

        if (errors.Count > 0)
        {
            builder.AppendLine("MAGASINS NON LISIBLES");
            builder.AppendLine();
            foreach (string error in errors) builder.AppendLine("- " + error);
            builder.AppendLine();
        }

        builder.AppendLine($"VERDICT : {(valid ? "CERTIFICAT INSTALLE ET VALIDE" : found ? "CERTIFICAT INSTALLE MAIS NON VALIDE" : "CERTIFICAT NON INSTALLE")}");
        builder.AppendLine("Lecture seule : aucun certificat et aucun magasin Windows n'ont ete modifies.");
        return builder.ToString().TrimEnd();
    }

    private static string NormalizeThumbprint(string? value)
    {
        return new string((value ?? string.Empty)
            .Where(Uri.IsHexDigit)
            .ToArray())
            .ToUpperInvariant();
    }

    private sealed record CertificateLocation(
        StoreLocation Location,
        StoreName Name,
        string Subject,
        string Issuer,
        string Thumbprint,
        DateTime NotBefore,
        DateTime NotAfter);
}

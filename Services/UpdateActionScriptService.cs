using System.Diagnostics;
using System.Text;
using System.Windows;

namespace EPFOptimizerPro.Services;

public static class UpdateActionScriptService
{
    public static void StartPowerShell(Window owner, string title, string script)
    {
        try
        {
            string safeTitle = title.Replace("'", string.Empty);
            string header =
                "$Host.UI.RawUI.WindowTitle = 'EPFOptimizerPro - " + safeTitle + "';" + Environment.NewLine +
                "Write-Host 'EPFOptimizerPro - " + safeTitle + "' -ForegroundColor Cyan;" + Environment.NewLine +
                "Write-Host '';" + Environment.NewLine;

            string footer =
                Environment.NewLine +
                "Write-Host '';" + Environment.NewLine +
                "Read-Host 'Appuie sur Entree pour fermer'" + Environment.NewLine;

            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(header + script + footer));

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-ExecutionPolicy Bypass -EncodedCommand " + encoded,
                UseShellExecute = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner,
                "Impossible de lancer l'action." + Environment.NewLine + ex.Message,
                "Gestion des mises a jour",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public static string BuildWindowsUpdateDetailsScript()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Continue'",
            "try {",
            "    $session = New-Object -ComObject Microsoft.Update.Session",
            "    $searcher = $session.CreateUpdateSearcher()",
            "    $result = $searcher.Search('IsInstalled=0 and IsHidden=0')",
            "    Write-Host ('Windows Update : ' + $result.Updates.Count + ' update(s) detectee(s)') -ForegroundColor Yellow",
            "    Write-Host ''",
            "    if ($result.Updates.Count -eq 0) { Write-Host 'Aucune mise a jour Windows disponible.' -ForegroundColor Green; return }",
            "    for ($i = 0; $i -lt $result.Updates.Count; $i++) {",
            "        $update = $result.Updates.Item($i)",
            "        Write-Host ('[' + ($i + 1) + '] ' + $update.Title) -ForegroundColor White",
            "        if ($update.KBArticleIDs.Count -gt 0) { Write-Host ('    KB : ' + (($update.KBArticleIDs | ForEach-Object { 'KB' + $_ }) -join ', ')) }",
            "        Write-Host ('    Type : ' + $update.Type)",
            "        Write-Host ('    Taille approx : ' + [math]::Round(($update.MaxDownloadSize / 1MB), 2) + ' Mo')",
            "        Write-Host ''",
            "    }",
            "}",
            "catch {",
            "    Write-Host 'Erreur pendant la lecture Windows Update :' -ForegroundColor Red",
            "    Write-Host $_.Exception.Message -ForegroundColor Red",
            "}"
        });
    }

    public static string BuildWindowsUpdateInstallScript()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Continue'",
            "try {",
            "    $session = New-Object -ComObject Microsoft.Update.Session",
            "    $searcher = $session.CreateUpdateSearcher()",
            "    $result = $searcher.Search('IsInstalled=0 and IsHidden=0')",
            "    Write-Host ('Windows Update : ' + $result.Updates.Count + ' update(s) detectee(s)') -ForegroundColor Yellow",
            "    Write-Host ''",
            "    if ($result.Updates.Count -eq 0) { Write-Host 'Aucune mise a jour Windows disponible.' -ForegroundColor Green; return }",
            "    $updatesToInstall = New-Object -ComObject Microsoft.Update.UpdateColl",
            "    for ($i = 0; $i -lt $result.Updates.Count; $i++) {",
            "        $update = $result.Updates.Item($i)",
            "        Write-Host ('[' + ($i + 1) + '] ' + $update.Title) -ForegroundColor White",
            "        if ($update.KBArticleIDs.Count -gt 0) { Write-Host ('    KB : ' + (($update.KBArticleIDs | ForEach-Object { 'KB' + $_ }) -join ', ')) }",
            "        Write-Host ('    Type : ' + $update.Type)",
            "        Write-Host ('    Taille approx : ' + [math]::Round(($update.MaxDownloadSize / 1MB), 2) + ' Mo')",
            "        if (-not $update.EulaAccepted) { try { $update.AcceptEula() } catch {} }",
            "        [void]$updatesToInstall.Add($update)",
            "        Write-Host ''",
            "    }",
            "    Write-Host 'Cette action va telecharger puis installer les mises a jour Windows listees ci-dessus.' -ForegroundColor Yellow",
            "    $answer = Read-Host 'Continuer ? Tape O puis Entree pour confirmer'",
            "    if ($answer -notin @('O','o','Oui','oui','Y','y','Yes','yes')) { Write-Host 'Installation annulee par utilisateur.' -ForegroundColor Yellow; return }",
            "    Write-Host ''",
            "    Write-Host 'Telechargement Windows Update...' -ForegroundColor Yellow",
            "    $downloader = $session.CreateUpdateDownloader()",
            "    $downloader.Updates = $updatesToInstall",
            "    $downloadResult = $downloader.Download()",
            "    Write-Host ('Resultat telechargement : ' + $downloadResult.ResultCode)",
            "    Write-Host ''",
            "    Write-Host 'Installation Windows Update...' -ForegroundColor Yellow",
            "    $installer = $session.CreateUpdateInstaller()",
            "    $installer.Updates = $updatesToInstall",
            "    $installResult = $installer.Install()",
            "    Write-Host ('Resultat installation : ' + $installResult.ResultCode) -ForegroundColor Green",
            "    Write-Host ('Redemarrage requis : ' + $installResult.RebootRequired)",
            "    Write-Host ''",
            "    for ($i = 0; $i -lt $updatesToInstall.Count; $i++) {",
            "        $updateResult = $installResult.GetUpdateResult($i)",
            "        Write-Host ('[' + ($i + 1) + '] ' + $updatesToInstall.Item($i).Title)",
            "        Write-Host ('    Resultat : ' + $updateResult.ResultCode + ' / HResult : ' + $updateResult.HResult)",
            "    }",
            "}",
            "catch {",
            "    Write-Host 'Erreur pendant installation Windows Update :' -ForegroundColor Red",
            "    Write-Host $_.Exception.Message -ForegroundColor Red",
            "}"
        });
    }

    public static string BuildWingetAppsListScript()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Continue'",
            "$winget = Get-Command winget -ErrorAction SilentlyContinue",
            "if ($null -eq $winget) { Write-Host 'winget est introuvable sur ce poste.' -ForegroundColor Red; return }",
            "Write-Host 'Mise a jour de la source winget...' -ForegroundColor Yellow",
            "winget source update --name winget --disable-interactivity",
            "Write-Host ''",
            "Write-Host 'Applications avec mises a jour disponibles via winget :' -ForegroundColor Yellow",
            "winget upgrade --source winget --accept-source-agreements --disable-interactivity"
        });
    }

    public static string BuildWingetAppsUpdateScript()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Continue'",
            "$winget = Get-Command winget -ErrorAction SilentlyContinue",
            "if ($null -eq $winget) { Write-Host 'winget est introuvable sur ce poste.' -ForegroundColor Red; return }",
            "Write-Host 'Mise a jour de la source winget...' -ForegroundColor Yellow",
            "winget source update --name winget --disable-interactivity",
            "Write-Host ''",
            "Write-Host 'Applications detectees :' -ForegroundColor Yellow",
            "winget upgrade --source winget --accept-source-agreements --disable-interactivity",
            "Write-Host ''",
            "Write-Host 'Cette action va installer toutes les mises a jour applicatives winget listees ci-dessus.' -ForegroundColor Yellow",
            "$answer = Read-Host 'Continuer ? Tape O puis Entree pour confirmer'",
            "if ($answer -notin @('O','o','Oui','oui','Y','y','Yes','yes')) { Write-Host 'Installation annulee par utilisateur.' -ForegroundColor Yellow; return }",
            "Write-Host ''",
            "Write-Host 'Installation des mises a jour applicatives winget...' -ForegroundColor Yellow",
            "winget upgrade --all --source winget --accept-source-agreements --accept-package-agreements --disable-interactivity",
            "Write-Host ''",
            "Write-Host 'Action winget terminee.' -ForegroundColor Green"
        });
    }

    public static string BuildMicrosoftStoreOpenScript()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Continue'",
            "Write-Host 'Ouverture de Microsoft Store sur la page des mises a jour...' -ForegroundColor Yellow",
            "try { Start-Process 'ms-windows-store://downloadsandupdates' }",
            "catch {",
            "    Write-Host 'Impossible d ouvrir la page directe des mises a jour. Ouverture du Microsoft Store.' -ForegroundColor Yellow",
            "    Start-Process 'ms-windows-store:'",
            "}"
        });
    }
}
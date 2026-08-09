$ErrorActionPreference = "Stop"

# Bump EPF Optimizer Pro de 3.9.36 vers 3.9.36
# Objectif : preparer un test version +1 propre.
# Le script ne commit rien et ne pousse rien.

$ProjectRoot = "C:\Users\pkjn\Documents\EPFOptimizerPro-Clean"
$OldVersion = "3.9.36.0"
$NewVersion = "3.9.36.0"
$OldShort = "3.9.36"
$NewShort = "3.9.36"
$BackupRoot = "C:\EPFOptimizerPro-VersionBackup\$(Get-Date -Format 'yyyyMMdd_HHmmss')"

Write-Host ""
Write-Host "=== Bump EPF Optimizer Pro $OldVersion -> $NewVersion ===" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $ProjectRoot)) {
    throw "Dossier projet introuvable : $ProjectRoot"
}

Set-Location $ProjectRoot

New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
Write-Host "Sauvegarde des fichiers modifies : $BackupRoot" -ForegroundColor Yellow
Write-Host ""

$ExcludedFolders = @(".git", "bin", "obj", "publish", "dist", "installer-build")
$Extensions = @("*.csproj", "*.cs", "*.xaml", "*.ps1", "*.md", "*.txt", "*.json", "*.config")

$Files = foreach ($Ext in $Extensions) {
    Get-ChildItem -Path $ProjectRoot -Recurse -File -Filter $Ext | Where-Object {
        $Full = $_.FullName
        -not ($ExcludedFolders | Where-Object { $Full -like "*$([IO.Path]::DirectorySeparatorChar)$_$([IO.Path]::DirectorySeparatorChar)*" })
    }
}

$Changed = @()

foreach ($File in ($Files | Sort-Object FullName -Unique)) {
    $Text = Get-Content $File.FullName -Raw -Encoding UTF8
    $NewText = $Text.Replace($OldVersion, $NewVersion).Replace($OldShort, $NewShort)

    if ($NewText -ne $Text) {
        $Relative = $File.FullName.Substring($ProjectRoot.Length).TrimStart('\')
        $BackupFile = Join-Path $BackupRoot $Relative
        New-Item -ItemType Directory -Path (Split-Path $BackupFile -Parent) -Force | Out-Null
        Copy-Item $File.FullName $BackupFile -Force
        Set-Content $File.FullName -Value $NewText -Encoding UTF8
        $Changed += $Relative
    }
}

if ($Changed.Count -eq 0) {
    Write-Host "Aucun fichier contenant $OldVersion ou $OldShort n'a ete trouve." -ForegroundColor Yellow
} else {
    Write-Host "Fichiers modifies :" -ForegroundColor Cyan
    $Changed | ForEach-Object { Write-Host " - $_" }
}

Write-Host ""
Write-Host "Verification des references restantes a $OldShort :" -ForegroundColor Cyan
$Remaining = Get-ChildItem -Path $ProjectRoot -Recurse -File -Include $Extensions | Where-Object {
    $Full = $_.FullName
    -not ($ExcludedFolders | Where-Object { $Full -like "*$([IO.Path]::DirectorySeparatorChar)$_$([IO.Path]::DirectorySeparatorChar)*" })
} | Select-String -Pattern $OldShort -ErrorAction SilentlyContinue

if ($Remaining) {
    $Remaining | ForEach-Object { Write-Host (" - " + $_.Path + ":" + $_.LineNumber + ": " + $_.Line.Trim()) -ForegroundColor Yellow }
} else {
    Write-Host "Aucune reference restante a $OldShort dans les fichiers sources suivis." -ForegroundColor Green
}

Write-Host ""
Write-Host "Build Release de controle..." -ForegroundColor Cyan
dotnet build .\EPFOptimizerPro.csproj -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERREUR build. Les sauvegardes sont ici : $BackupRoot" -ForegroundColor Red
    throw "Build echoue apres bump version"
}

Write-Host ""
Write-Host "[OK] Version passee en $NewVersion et build reussi." -ForegroundColor Green
Write-Host ""
Write-Host "Etat Git :" -ForegroundColor Cyan
git status --short

Write-Host ""
Write-Host "Suite conseillee si le test visuel est OK :" -ForegroundColor Yellow
Write-Host "git add ."
Write-Host "git commit -m \"Version EPF Optimizer Pro v$NewShort\""
Write-Host "git push"
Write-Host ""
Write-Host "Puis refaire : publish, signature exe, MSI, signature MSI." -ForegroundColor Yellow
Write-Host ""


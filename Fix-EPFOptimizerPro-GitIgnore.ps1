param(
    [switch]$Push
)

$ErrorActionPreference = "Stop"

# Fix EPFOptimizerPro clean repository:
# - removes the accidental file ..gitignore if it exists
# - updates the real .gitignore
# - keeps generated folders out of Git: bin, obj, publish, dist, installer-build
# - commits only the useful scripts and .gitignore
# - optionally pushes if launched with -Push

$ProjectRoot = "C:\Users\pkjn\Documents\EPFOptimizerPro-Clean"

Write-Host ""
Write-Host "=== Nettoyage Git EPFOptimizerPro ===" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $ProjectRoot)) {
    throw "Dossier projet introuvable : $ProjectRoot"
}

Set-Location $ProjectRoot

Write-Host "Dossier : $ProjectRoot"
Write-Host ""

# Remove accidental wrong file created with Add-Content ..gitignore
$WrongGitIgnore = Join-Path $ProjectRoot "..gitignore"
if (Test-Path $WrongGitIgnore) {
    Write-Host "Suppression du mauvais fichier ..gitignore" -ForegroundColor Yellow
    Remove-Item $WrongGitIgnore -Force
}

# Ensure real .gitignore has all generated folders
$GitIgnore = Join-Path $ProjectRoot ".gitignore"
$RequiredLines = @(
    "bin/",
    "obj/",
    "publish/",
    "dist/",
    "installer-build/"
)

if (Test-Path $GitIgnore) {
    $CurrentLines = Get-Content $GitIgnore
} else {
    $CurrentLines = @()
}

$CleanLines = @()
foreach ($Line in ($CurrentLines + $RequiredLines)) {
    $Trimmed = $Line.Trim()
    if ($Trimmed -ne "" -and -not ($CleanLines -contains $Trimmed)) {
        $CleanLines += $Trimmed
    }
}

$CleanLines | Set-Content $GitIgnore -Encoding UTF8

Write-Host "Mise a jour de .gitignore : OK" -ForegroundColor Green
Write-Host ""

Write-Host "Etat Git apres correction :" -ForegroundColor Cyan
git status --short
Write-Host ""

# Add only useful files, never generated folders
$FilesToAdd = @(".gitignore")

if (Test-Path (Join-Path $ProjectRoot "Sign-EPFOptimizerPro.ps1")) {
    $FilesToAdd += "Sign-EPFOptimizerPro.ps1"
}

if (Test-Path (Join-Path $ProjectRoot "Build-EPFOptimizerPro-MSI.ps1")) {
    $FilesToAdd += "Build-EPFOptimizerPro-MSI.ps1"
}

Write-Host "Ajout Git des fichiers utiles uniquement :" -ForegroundColor Cyan
foreach ($File in $FilesToAdd) {
    Write-Host " - $File"
}
Write-Host ""

git add -- $FilesToAdd

$Staged = git diff --cached --name-only
if (-not $Staged) {
    Write-Host "Aucun changement utile a committer." -ForegroundColor Yellow
} else {
    Write-Host "Fichiers prepares pour commit :" -ForegroundColor Cyan
    $Staged | ForEach-Object { Write-Host " - $_" }
    Write-Host ""

    git commit -m "Ajout scripts signature et creation MSI"
    Write-Host "Commit cree." -ForegroundColor Green
}

Write-Host ""
Write-Host "Etat Git final :" -ForegroundColor Cyan
git status

if ($Push) {
    Write-Host ""
    Write-Host "Push vers origin/main..." -ForegroundColor Cyan
    git push
    Write-Host ""
    Write-Host "Etat Git apres push :" -ForegroundColor Cyan
    git status
} else {
    Write-Host ""
    Write-Host "Push non effectue. Pour pousser ensuite : git push" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "[OK] Nettoyage Git termine." -ForegroundColor Green
Write-Host ""

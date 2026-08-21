<#
.SYNOPSIS
Met a jour proprement le depot Git EPFOptimizerPro depuis origin/main.

.DESCRIPTION
- Verifie que la commande est lancee dans le bon depot.
- Refuse toute mise a jour si des modifications locales sont presentes.
- Recupere les branches et les tags distants.
- Bascule sur main si necessaire.
- Applique uniquement une avance rapide avec git pull --ff-only.
- Affiche le commit, le dernier tag et l etat final.

Aucune suppression, aucun reset force et aucun stash automatique.
#>

[CmdletBinding()]
param(
    [string]$ProjectPath = "C:\Users\pkjn\Documents\EPFOptimizerPro-Clean"
)

$ErrorActionPreference = "Stop"

function Invoke-Git {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Echec Git : git $($Arguments -join ' ')"
    }
}

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "MISE A JOUR GIT EPFOPTIMIZERPRO" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git est introuvable dans le PATH."
}

if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
    throw "Dossier projet introuvable : $ProjectPath"
}

Set-Location -LiteralPath $ProjectPath

if (-not (Test-Path -LiteralPath ".git" -PathType Container)) {
    throw "Ce dossier n est pas un depot Git : $ProjectPath"
}

$TopLevel = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0) {
    throw "Impossible de lire la racine du depot Git."
}

$RemoteUrl = (& git remote get-url origin 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($RemoteUrl)) {
    throw "Le depot distant origin est introuvable."
}

Write-Host "Depot   : $TopLevel" -ForegroundColor DarkGray
Write-Host "Distant : $RemoteUrl" -ForegroundColor DarkGray
Write-Host ""

$LocalChanges = @(& git status --porcelain)
if ($LASTEXITCODE -ne 0) {
    throw "Impossible de lire l etat Git."
}

if ($LocalChanges.Count -gt 0) {
    Write-Host "Mise a jour annulee : des changements locaux sont presents." -ForegroundColor Yellow
    Write-Host ""
    $LocalChanges | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
    Write-Host ""
    Write-Host "Commit, stash ou annule ces changements avant de relancer le script." -ForegroundColor Yellow
    exit 2
}

Write-Host "Recuperation de origin et des tags..." -ForegroundColor Cyan
Invoke-Git -Arguments @("fetch", "origin", "--prune", "--tags")

$CurrentBranch = (& git branch --show-current).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Impossible de determiner la branche courante."
}

if ($CurrentBranch -ne "main") {
    Write-Host "Bascule de $CurrentBranch vers main..." -ForegroundColor Cyan
    Invoke-Git -Arguments @("switch", "main")
}

Write-Host "Mise a jour de main en avance rapide uniquement..." -ForegroundColor Cyan
Invoke-Git -Arguments @("pull", "--ff-only", "origin", "main")

$Head = (& git log -1 --oneline --decorate).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Impossible de lire le commit courant."
}

$LatestTag = (& git describe --tags --abbrev=0 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($LatestTag)) {
    $LatestTag = "Aucun tag"
}

$FinalChanges = @(& git status --porcelain)
if ($LASTEXITCODE -ne 0) {
    throw "Impossible de verifier l etat final."
}

Write-Host ""
Write-Host "[OK] Depot Git mis a jour proprement." -ForegroundColor Green
Write-Host "Commit : $Head" -ForegroundColor Green
Write-Host "Tag    : $LatestTag" -ForegroundColor Green

if ($FinalChanges.Count -eq 0) {
    Write-Host "Etat   : working tree clean" -ForegroundColor Green
}
else {
    Write-Host "Etat   : changements detectes apres la mise a jour" -ForegroundColor Yellow
    $FinalChanges | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
}

Write-Host ""

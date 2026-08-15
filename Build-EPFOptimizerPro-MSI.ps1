# EPF Optimizer Pro - MSI build wrapper
# This file intentionally delegates to the maintained autonomous MSI build script.
# Do not put version numbers here.

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Target = Join-Path $ScriptDir 'Build-EPFOptimizerPro-MSI-v2-StartMenu-x64.ps1'

if (-not (Test-Path $Target)) {
    throw "Autonomous MSI build script not found: $Target"
}

Write-Host "Delegating MSI build to: $Target" -ForegroundColor Cyan
& $Target @args
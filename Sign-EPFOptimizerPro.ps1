$ErrorActionPreference = "Stop"

# Script de signature EPF Optimizer Pro
# Le mot de passe du certificat n'est PAS stocke dans ce script.
# Il est demande par PowerShell, utilise en memoire, puis oublie.

$ProjectRoot = "C:\Users\pkjn\Documents\EPFOptimizerPro-Clean"
$SignTool = "C:\Program Files (x86)\Windows Kits\10\Tools\bin\i386\signtool.exe"
$CertFile = "C:\Certificats\RADIUSServerCertificate.p12"
$ExeFile = Join-Path $ProjectRoot "publish\win-x64\EPFOptimizerPro.exe"

Write-Host ""
Write-Host "=== Signature EPF Optimizer Pro ===" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $ProjectRoot)) {
    throw "Dossier projet introuvable : $ProjectRoot"
}

if (-not (Test-Path $SignTool)) {
    throw "SignTool introuvable : $SignTool"
}

if (-not (Test-Path $CertFile)) {
    throw "Certificat introuvable : $CertFile"
}

if (-not (Test-Path $ExeFile)) {
    throw "Executable introuvable : $ExeFile. Lance d'abord : dotnet publish .\EPFOptimizerPro.csproj -c Release -r win-x64 --self-contained false -o .\publish\win-x64"
}

Write-Host "Projet     : $ProjectRoot"
Write-Host "SignTool   : $SignTool"
Write-Host "Certificat : $CertFile"
Write-Host "Executable : $ExeFile"
Write-Host ""
Write-Host "Le mot de passe du certificat va etre demande maintenant." -ForegroundColor Yellow
Write-Host "Il n'est pas stocke dans le script." -ForegroundColor Yellow
Write-Host ""

$SecurePassword = Read-Host "Mot de passe du certificat" -AsSecureString
$Bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecurePassword)
$PlainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($Bstr)

try {
    & $SignTool sign /f $CertFile /p $PlainPassword /fd SHA256 /tr "http://timestamp.digicert.com" /td SHA256 /v $ExeFile

    if ($LASTEXITCODE -ne 0) {
        throw "Erreur pendant la signature de EPFOptimizerPro.exe"
    }
}
finally {
    if ($Bstr -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($Bstr)
    }
    Remove-Variable PlainPassword -ErrorAction SilentlyContinue
    Remove-Variable SecurePassword -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Verification de la signature..." -ForegroundColor Cyan
Write-Host ""

& $SignTool verify /pa /v $ExeFile

if ($LASTEXITCODE -ne 0) {
    throw "Erreur pendant la verification de signature"
}

Write-Host ""
Write-Host "[OK] EPFOptimizerPro.exe signe et verifie avec succes." -ForegroundColor Green
Write-Host ""

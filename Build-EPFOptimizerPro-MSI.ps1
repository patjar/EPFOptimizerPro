$ErrorActionPreference = "Stop"

# Build MSI EPF Optimizer Pro
# Preconditions:
# 1. dotnet publish already done in .\publish\win-x64
# 2. EPFOptimizerPro.exe inside publish\win-x64 is already signed
# 3. WiX CLI is available with the command: wix
# 4. The MSI will be signed at the end. Password is requested, not stored.

$ProjectRoot = "C:\Users\pkjn\Documents\EPFOptimizerPro-Clean"
$PublishDir = Join-Path $ProjectRoot "publish\win-x64"
$BuildDir = Join-Path $ProjectRoot "installer-build"
$OutputDir = Join-Path $ProjectRoot "dist"
$ProductName = "EPF Optimizer Pro"
$Manufacturer = "EPF"
$Version = "3.9.35.0"
$UpgradeCode = "0db39f56-f090-4e32-9263-8c78fb1b49f4"
$MsiFile = Join-Path $OutputDir "EPFOptimizerPro-Setup-v3.9.35.msi"
$WxsFile = Join-Path $BuildDir "EPFOptimizerPro.wxs"
$SignTool = "C:\Program Files (x86)\Windows Kits\10\Tools\bin\i386\signtool.exe"
$CertFile = "C:\Certificats\RADIUSServerCertificate.p12"

Write-Host ""
Write-Host "=== Build MSI EPF Optimizer Pro ===" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $PublishDir)) {
    throw "Publish folder not found: $PublishDir"
}

$ExeFile = Join-Path $PublishDir "EPFOptimizerPro.exe"
if (-not (Test-Path $ExeFile)) {
    throw "Signed exe not found: $ExeFile"
}

$WixCmd = Get-Command wix -ErrorAction SilentlyContinue
if (-not $WixCmd) {
    throw "WiX CLI not found. Install it with: dotnet tool install --global wix"
}

if (-not (Test-Path $SignTool)) {
    throw "SignTool not found: $SignTool"
}

if (-not (Test-Path $CertFile)) {
    throw "Certificate not found: $CertFile"
}

Remove-Item $BuildDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $BuildDir -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$Files = Get-ChildItem $PublishDir -File -Recurse | Sort-Object FullName
if ($Files.Count -eq 0) {
    throw "No files found in publish folder: $PublishDir"
}

$Components = New-Object System.Collections.Generic.List[string]
$Index = 1
foreach ($File in $Files) {
    $Relative = $File.FullName.Substring($PublishDir.Length).TrimStart('\')
    if ($Relative -like "*\*") {
        throw "Subfolder detected in publish output: $Relative. This simple MSI script expects a flat publish folder."
    }

    $ComponentId = "cmp" + $Index.ToString("0000")
    $FileId = "fil" + $Index.ToString("0000")
    $Source = [System.Security.SecurityElement]::Escape($File.FullName)
    $Name = [System.Security.SecurityElement]::Escape($File.Name)

    $Components.Add("      <Component Id=`"$ComponentId`" Guid=`"*`">")
    $Components.Add("        <File Id=`"$FileId`" Name=`"$Name`" Source=`"$Source`" KeyPath=`"yes`" />")
    $Components.Add("      </Component>")
    $Index++
}

$ComponentXml = $Components -join [Environment]::NewLine

$Wxs = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package
    Name="$ProductName"
    Manufacturer="$Manufacturer"
    Version="$Version"
    UpgradeCode="$UpgradeCode"
    Scope="perMachine">

    <MajorUpgrade DowngradeErrorMessage="A newer version of EPF Optimizer Pro is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <Feature Id="MainFeature" Title="$ProductName" Level="1">
      <ComponentGroupRef Id="AppFiles" />
    </Feature>

    <StandardDirectory Id="ProgramFilesFolder">
      <Directory Id="INSTALLFOLDER" Name="EPF Optimizer Pro" />
    </StandardDirectory>

    <ComponentGroup Id="AppFiles" Directory="INSTALLFOLDER">
$ComponentXml
    </ComponentGroup>
  </Package>
</Wix>
"@

Set-Content -Path $WxsFile -Value $Wxs -Encoding UTF8

Write-Host "WiX source: $WxsFile"
Write-Host "MSI output: $MsiFile"
Write-Host ""

& wix build $WxsFile -arch x64 -out $MsiFile
if ($LASTEXITCODE -ne 0) {
    throw "MSI build failed"
}

if (-not (Test-Path $MsiFile)) {
    throw "MSI was not created: $MsiFile"
}

Write-Host ""
Write-Host "MSI created." -ForegroundColor Green
Write-Host "Signing MSI..." -ForegroundColor Cyan
Write-Host "Password will be requested now. It is not stored." -ForegroundColor Yellow
Write-Host ""

$SecurePassword = Read-Host "Certificate password" -AsSecureString
$Bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecurePassword)
$PlainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($Bstr)

try {
    & $SignTool sign /f $CertFile /p $PlainPassword /fd SHA256 /tr "http://timestamp.digicert.com" /td SHA256 /v $MsiFile
    if ($LASTEXITCODE -ne 0) {
        throw "MSI signing failed"
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
Write-Host "Verifying MSI signature..." -ForegroundColor Cyan
Write-Host ""

& $SignTool verify /pa /v $MsiFile
if ($LASTEXITCODE -ne 0) {
    throw "MSI signature verification failed"
}

Write-Host ""
Write-Host "[OK] MSI built, signed and verified." -ForegroundColor Green
Write-Host $MsiFile -ForegroundColor Green
Write-Host ""

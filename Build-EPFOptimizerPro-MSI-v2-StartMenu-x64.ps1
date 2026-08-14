$ErrorActionPreference = "Stop"

# Build MSI EPF Optimizer Pro v3 - autonomous publish
# Correctifs MSI :
# - installation dans C:\Program Files\EPF Optimizer Pro pour x64
# - raccourci menu Demarrer pour que Windows trouve l'application
# - InstallLocation renseigne dans Programmes et fonctionnalites
# - signature du MSI avec demande de mot de passe, sans stockage
# - dotnet publish automatique avant generation WiX
# - WiX source generated under obj to keep Git working tree clean

$ProjectRoot = "C:\Users\pkjn\Documents\EPFOptimizerPro-Clean"
$PublishDir = Join-Path $ProjectRoot "publish\win-x64"
$BuildDir = Join-Path $ProjectRoot "obj\installer-build"
$OutputDir = Join-Path $ProjectRoot "dist"
$ProductName = "EPF Optimizer Pro"
$Manufacturer = "EPF"
$UpgradeCode = "0db39f56-f090-4e32-9263-8c78fb1b49f4"
$SignToolCandidates = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\*\x86\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\Tools\bin\x64\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\Tools\bin\x86\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\Tools\bin\i386\signtool.exe"
)
$SignTool = Get-ChildItem -Path $SignToolCandidates -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch "\\arm64\\" } |
    Sort-Object @{Expression={ if ($_.FullName -match "\\x64\\") {0} elseif ($_.FullName -match "\\x86\\|\\i386\\") {1} else {2} }}, LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $SignTool) { $SignTool = "" }
$CertFile = "C:\Certificats\RADIUSServerCertificate.p12"

Write-Host ""
Write-Host "=== Build MSI EPF Optimizer Pro v2 ===" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $ProjectRoot)) { throw "Dossier projet introuvable : $ProjectRoot" }
Set-Location $ProjectRoot
# AUTO-PUBLISH-BEGIN
$Csproj = Join-Path $ProjectRoot "EPFOptimizerPro.csproj"
if (-not (Test-Path $Csproj)) { throw "Projet introuvable : $Csproj" }

Write-Host "Publication Release win-x64..." -ForegroundColor Cyan
Remove-Item (Join-Path $ProjectRoot "bin") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $ProjectRoot "obj") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
& dotnet publish $Csproj -c Release -r win-x64 --self-contained true -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish echoue" }

# Current WiX generation expects a flat publish folder. Remove resource subfolders.
Get-ChildItem $PublishDir -Directory -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
# AUTO-PUBLISH-END

if (-not (Test-Path $PublishDir)) { throw "Publish folder introuvable : $PublishDir" }
$ExeFile = Join-Path $PublishDir "EPFOptimizerPro.exe"
if (-not (Test-Path $ExeFile)) { throw "Executable introuvable : $ExeFile" }

$Csproj = Join-Path $ProjectRoot "EPFOptimizerPro.csproj"
if (-not (Test-Path $Csproj)) { throw "Projet introuvable : $Csproj" }

[xml]$ProjectXml = Get-Content $Csproj
$Version = $ProjectXml.Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = $ProjectXml.Project.PropertyGroup.AssemblyVersion | Select-Object -First 1 }
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = "3.9.35.0" }

$ShortVersion = $Version
if ($ShortVersion.EndsWith(".0")) { $ShortVersion = $ShortVersion.Substring(0, $ShortVersion.Length - 2) }

$MsiFile = Join-Path $OutputDir "EPFOptimizerPro-Setup-v$ShortVersion.msi"
$WxsFile = Join-Path $BuildDir "EPFOptimizerPro.wxs"

$WixCmd = Get-Command wix -ErrorAction SilentlyContinue
if (-not $WixCmd) { throw "WiX CLI introuvable. Installer avec : dotnet tool install --global wix" }
if (-not (Test-Path $SignTool)) { throw "SignTool introuvable : $SignTool" }
if (-not (Test-Path $CertFile)) { throw "Certificat introuvable : $CertFile" }

Remove-Item $BuildDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $BuildDir -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$Files = Get-ChildItem $PublishDir -File -Recurse | Sort-Object FullName
if ($Files.Count -eq 0) { throw "Aucun fichier trouve dans : $PublishDir" }

$Components = New-Object System.Collections.Generic.List[string]
$Index = 1
foreach ($File in $Files) {
    $Relative = $File.FullName.Substring($PublishDir.Length).TrimStart('\')
    if ($Relative -like "*\*") {
        throw "Sous-dossier detecte dans publish : $Relative. Ce script attend un publish plat."
    }

    $ComponentId = "cmp" + $Index.ToString("0000")
    $FileId = "fil" + $Index.ToString("0000")
    $Source = [System.Security.SecurityElement]::Escape($File.FullName)
    $Name = [System.Security.SecurityElement]::Escape($File.Name)

    $Components.Add("      <Component Id=`"$ComponentId`" Guid=`"*`" Bitness=`"always64`">")
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

    <MajorUpgrade DowngradeErrorMessage="Une version plus recente de EPF Optimizer Pro est deja installee." />
    <MediaTemplate EmbedCab="yes" />


    <Feature Id="MainFeature" Title="$ProductName" Level="1">
      <ComponentGroupRef Id="AppFiles" />
      <ComponentRef Id="StartMenuShortcutComponent" />
    </Feature>

    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="EPF Optimizer Pro" />
    </StandardDirectory>

    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="EPF Optimizer Pro" />
    </StandardDirectory>

    <ComponentGroup Id="AppFiles" Directory="INSTALLFOLDER">
$ComponentXml
    </ComponentGroup>

    <Component Id="StartMenuShortcutComponent" Directory="ApplicationProgramsFolder" Guid="*" Bitness="always64">
      <Shortcut Id="StartMenuShortcut"
                Name="EPF Optimizer Pro"
                Description="EPF Optimizer Pro"
                Target="[INSTALLFOLDER]EPFOptimizerPro.exe"
                WorkingDirectory="INSTALLFOLDER" />
      <RemoveFolder Id="RemoveApplicationProgramsFolder" On="uninstall" />
      <RegistryValue Root="HKLM"
                     Key="Software\EPF\EPFOptimizerPro"
                     Name="StartMenuShortcut"
                     Type="integer"
                     Value="1"
                     KeyPath="yes" />
    </Component>
  </Package>
</Wix>
"@

Set-Content -Path $WxsFile -Value $Wxs -Encoding UTF8

Write-Host "Version MSI : $Version"
Write-Host "WiX source  : $WxsFile"
Write-Host "MSI output  : $MsiFile"
Write-Host ""

& wix build $WxsFile -arch x64 -out $MsiFile
if ($LASTEXITCODE -ne 0) { throw "Build MSI echoue" }
if (-not (Test-Path $MsiFile)) { throw "MSI non cree : $MsiFile" }

Write-Host "MSI cree. Signature..." -ForegroundColor Green
Write-Host "Mot de passe demande maintenant. Il n'est pas stocke." -ForegroundColor Yellow

$SecurePassword = Read-Host "Mot de passe certificat" -AsSecureString
$Bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecurePassword)
$PlainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($Bstr)

try {
    & $SignTool sign /f $CertFile /p $PlainPassword /fd SHA256 /tr "http://timestamp.digicert.com" /td SHA256 /v $MsiFile
    if ($LASTEXITCODE -ne 0) { throw "Signature MSI echouee" }
}
finally {
    if ($Bstr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($Bstr) }
    Remove-Variable PlainPassword -ErrorAction SilentlyContinue
    Remove-Variable SecurePassword -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Verification signature MSI..." -ForegroundColor Cyan
& $SignTool verify /pa /v $MsiFile
if ($LASTEXITCODE -ne 0) { throw "Verification signature MSI echouee" }

Write-Host ""
Write-Host "[OK] MSI x64 cree, signe et verifie." -ForegroundColor Green
Write-Host $MsiFile -ForegroundColor Green
Write-Host ""
Write-Host "Ce MSI doit installer dans : C:\Program Files\EPF Optimizer Pro" -ForegroundColor Yellow
Write-Host "Et creer un raccourci menu Demarrer : EPF Optimizer Pro" -ForegroundColor Yellow
Write-Host ""

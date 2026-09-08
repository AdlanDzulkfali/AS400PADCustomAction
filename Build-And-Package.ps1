<#
.SYNOPSIS
    Builds, packages, and signs the Power Automate Desktop (PAD) AS400PADCustomAction module.

.DESCRIPTION
    1. Compiles AS400PADCustomAction in Release configuration.
    2. Packages the output DLLs into a cabinet (.cab) file using makecab.exe.
    3. Signs both the DLLs and the .cab file using signtool.exe (offline SHA256, no external timestamping delay).

.PARAMETER Configuration
    Build configuration (default: Release).

.PARAMETER CertificatePath
    Path to an existing code-signing .pfx certificate.

.PARAMETER CertificatePassword
    Password for the .pfx certificate.

.PARAMETER CreateSelfSignedCert
    If specified, automatically generates a self-signed code-signing certificate (PFX & CER)
    and uses it to sign the DLLs and CAB file instantly offline.

.PARAMETER OutputDirectory
    Target directory for the packaged .cab and artifacts (default: .\dist).

.EXAMPLE
    .\Build-And-Package.ps1 -CreateSelfSignedCert
    Compiles, packages, generates test certificates, and signs the CAB instantly offline.

.EXAMPLE
    .\Build-And-Package.ps1 -CertificatePath "C:\Certs\enterprise_codesign.pfx" -CertificatePassword "Secret123!"
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Configuration = "Release",

    [Parameter()]
    [string]$CertificatePath,

    [Parameter()]
    [string]$CertificatePassword,

    [Parameter()]
    [switch]$CreateSelfSignedCert,

    [Parameter()]
    [string]$OutputDirectory = "dist"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Power Automate Desktop - AS400PADCustomAction Packager  " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ScriptDir "AS400PADCustomAction.csproj"
$ResolvedOutputDir = Join-Path $ScriptDir $OutputDirectory

# Ensure output directory exists
if (-not (Test-Path $ResolvedOutputDir)) {
    New-Item -ItemType Directory -Path $ResolvedOutputDir -Force | Out-Null
}

# ---------------------------------------------------------
# Step 1: Build the project
# ---------------------------------------------------------
Write-Host "`n[1/4] Compiling project in $Configuration configuration..." -ForegroundColor Yellow

& dotnet build $ProjectFile -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

$BinDir = Join-Path $ScriptDir "bin\$Configuration\net472"
$MainDll = Join-Path $BinDir "Modules.AS400PADCustomAction.dll"
$SdkDll = Join-Path $BinDir "Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.dll"

if (-not (Test-Path $MainDll)) {
    throw "Expected assembly was not found at '$MainDll'"
}

Write-Host "Build succeeded: $MainDll" -ForegroundColor Green

# ---------------------------------------------------------
# Step 2: Locate signtool.exe (fast direct path lookup)
# ---------------------------------------------------------
Write-Host "`n[2/4] Locating signtool.exe..." -ForegroundColor Yellow

$SignToolPath = $null
$knownPaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x86\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\signtool.exe"
)
foreach ($p in $knownPaths) {
    if (Test-Path $p) {
        $SignToolPath = $p
        break
    }
}

if (-not $SignToolPath) {
    $commandSignTool = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if ($commandSignTool) { $SignToolPath = $commandSignTool.Source }
}

if ($SignToolPath) {
    Write-Host "Found signtool: $SignToolPath" -ForegroundColor Green
} else {
    Write-Host "WARNING: signtool.exe not found on PATH or standard Windows Kits locations." -ForegroundColor DarkYellow
}

# ---------------------------------------------------------
# Step 3: Handle Code Signing Certificate
# ---------------------------------------------------------
$PfxPath = $CertificatePath
$PfxPass = $CertificatePassword

if ($CreateSelfSignedCert) {
    $PfxPath = Join-Path $ResolvedOutputDir "AS400PADCustomAction_TestCert.pfx"
    $CerPath = Join-Path $ResolvedOutputDir "AS400PADCustomAction_TestCert.cer"
    $certPass = "AS400PAD_Pass_2026!"
    $PfxPass = $certPass

    if (Test-Path $PfxPath) {
        Write-Host "`nReusing existing test code-signing certificate:" -ForegroundColor Cyan
        Write-Host "  PFX: $PfxPath"
    } else {
        Write-Host "`nGenerating self-signed test code-signing certificate (instant offline)..." -ForegroundColor Cyan
        $certSubject = "CN=AS400PADCustomAction-Dev"
        $securePass = ConvertTo-SecureString -String $certPass -AsPlainText -Force

        # Create Code-Signing Certificate in CurrentUser\My
        $cert = New-SelfSignedCertificate -Subject $certSubject `
            -KeyAlgorithm RSA `
            -KeyLength 2048 `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -Type CodeSigningCert `
            -KeyExportPolicy Exportable `
            -NotAfter (Get-Date).AddYears(3)

        # Export to PFX & CER
        Export-PfxCertificate -Cert $cert -FilePath $PfxPath -Password $securePass -Force | Out-Null
        Export-Certificate -Cert $cert -FilePath $CerPath -Force | Out-Null

        Write-Host "Self-signed certificate generated:" -ForegroundColor Green
        Write-Host "  Thumbprint: $($cert.Thumbprint)"
        Write-Host "  PFX Export: $PfxPath"
        Write-Host "  CER Export: $CerPath"
        Write-Host "  Password:   $certPass"
    }
}

# Sign DLLs before packaging if certificate and signtool are available (Fast offline SHA256)
if ($SignToolPath -and $PfxPath -and (Test-Path $PfxPath)) {
    Write-Host "`nSigning assembly DLLs (offline SHA256, no timestamping delay)..." -ForegroundColor Cyan
    $dllsToSign = @($MainDll)
    if (Test-Path $SdkDll) { $dllsToSign += $SdkDll }

    foreach ($dll in $dllsToSign) {
        Write-Host "  Signing: $(Split-Path -Leaf $dll)"
        & $SignToolPath sign /f $PfxPath /p $PfxPass /fd SHA256 $dll
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to sign $dll with signtool exit code $LASTEXITCODE"
        }
    }
}

# ---------------------------------------------------------
# Step 4: Package into .CAB with makecab.exe
# ---------------------------------------------------------
Write-Host "`n[3/4] Packaging custom action module into CAB archive..." -ForegroundColor Yellow

$CabFileName = "Modules.AS400PADCustomAction.cab"
$FinalCabPath = Join-Path $ResolvedOutputDir $CabFileName
$DdfPath = Join-Path $ResolvedOutputDir "package.ddf"

# Generate Diamond Directive File (DDF)
$ddfContent = @"
.OPTION EXPLICIT
.Set CabinetNameTemplate=$CabFileName
.Set DiskDirectory1=$ResolvedOutputDir
.Set CompressionType=LZX
.Set Cabinet=on
.Set Compress=on
"$MainDll" "Modules.AS400PADCustomAction.dll"
"@

if (Test-Path $SdkDll) {
    $ddfContent += "`n`"$SdkDll`" `"Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.dll`""
}

Set-Content -Path $DdfPath -Value $ddfContent -Encoding ASCII

# Run makecab.exe directly
& makecab.exe /F $DdfPath
if ($LASTEXITCODE -ne 0) {
    throw "makecab.exe failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $FinalCabPath)) {
    throw "CAB generation failed; '$FinalCabPath' not found."
}

Write-Host "Cabinet created: $FinalCabPath" -ForegroundColor Green

# ---------------------------------------------------------
# Step 5: Sign the final .CAB file (offline SHA256)
# ---------------------------------------------------------
Write-Host "`n[4/4] Finalizing and signing CAB package..." -ForegroundColor Yellow

if ($SignToolPath -and $PfxPath -and (Test-Path $PfxPath)) {
    Write-Host "Signing CAB file: $FinalCabPath" -ForegroundColor Cyan
    & $SignToolPath sign /f $PfxPath /p $PfxPass /fd SHA256 $FinalCabPath
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to sign $FinalCabPath with signtool exit code $LASTEXITCODE"
    }

    Write-Host "`nVerifying signature on CAB:" -ForegroundColor Cyan
    & $SignToolPath verify /v $FinalCabPath
} else {
    Write-Host "`nCAB file generated without digital signature." -ForegroundColor DarkYellow
    Write-Host "To sign with an enterprise code-signing certificate, run:" -ForegroundColor Gray
    Write-Host "  signtool.exe sign /f `"<YourCert.pfx>`" /p `"<Password>`" /fd SHA256 `"$FinalCabPath`"" -ForegroundColor White
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  PACKAGE READY FOR POWER AUTOMATE PORTAL IMPORT:        " -ForegroundColor Green
Write-Host "  CAB Archive: $FinalCabPath" -ForegroundColor White
if ($CreateSelfSignedCert) {
    Write-Host "  Test PFX:    $PfxPath" -ForegroundColor White
    Write-Host "  Test CER:    $CerPath" -ForegroundColor White
    Write-Host "`nTo install the test cert into your local Trusted Root store:" -ForegroundColor Yellow
    Write-Host "  certutil -addstore -user Root `"$CerPath`"" -ForegroundColor Gray
}
Write-Host "==========================================================" -ForegroundColor Green

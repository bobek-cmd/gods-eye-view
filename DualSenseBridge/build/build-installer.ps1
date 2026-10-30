<#
.SYNOPSIS
  Builds DualSense Bridge and packages it into a single setup EXE.

.DESCRIPTION
  1. Runs the unit tests.
  2. Publishes the app self-contained for win-x64 (no .NET install needed on the target PC).
  3. Downloads the official ViGEmBus and HidHide installers (bundled into the setup).
  4. Compiles installer\DualSenseBridge.iss with Inno Setup 6.

  Output: artifacts\installer\DualSenseBridge-Setup-<version>.exe

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File build\build-installer.ps1
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File build\build-installer.ps1 -Version 1.0.1 -SkipTests
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [switch]$SkipTests,
    [switch]$SkipDrivers
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "artifacts\publish"
$redistDir = Join-Path $root "installer\redist"
$issFile = Join-Path $root "installer\DualSenseBridge.iss"

# Official, signed driver installers (Nefarius Software Solutions).
$drivers = @(
    @{
        Name   = "ViGEmBus_Setup.exe"
        Url    = "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe"
        Sha256 = "89220A7865076B342892F98865F3499FB7C4CFD673159E89D352C360FD014C6A"
    },
    @{
        Name   = "HidHide_Setup.exe"
        Url    = "https://github.com/nefarius/HidHide/releases/download/v1.5.230.0/HidHide_1.5.230_x64.exe"
        Sha256 = "F4BBBCB82E6258641B887C74BC81C4C5F66E4AA811808DFC304347687B7605F6"
    }
)

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 8 SDK nije pronađen. Instalirajte ga: winget install Microsoft.DotNet.SDK.8"
}

if (-not $SkipTests) {
    Step "Pokretanje testova"
    dotnet test (Join-Path $root "tests\DualSenseBridge.Tests") -c Release
    if ($LASTEXITCODE -ne 0) { throw "Testovi nisu prošli." }
}

Step "Objavljivanje aplikacije (self-contained, win-x64)"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $root "src\DualSenseBridge\DualSenseBridge.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:PublishReadyToRun=true `
    -p:Version=$Version `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish nije uspio." }

if (-not $SkipDrivers) {
    Step "Preuzimanje instalacija drivera"
    New-Item -ItemType Directory -Force -Path $redistDir | Out-Null
    foreach ($driver in $drivers) {
        $target = Join-Path $redistDir $driver.Name
        if ((Test-Path $target) -and (Get-FileHash $target -Algorithm SHA256).Hash -eq $driver.Sha256) {
            Write-Host "  $($driver.Name) već postoji"
            continue
        }
        Write-Host "  $($driver.Url)"
        Invoke-WebRequest -Uri $driver.Url -OutFile $target -UseBasicParsing
        $hash = (Get-FileHash $target -Algorithm SHA256).Hash
        if ($hash -ne $driver.Sha256) {
            Remove-Item $target -Force
            throw "SHA-256 za $($driver.Name) ne odgovara ($hash)."
        }
        $signature = Get-AuthenticodeSignature $target
        if ($signature.Status -ne "Valid") {
            Remove-Item $target -Force
            throw "Digitalni potpis za $($driver.Name) nije valjan ($($signature.Status))."
        }
    }
}

Step "Izrada instalacije (Inno Setup)"
$iscc = Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $iscc) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) {
    throw "Inno Setup 6 nije pronađen. Instalirajte ga: winget install JRSoftware.InnoSetup"
}

& $iscc "/DMyAppVersion=$Version" "/DPublishDir=$publishDir" $issFile
if ($LASTEXITCODE -ne 0) { throw "ISCC nije uspio." }

$setup = Join-Path $root "artifacts\installer\DualSenseBridge-Setup-$Version.exe"
Write-Host "`nGotovo: $setup" -ForegroundColor Green

#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes PentaGrammata and builds the NSIS Windows installer.

.PARAMETER Version
    Version string to embed in the installer (e.g. "1.2.0").
    Defaults to the value in version.txt at the repository root.

.PARAMETER Runtime
    .NET runtime identifier to publish for. Defaults to "win-x64".

.PARAMETER SkipPublish
    Skip the dotnet publish step (use an existing publish output).

.EXAMPLE
    .\Build-Installer.ps1
    .\Build-Installer.ps1 -Version "1.2.0"
    .\Build-Installer.ps1 -Version "1.2.0" -SkipPublish
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $Runtime     = "win-x64",
    [switch] $SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Paths (all relative to the repository root, one level above this script)
# ---------------------------------------------------------------------------
$RepoRoot    = (Resolve-Path "$PSScriptRoot\..").Path
$ProjectFile = Join-Path $RepoRoot "src\PentaGrammata.csproj"
$NsiFile     = Join-Path $RepoRoot "installer\nsis\PentaGrammata.nsi"
$PublishDir  = Join-Path $RepoRoot "publish\$Runtime"
$OutputDir   = Join-Path $RepoRoot "installer\nsis"
$VersionFile = Join-Path $RepoRoot "version.txt"

# ---------------------------------------------------------------------------
# Resolve version
# ---------------------------------------------------------------------------
if (-not $Version) {
    if (-not (Test-Path $VersionFile)) {
        Write-Error "version.txt not found at '$VersionFile'. Create it or pass -Version explicitly."
    }
    $Version = (Get-Content $VersionFile -Raw).Trim()
    if (-not $Version) {
        Write-Error "version.txt is empty. Add a version string (e.g. 1.0.0) and try again."
    }
}
Write-Host ""
Write-Host "==> Version: $Version" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# Validate tools
# ---------------------------------------------------------------------------
function Resolve-Tool {
    param([string]$Name)
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Resolve-Makensis {
    # 1. Try PATH first
    $onPath = Resolve-Tool "makensis"
    if ($onPath) { return $onPath }

    # 2. Common default NSIS install locations
    $candidates = @(
        "$env:ProgramFiles\NSIS\makensis.exe",
        "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
        "$env:ProgramFiles\NSIS\Unicode\makensis.exe",
        "${env:ProgramFiles(x86)}\NSIS\Unicode\makensis.exe"
    )
    foreach ($path in $candidates) {
        if (Test-Path $path) { return $path }
    }

    Write-Error @"
'makensis' was not found on PATH or in the default NSIS install directories.
Please install NSIS from https://nsis.sourceforge.io/Download and try again,
or add its directory to your PATH.
"@
}

$DotnetExe   = Resolve-Tool "dotnet"
if (-not $DotnetExe) {
    Write-Error "'dotnet' was not found on PATH. Please install the .NET SDK and try again."
}
$MakensisExe = Resolve-Makensis

# ---------------------------------------------------------------------------
# Publish the application
# ---------------------------------------------------------------------------
if (-not $SkipPublish) {
    Write-Host ""
    Write-Host "==> Publishing $Runtime ..." -ForegroundColor Cyan

    $publishArgs = @(
        "publish", $ProjectFile,
        "-c", "Release",
        "-r", $Runtime,
        "--self-contained", "true",
        "-p:Version=$Version",
        "-p:AssemblyVersion=$Version",
        "-p:FileVersion=$Version",
        "-o", $PublishDir
    )

    & $DotnetExe @publishArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
    }

    Write-Host "    Published to: $PublishDir" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "==> Skipping publish (using existing output in $PublishDir)" -ForegroundColor Yellow
    if (-not (Test-Path $PublishDir)) {
        Write-Error "Publish directory not found: $PublishDir"
    }
}

# ---------------------------------------------------------------------------
# Build NSIS installer
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "==> Running NSIS ..." -ForegroundColor Cyan

$nsisArgs = @(
    # -D works on both Windows and Linux makensis (/D is Windows-only).
    "-DAPP_VERSION=$Version",
    $NsiFile
)

Write-Host "    Using APP_VERSION=$Version" -ForegroundColor Yellow

Push-Location $OutputDir
try {
    & $MakensisExe @nsisArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "makensis failed with exit code $LASTEXITCODE."
    }
} finally {
    Pop-Location
}

# ---------------------------------------------------------------------------
# Report output
# ---------------------------------------------------------------------------
$EffectiveVersion = $Version

$InstallerFile = Join-Path $OutputDir "PentaGrammata-$EffectiveVersion-$Runtime-setup.exe"

Write-Host ""
if (Test-Path $InstallerFile) {
    $size = [math]::Round((Get-Item $InstallerFile).Length / 1MB, 2)
    Write-Host "==> Installer created successfully!" -ForegroundColor Green
    Write-Host "    $InstallerFile ($size MB)" -ForegroundColor Green
} else {
    Write-Warning "Build completed but installer file not found at expected path:"
    Write-Warning "    $InstallerFile"
    Write-Warning "Check the NSIS output above for the actual filename."
}
Write-Host ""

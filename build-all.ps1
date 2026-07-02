<#
.SYNOPSIS
    Publishes every Hosts File Editor flavor/architecture combination in one command.
.DESCRIPTION
    Publishes classic (WinForms) and modern (WinUI) for both win-x64 and win-arm64.
    Each combination is packaged (via Directory.Build.targets' SignAndPackage target) into
    its own artifacts\<flavor>\<arch>\ folder, then the resulting .msix files are copied into
    artifacts\store\ with descriptive names ready to upload to Partner Center.
.PARAMETER Configuration
    Build configuration to publish. Default: Release.
.PARAMETER Sign
    Enable Authenticode signing of the exe/helper (-p:EnableSigning=true). Default is
    unsigned, since the Microsoft Store re-signs packages on ingestion anyway. Combine with
    -ExtraArgs to pass signing properties, e.g. -p:SigningCertThumbprint=... .
.PARAMETER ExtraArgs
    Additional arguments passed through to every dotnet publish invocation.
.EXAMPLE
    .\build-all.ps1
.EXAMPLE
    .\build-all.ps1 -Sign -ExtraArgs "-p:SigningCertThumbprint=ABCD1234"
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$Sign,
    [string[]]$ExtraArgs = @()
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$enableSigning = if ($Sign) { "true" } else { "false" }

$targets = @(
    @{ Project = "HostsFileEditor.WinForm\HostsFileEditor.WinForm.csproj"; Flavor = "classic"; Rid = "win-x64";   Platform = $null }
    @{ Project = "HostsFileEditor.WinForm\HostsFileEditor.WinForm.csproj"; Flavor = "classic"; Rid = "win-arm64"; Platform = $null }
    @{ Project = "HostsFileEditor.WinUI\HostsFileEditor.WinUI.csproj";     Flavor = "modern";  Rid = "win-x64";   Platform = "x64" }
    @{ Project = "HostsFileEditor.WinUI\HostsFileEditor.WinUI.csproj";     Flavor = "modern";  Rid = "win-arm64"; Platform = "arm64" }
)

foreach ($t in $targets) {
    $arch = $t.Rid -replace '^win-', ''
    Write-Host "==> Publishing $($t.Flavor) / $arch ($($t.Rid))" -ForegroundColor Cyan

    $publishArgs = @(
        "publish"
        (Join-Path $repoRoot $t.Project)
        "-c", $Configuration
        "-r", $t.Rid
        "-p:EnableSigning=$enableSigning"
    )
    if ($t.Platform) {
        $publishArgs += "-p:Platform=$($t.Platform)"
    }
    $publishArgs += $ExtraArgs

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $($t.Flavor) / $arch"
    }
}

# Collect the resulting MSIX packages into artifacts\store\ with descriptive names for upload.
$storeDir = Join-Path $repoRoot "artifacts\store"
New-Item -ItemType Directory -Force -Path $storeDir | Out-Null

Write-Host "`n==> Collecting Store packages" -ForegroundColor Cyan
foreach ($t in $targets) {
    $arch = $t.Rid -replace '^win-', ''
    $src = Join-Path $repoRoot "artifacts\$($t.Flavor)\$arch\HostsFileEditor.msix"
    $dst = Join-Path $storeDir "HostsFileEditor-$($t.Flavor)-$arch.msix"
    if (Test-Path $src) {
        Copy-Item $src $dst -Force
        Write-Host "  -> $dst" -ForegroundColor Green
    }
    else {
        Write-Warning "Expected package not found: $src"
    }
}

Write-Host "`nDone. Per-arch app output: artifacts\classic\<arch>\, artifacts\modern\<arch>\. Store uploads: artifacts\store\." -ForegroundColor Green

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

# When signing, auto-resolve the Azure Trusted Signing dlib from the NuGet global-packages
# cache (Microsoft.Trusted.Signing.Client) unless the caller supplied one. The metadata (/dmdf)
# and timestamp default in Directory.Build.targets. See docs/signing.md.
if ($Sign -and -not ($ExtraArgs -join ' ' -match 'SigningDlib')) {
    $nugetRoot = (dotnet nuget locals global-packages --list) -replace '.*global-packages:\s*', ''
    # Pick the x64 dlib to match the x64 signtool the build resolves; the x86 one fails to load.
    $dlib = Get-ChildItem -Path (Join-Path $nugetRoot 'microsoft.trusted.signing.client') `
        -Recurse -Filter 'Azure.CodeSigning.Dlib.dll' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object FullName -Descending | Select-Object -First 1
    if ($dlib) {
        Write-Host "Using Trusted Signing dlib: $($dlib.FullName)" -ForegroundColor DarkGray
        $ExtraArgs += "-p:SigningDlib=$($dlib.FullName)"
    }
    else {
        Write-Warning "Trusted Signing dlib not found in the NuGet cache. Restore it first (see docs/signing.md), or pass -p:SigningDlib=<path>."
    }
}

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

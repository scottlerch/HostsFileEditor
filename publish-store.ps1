<#
.SYNOPSIS
    Automate the Microsoft Store submissions for both Hosts File Editor editions using the
    Microsoft Store Developer CLI (msstore), instead of the Partner Center portal UI.

.DESCRIPTION
    Run AFTER `build-all.ps1 -Sign` has produced artifacts\store\*.msix. For each edition this:
      1. stages that edition's x64+arm64 packages and uploads them into a DRAFT submission
         (`msstore publish -i <dir> --noCommit`),
      2. patches the draft's "What's new" notes (every listing locale),
      3. commits (`submission publish`) and polls — unless -NoCommit.

    FIRST-TIME setup (see CLAUDE.md > Store release automation for the full walkthrough):
      .\publish-store.ps1 -InstallTooling      # winget-installs the .NET 9 runtime + msstore CLI
      msstore                                  # interactive first-run: sign in with your ENTRA ID
                                               #   account (NOT your MSA); resolves your Seller Id.
      msstore info                             # confirm the config

    RECOMMENDED first real run: use -NoCommit and then review the Draft in Partner Center - in
    particular confirm BOTH architectures (x64 + arm64) uploaded at the new version. `msstore publish`
    takes a directory; if it only picks up one package, tell the maintainer (fallback: a .msixbundle).

.EXAMPLE
    .\publish-store.ps1 -InstallTooling
.EXAMPLE
    .\build-all.ps1 -Sign; .\publish-store.ps1 -NoCommit     # build+sign, stage drafts, review in portal
.EXAMPLE
    .\publish-store.ps1 -Edition modern                      # publish only the modern edition, commit
#>
[CmdletBinding()]
param(
    [switch]$InstallTooling,                    # winget-install the .NET 9 runtime + msstore CLI, then exit
    [switch]$Inspect,                           # dump the current submission JSON and exit
    [switch]$NoCommit,                          # upload + set notes into a Draft, but DON'T publish
    [ValidateSet('classic', 'modern', 'both')]
    [string]$Edition = 'both',
    [string]$ClassicProductId = '9NF73PSPK332',   # https://apps.microsoft.com/detail/9NF73PSPK332
    [string]$ModernProductId  = '9NBQWCDXGF9R'    # https://apps.microsoft.com/detail/9NBQWCDXGF9R
)
$ErrorActionPreference = 'Stop'
$storeDir = Join-Path $PSScriptRoot 'artifacts\store'

# ---- Per-release "What's new" copy (edit each release) ----
$notes = @{
    classic = @'
Performance and reliability update:
- Much faster on very large hosts files - async load, ~3x faster parser, and bulk edits that used to freeze now finish in milliseconds.
- Smaller, faster install.
- Fixed a crash when sorting by a column header; fixed a Select-All/Delete data-loss case.
- Tailscale/MagicDNS names ending in a dot (host.tailnet.ts.net.) now parse correctly.
'@
    modern = @'
Performance and polish update:
- Much faster on very large hosts files - async load, ~3x faster parser, instant bulk edits.
- New status bar (line + host-entry counts); no more column flicker; selection kept across Check/Uncheck & Duplicate.
- Tailscale/MagicDNS names ending in a dot now parse correctly.
'@
}

function Test-Tool([string]$Name) { [bool](Get-Command $Name -ErrorAction SilentlyContinue) }

function Install-Tooling {
    if (-not (Test-Tool winget)) {
        throw "winget not found. Install 'App Installer' from the Microsoft Store, then re-run; or download msstore directly from https://aka.ms/msstoredevcli/releases."
    }
    $wingetArgs = '--accept-source-agreements', '--accept-package-agreements', '--disable-interactivity'
    Write-Host '==> Installing .NET 9 Desktop Runtime (msstore prerequisite)' -ForegroundColor Cyan
    & winget install --id Microsoft.DotNet.DesktopRuntime.9 @wingetArgs
    Write-Host '==> Installing Microsoft Store Developer CLI' -ForegroundColor Cyan
    & winget install 'Microsoft Store Developer CLI' @wingetArgs
    Write-Host "Done. Open a NEW shell so 'msstore' is on PATH, then run: msstore   (sign in with your Entra ID account)." -ForegroundColor Green
}

if ($InstallTooling) { Install-Tooling; return }

if (-not (Test-Tool msstore)) {
    throw "msstore CLI not found. Run '.\publish-store.ps1 -InstallTooling' once, then 'msstore' to sign in. See CLAUDE.md > Store release automation."
}

function Publish-Edition {
    param([string]$Name, [string]$ProductId)

    if ($Inspect) { & msstore submission get $ProductId; return }

    $packages = @(Get-ChildItem (Join-Path $storeDir "HostsFileEditor-$Name-*.msix") -ErrorAction SilentlyContinue)
    if (-not $packages) { throw "No packages found: $storeDir\HostsFileEditor-$Name-*.msix (run .\build-all.ps1 -Sign first)." }
    Write-Host "==> $Name ($ProductId): $($packages.Count) package(s)" -ForegroundColor Cyan
    $packages | ForEach-Object { Write-Host "     $($_.Name)" -ForegroundColor DarkGray }

    # Stage just THIS edition's packages so `msstore publish -i <dir>` uploads only its arches
    # (artifacts\store\ also holds the other edition's packages).
    $stage = Join-Path $storeDir "_upload-$Name"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    New-Item -ItemType Directory -Path $stage | Out-Null
    $packages | Copy-Item -Destination $stage

    # 1. Upload the packages into a DRAFT (create/clone a pending submission, don't commit yet).
    Write-Host '  uploading packages into a draft submission...' -ForegroundColor DarkGray
    & msstore publish -i $stage -id $ProductId --noCommit
    if ($LASTEXITCODE) { throw "msstore publish failed for $Name (exit $LASTEXITCODE)." }

    # 2. Set this release's "What's new" on the draft, for every listing locale.
    $sub = (& msstore submission get $ProductId | Out-String) | ConvertFrom-Json -Depth 60
    foreach ($locale in @($sub.Listings.PSObject.Properties.Name)) {
        if ($sub.Listings.$locale.BaseListing) { $sub.Listings.$locale.BaseListing.ReleaseNotes = $notes[$Name] }
    }
    & msstore submission update $ProductId ($sub | ConvertTo-Json -Depth 60 -Compress)
    if ($LASTEXITCODE) { throw "msstore submission update failed for $Name (exit $LASTEXITCODE)." }

    # 3. Commit + poll (unless leaving a Draft for a manual portal review).
    if ($NoCommit) { Write-Host '  draft updated (packages + notes); NOT published - review in Partner Center.' -ForegroundColor Yellow; return }
    & msstore submission publish $ProductId
    & msstore submission poll $ProductId
}

if ($Edition -in 'classic', 'both') { Publish-Edition -Name 'classic' -ProductId $ClassicProductId }
if ($Edition -in 'modern', 'both') { Publish-Edition -Name 'modern' -ProductId $ModernProductId }
Write-Host 'Done.' -ForegroundColor Green

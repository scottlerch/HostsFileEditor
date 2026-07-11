<#
.SYNOPSIS
    Automate the Microsoft Store submissions for both Hosts File Editor editions using the
    Microsoft Store Developer CLI (msstore), instead of the Partner Center portal UI.

.DESCRIPTION
    Run AFTER `build-all.ps1 -Sign` has produced artifacts\store\*.msix. For each edition this:
      1. pulls the current submission JSON (`msstore submission get`),
      2. patches it with the freshly built packages + this release's "What's new" notes,
      3. pushes it back (`submission update`), publishes (`submission publish`), and polls.

    FIRST-TIME setup:
      .\publish-store.ps1 -InstallTooling      # winget-installs the .NET 9 runtime + msstore CLI
      msstore reconfigure --tenantId <T> --sellerId <S> --clientId <C> --clientSecret <SECRET>
      .\publish-store.ps1 -Inspect             # dump the submission JSON, then fill in the two
                                               #   marked field paths below for your account.

    See the "Store release automation" section in CLAUDE.md for the full walkthrough (creating the
    Partner Center Azure AD app to get the tenant/client/seller/secret values).

.EXAMPLE
    .\publish-store.ps1 -InstallTooling
.EXAMPLE
    .\build-all.ps1 -Sign; .\publish-store.ps1            # build+sign, then submit both editions
.EXAMPLE
    .\publish-store.ps1 -Edition modern -NoCommit         # update the modern draft, don't publish
#>
[CmdletBinding()]
param(
    [switch]$InstallTooling,                    # winget-install the .NET 9 runtime + msstore CLI, then exit
    [switch]$Inspect,                           # dump the current submission JSON and exit
    [switch]$NoCommit,                          # update the draft but DON'T publish (review in the portal)
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
    Write-Host "Done. Open a NEW shell so 'msstore' is on PATH, then run: msstore reconfigure ..." -ForegroundColor Green
}

if ($InstallTooling) { Install-Tooling; return }

if (-not (Test-Tool msstore)) {
    throw "msstore CLI not found. Run '.\publish-store.ps1 -InstallTooling' once (installs it + the .NET 9 runtime via winget), then 'msstore reconfigure ...'. See CLAUDE.md > Store release automation."
}

function Publish-Edition {
    param([string]$Name, [string]$ProductId)

    $packages = @(Get-ChildItem (Join-Path $storeDir "HostsFileEditor-$Name-*.msix") -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
    if (-not $packages) { throw "No packages found: $storeDir\HostsFileEditor-$Name-*.msix (run .\build-all.ps1 -Sign first)." }
    Write-Host "==> $Name ($ProductId): $($packages.Count) package(s)" -ForegroundColor Cyan
    $packages | ForEach-Object { Write-Host "     $_" -ForegroundColor DarkGray }

    $current = (& msstore submission get $ProductId | Out-String)
    if ($Inspect) { $current; return }

    $sub = $current | ConvertFrom-Json -Depth 50

    # --- CONFIRM THESE TWO FIELD PATHS against `-Inspect` output the first time, then uncomment ---
    #     (the exact schema — applicationPackages shape / releaseNotes location — varies per account)
    # $sub.applicationPackages = @($packages | ForEach-Object { @{ fileName = (Split-Path $_ -Leaf); fileStatus = 'PendingUpload'; localFilePath = $_ } })
    # $sub.listings.'en-us'.baseListing.releaseNotes = $notes[$Name]
    throw "Fill in the two field paths above (use -Inspect to see the JSON), then remove this line. See CLAUDE.md > Store release automation."

    $updated = $sub | ConvertTo-Json -Depth 50
    & msstore submission update $ProductId $updated
    if ($NoCommit) { Write-Host '  draft updated (not published).' -ForegroundColor Yellow; return }
    & msstore submission publish $ProductId
    & msstore submission poll $ProductId
}

if ($Edition -in 'classic', 'both') { Publish-Edition -Name 'classic' -ProductId $ClassicProductId }
if ($Edition -in 'modern', 'both') { Publish-Edition -Name 'modern' -ProductId $ModernProductId }
Write-Host 'Done.' -ForegroundColor Green

<#
.SYNOPSIS
    Automate the Microsoft Store submissions for both Hosts File Editor editions with StoreBroker
    (Microsoft's PowerShell module over the Store submission REST API), instead of the Partner
    Center portal UI.

.DESCRIPTION
    Run AFTER `build-all.ps1 -Sign` has produced artifacts\store\HostsFileEditor-<edition>-<arch>.msix.
    For each edition this:
      1. clones the app's current published submission into a fresh pending one
         (New-ApplicationSubmission -Force),
      2. swaps in this release's packages (existing -> PendingDelete, the new x64+arm64 msix ->
         PendingUpload) and sets the "What's new" notes on every listing locale,
      3. pushes the patched submission (Set-ApplicationSubmission), uploads the packages .zip
         (Set-SubmissionPackage), and commits (Complete-ApplicationSubmission) - unless -NoCommit.

    Why StoreBroker and not the msstore CLI: msstore's `publish` builds a project of a recognized
    type; it can't take our pre-built, externally-signed MSIX and push them to an existing app by id.
    StoreBroker is purpose-built for exactly that - upload prepared packages + metadata to an
    existing app over the submission API. Cloning the published submission means the rich listing
    (description, screenshots, etc.) is preserved; only the packages and release notes change.

    FIRST-TIME setup (see CLAUDE.md > Store release automation for the full walkthrough):
      .\publish-store.ps1 -InstallTooling      # installs the StoreBroker PowerShell module
      # Create an Azure AD app in Partner Center (Account settings > User management > Azure AD
      # applications, Manager role) to get the TenantId / ClientId / a client secret (a "Key").

    AUTH each run (the script calls Set-StoreBrokerAuthentication for you): supply the credentials
    via -TenantId / -ClientId / -ClientSecret, or the environment variables STOREBROKER_TENANTID /
    STOREBROKER_CLIENTID / STOREBROKER_CLIENTSECRET, otherwise you are prompted. The secret is only
    ever held as a SecureString and is never written to disk by this script. Tenant and Client ids
    are not secrets; the secret is - keep it out of source control and shell history.

    RECOMMENDED first real run: -NoCommit, then review the Draft in Partner Center - confirm BOTH
    architectures (x64 + arm64) uploaded at the new version before you commit it there.

    Each edition publishes INDEPENDENTLY: if one fails, the other still completes, a per-edition
    summary prints, and the script exits non-zero. New-ApplicationSubmission -Force means a rerun
    discards any half-built pending submission and re-clones from the published one, so retrying is
    safe - retry just the failed edition with -Edition <name>.

.EXAMPLE
    .\publish-store.ps1 -InstallTooling
.EXAMPLE
    .\build-all.ps1 -Sign; .\publish-store.ps1 -NoCommit    # build+sign, stage drafts, review in portal
.EXAMPLE
    .\publish-store.ps1 -Edition modern                     # publish only the modern edition, commit
#>
[CmdletBinding()]
param(
    [switch]$InstallTooling,                    # install the StoreBroker module, then exit
    [switch]$Inspect,                           # dump each edition's current submission JSON and exit
    [switch]$NoCommit,                          # upload packages + set notes into a Draft, but DON'T commit
    [ValidateSet('classic', 'modern', 'both')]
    [string]$Edition = 'both',
    [string]$TenantId,                          # Partner Center Azure AD tenant id (or $env:STOREBROKER_TENANTID)
    [string]$ClientId,                          # Azure AD app (client) id            (or $env:STOREBROKER_CLIENTID)
    [securestring]$ClientSecret,                # Azure AD client secret              (or $env:STOREBROKER_CLIENTSECRET)
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

function Install-Tooling {
    Write-Host '==> Installing the StoreBroker PowerShell module (CurrentUser scope)' -ForegroundColor Cyan
    Install-Module -Name StoreBroker -Scope CurrentUser -Force -AllowClobber
    $v = (Get-Module -ListAvailable StoreBroker | Sort-Object Version -Descending | Select-Object -First 1).Version
    Write-Host "Done. StoreBroker $v installed." -ForegroundColor Green
    Write-Host 'Next: set STOREBROKER_TENANTID / STOREBROKER_CLIENTID / STOREBROKER_CLIENTSECRET (or pass' -ForegroundColor Green
    Write-Host '      -TenantId / -ClientId / -ClientSecret), then run this script after build-all.ps1 -Sign.' -ForegroundColor Green
}

if ($InstallTooling) { Install-Tooling; return }

if (-not (Get-Module -ListAvailable StoreBroker)) {
    throw "StoreBroker module not found. Run '.\publish-store.ps1 -InstallTooling' once. See CLAUDE.md > Store release automation."
}
Import-Module StoreBroker -ErrorAction Stop

# StoreBroker's opt-out telemetry POSTs to App Insights fail with a 400 on this account and spew
# alarming (but entirely non-fatal) errors into the log on every API call. Turn it off.
$global:SBDisableTelemetry = $true
$global:SBSuppressTelemetryReminder = $true

# Resolve credentials (param > env var > prompt) and authenticate. The secret stays a SecureString.
function Connect-Store {
    $tenant = if ($TenantId) { $TenantId } elseif ($env:STOREBROKER_TENANTID) { $env:STOREBROKER_TENANTID } else { Read-Host 'Partner Center TenantId (GUID)' }
    $client = if ($ClientId) { $ClientId } elseif ($env:STOREBROKER_CLIENTID) { $env:STOREBROKER_CLIENTID } else { Read-Host 'Azure AD ClientId (GUID)' }
    $secret =
        if     ($ClientSecret)                    { $ClientSecret }
        elseif ($env:STOREBROKER_CLIENTSECRET)    { ConvertTo-SecureString $env:STOREBROKER_CLIENTSECRET -AsPlainText -Force }
        else                                      { Read-Host 'Azure AD client secret' -AsSecureString }
    if (-not $tenant -or -not $client -or -not $secret) { throw 'Missing TenantId / ClientId / ClientSecret for Partner Center authentication.' }
    $cred = [System.Management.Automation.PSCredential]::new($client, $secret)
    Set-StoreBrokerAuthentication -TenantId $tenant -Credential $cred | Out-Null
    Write-Host "Authenticated to Partner Center (tenant $tenant)." -ForegroundColor DarkGray
}

# Read Identity/@Version straight out of an msix (a zip) so the summary shows what's being uploaded.
function Get-MsixVersion([string]$Path) {
    try {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
        try {
            $entry = $zip.Entries | Where-Object { $_.FullName -eq 'AppxManifest.xml' } | Select-Object -First 1
            if (-not $entry) { return '?' }
            $sr = [System.IO.StreamReader]::new($entry.Open())
            try { $xml = [xml]$sr.ReadToEnd() } finally { $sr.Dispose() }
            return $xml.Package.Identity.Version
        } finally { $zip.Dispose() }
    } catch { return '?' }
}

function Get-EditionPackages([string]$Name) {
    $pkgs = @(Get-ChildItem (Join-Path $storeDir "HostsFileEditor-$Name-*.msix") -ErrorAction SilentlyContinue)
    if (-not $pkgs) { throw "No packages found: $storeDir\HostsFileEditor-$Name-*.msix (run .\build-all.ps1 -Sign first)." }
    $pkgs
}

# The Store submission API (manage.devcenter.microsoft.com, behind Azure Front Door) intermittently
# times out / returns 502-504. Retry those transient failures with linear backoff; let real errors
# (400/401/409/...) surface immediately. Every call here is safe to retry: New-ApplicationSubmission
# -Force re-clones cleanly, and Set-* / upload are idempotent (same body / same blob).
function Invoke-WithRetry {
    param([Parameter(Mandatory)][scriptblock]$Action, [string]$What = 'call', [int]$MaxAttempts = 5, [int]$BaseDelaySec = 6)
    for ($attempt = 1; ; $attempt++) {
        try { return & $Action }
        catch {
            $m = ($_.Exception.Message -split "`n")[0]
            $transient = $m -match '50[234]|OriginTimeout|Gateway|Service unavailable|timed? ?out|temporarily'
            if (-not $transient -or $attempt -ge $MaxAttempts) { throw }
            $delay = $BaseDelaySec * $attempt
            Write-Host "    $What transient failure (attempt $attempt/$MaxAttempts): $m" -ForegroundColor DarkYellow
            Write-Host "    retrying in ${delay}s..." -ForegroundColor DarkYellow
            Start-Sleep -Seconds $delay
        }
    }
}

function Publish-Edition {
    param([string]$Name, [string]$ProductId)

    $packages = Get-EditionPackages $Name
    Write-Host "==> $Name ($ProductId): $($packages.Count) package(s)" -ForegroundColor Cyan
    $packages | ForEach-Object { Write-Host "     $($_.Name)  [v$(Get-MsixVersion $_.FullName)]" -ForegroundColor DarkGray }

    # 1. Clone the current published submission into a fresh pending one. -Force first discards any
    #    half-built pending submission, so reruns re-clone cleanly from what's live.
    Write-Host '  cloning current published submission...' -ForegroundColor DarkGray
    $sub = Invoke-WithRetry -What 'clone submission' { New-ApplicationSubmission -AppId $ProductId -Force -NoStatus }
    $uploadUrl = $sub.fileUploadUrl

    # 2a. Swap packages: mark every existing package for deletion, add the new x64+arm64 msix. The
    #     Store fills in version/architecture/languages from each uploaded package during processing,
    #     so a new entry needs only its file name and PendingUpload status.
    $existing = @($sub.applicationPackages)
    foreach ($p in $existing) { $p.fileStatus = 'PendingDelete' }
    $added = $packages | ForEach-Object { [pscustomobject]@{ fileName = $_.Name; fileStatus = 'PendingUpload' } }
    $sub.applicationPackages = @($existing) + @($added)

    # 2b. Set this release's "What's new" on every listing locale that has a base listing.
    foreach ($prop in @($sub.listings.PSObject.Properties)) {
        $base = $prop.Value.baseListing
        if ($null -ne $base) {
            if ($base.PSObject.Properties.Name -contains 'releaseNotes') { $base.releaseNotes = $notes[$Name] }
            else { $base | Add-Member -NotePropertyName releaseNotes -NotePropertyValue $notes[$Name] -Force }
        }
    }

    # 3. Push the patched submission metadata, then upload the packages as a single zip (each msix at
    #    the zip root, its name matching the fileName set above).
    Write-Host '  updating submission metadata (packages + notes)...' -ForegroundColor DarkGray
    Invoke-WithRetry -What 'update submission' { Set-ApplicationSubmission -AppId $ProductId -UpdatedSubmission $sub -NoStatus } | Out-Null

    $zip = Join-Path $storeDir "_upload-$Name.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    try {
        Compress-Archive -Path $packages.FullName -DestinationPath $zip -Force
        Write-Host "  uploading packages ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)..." -ForegroundColor DarkGray
        Invoke-WithRetry -What 'upload packages' { Set-SubmissionPackage -PackagePath $zip -UploadUrl $uploadUrl -NoStatus }
    }
    finally { Remove-Item $zip -Force -ErrorAction SilentlyContinue }

    # 4. Commit + report status once (unless leaving a Draft for a manual portal review).
    if ($NoCommit) {
        Write-Host "  draft ready (submission $($sub.id)); NOT committed - review in Partner Center." -ForegroundColor Yellow
        return
    }
    Write-Host '  committing submission...' -ForegroundColor DarkGray
    Invoke-WithRetry -What 'commit submission' { Complete-ApplicationSubmission -AppId $ProductId -SubmissionId $sub.id -NoStatus }
    try {
        $st = Invoke-WithRetry -What 'get status' { Get-ApplicationSubmissionStatus -AppId $ProductId -SubmissionId $sub.id -NoStatus }
        Write-Host "  committed; certification status: $($st.status)" -ForegroundColor DarkGray
    } catch { }
}

function Show-CurrentSubmission {
    param([string]$Name, [string]$ProductId)
    $app = Invoke-WithRetry -What 'get application' { Get-Application -AppId $ProductId -NoStatus }
    Write-Host "===== $Name ($ProductId) - '$($app.primaryName)' =====" -ForegroundColor Cyan
    $subId = if ($app.pendingApplicationSubmission)      { $app.pendingApplicationSubmission.id }
             elseif ($app.lastPublishedApplicationSubmission) { $app.lastPublishedApplicationSubmission.id }
             else { $null }
    if (-not $subId) { Write-Host '  (no submission found)' -ForegroundColor DarkGray; return }
    $kind = if ($app.pendingApplicationSubmission) { 'pending' } else { 'lastPublished' }
    Write-Host "  $kind submission $subId" -ForegroundColor DarkGray
    Invoke-WithRetry -What 'get submission' { Get-ApplicationSubmission -AppId $ProductId -SubmissionId $subId -NoStatus } | ConvertTo-Json -Depth 20
}

$targets = @()
if ($Edition -in 'classic', 'both') { $targets += @{ Name = 'classic'; ProductId = $ClassicProductId } }
if ($Edition -in 'modern', 'both')  { $targets += @{ Name = 'modern';  ProductId = $ModernProductId } }

Connect-Store

if ($Inspect) {
    foreach ($t in $targets) { Show-CurrentSubmission -Name $t.Name -ProductId $t.ProductId }
    return
}

# Publish each edition INDEPENDENTLY: a failure in one must not abort the other or hide that it
# already submitted. Collect a per-edition result and exit non-zero if any failed.
$results = foreach ($t in $targets) {
    try {
        Publish-Edition -Name $t.Name -ProductId $t.ProductId
        [pscustomobject]@{ Edition = $t.Name; ProductId = $t.ProductId; Result = $(if ($NoCommit) { 'Draft ready (not committed)' } else { 'Committed' }) }
    }
    catch {
        Write-Host "  FAILED ($($t.Name)): $($_.Exception.Message)" -ForegroundColor Red
        [pscustomobject]@{ Edition = $t.Name; ProductId = $t.ProductId; Result = "FAILED - $($_.Exception.Message)" }
    }
}

Write-Host "`n=== Per-edition summary ===" -ForegroundColor Cyan
($results | Format-Table Edition, ProductId, Result -AutoSize | Out-String).TrimEnd() | Write-Host

if (@($results | Where-Object { $_.Result -like 'FAILED*' }).Count) {
    # New-ApplicationSubmission -Force means a rerun re-clones cleanly, so just retry the failed one.
    Write-Host 'One or more editions did not complete. Re-run with -Edition <name> to retry just that one.' -ForegroundColor Yellow
    exit 1
}
Write-Host 'Done.' -ForegroundColor Green

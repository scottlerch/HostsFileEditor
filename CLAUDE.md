# CLAUDE.md

Guidance for working in this repository.

## What this is

**Hosts File Editor** — a Windows desktop app for editing the system `hosts` file
(`%WinDir%\System32\drivers\etc\hosts`). It supports cut/copy/paste/duplicate/enable/disable/move
of entries, filtering and sorting, enabling/disabling the entire hosts file, archiving and restoring
hosts-file configurations (presets, also reachable from a taskbar Jump List), merging another hosts
file with duplicate elimination, auto-pinging endpoints to check availability, and a headless
command line (`apply`/`enable`/`disable`/`import`/`merge`/`list`) shared by both editions.

The repository is **mid-migration from WinForms to WinUI 3**. Two UIs currently ship side by side on
top of one shared core library:

- **`HostsFileEditor.WinForm`** — the legacy "classic" UI (`PackagingFlavor=classic`). Still builds
  and ships.
- **`HostsFileEditor.WinUI`** — the new "modern" WinUI 3 UI (`PackagingFlavor=modern`). The migration
  target.

When changing behavior, prefer putting shared logic in **`HostsFileEditor.Core`** so both UIs benefit.
New UI work should go into the WinUI project; touch the WinForm project only for parity fixes unless
asked otherwise.

## Projects

| Project | Type | TFM | Notes |
|---|---|---|---|
| `HostsFileEditor.Core` | Class library | `net10.0-windows` | Domain model, file I/O, undo/redo, Win32 interop. No UI dependencies. The important code lives here. |
| `HostsFileEditor.Core.Tests` | MSTest | `net10.0-windows` | Tests for Core. Uses MSTest + Shouldly. `InternalsVisibleTo` grants access to internals. |
| `HostsFileEditor.WinUI` | WinUI 3 app | `net10.0-windows10.0.19041.0` | "Modern" UI. DI via `Microsoft.Extensions.DependencyInjection`, MSIX/AOT/trimmed publish, x64+arm64. |
| `HostsFileEditor.WinForm` | WinForms app | `net10.0-windows` | "Classic" UI. Self-contained, ReadyToRun, win-x64. Uses `Equin.ApplicationFramework.BindingListView`. |
| `HostsFileEditor.Elevate` | Helper exe (no window) | `net10.0-windows` | Tiny `asInvoker` exe launched with the `runas` verb to perform privileged hosts-file writes/moves on demand. Copied into an `Elevate\` subfolder beside each app (AOT-published for packages). |
| `HostsFileEditor.Cli` | Console exe (`hfe.exe`) | `net10.0-windows` | Console-subsystem launcher for the shared CLI (`Core/CommandLine/HostsCli`) — `cmd`/PowerShell wait for it, unlike the GUI exes. One-line `Main`. AOT-published into each app's publish root (`PublishCliLauncher` targets); MSIX packages register an `hfe` app-execution alias. |

Solution file is **`HostsFileEditor.slnx`** (the new XML solution format). The WinUI project is pinned
to the `x64` platform in the solution.

## Build, test, run

Run from a **Visual Studio 2022 Developer PowerShell** so `makeappx.exe` and `signtool.exe` (Windows
SDK) are on `PATH` — `dotnet publish` signs and packages MSIX via `Directory.Build.targets`.

```powershell
dotnet build HostsFileEditor.slnx -c Debug        # build everything
dotnet test  HostsFileEditor.slnx                 # run Core.Tests (with coverage.runsettings)
dotnet build HostsFileEditor.slnx -c Release      # optimized build
dotnet publish -c Release                          # produces signed exe + MSIX into artifacts/
dotnet publish -c Release -bl:logs/publish.binlog  # with binary log for troubleshooting
dotnet clean
```

Single-project iteration is fastest for Core work:

```powershell
dotnet build HostsFileEditor.Core/HostsFileEditor.Core.csproj -c Debug
dotnet test  HostsFileEditor.Core.Tests/HostsFileEditor.Core.Tests.csproj
```

The WinUI project requires an explicit platform/RID; defaults are `x64` / `win-x64`. If a plain
`dotnet build` of the solution complains about platform, build the WinUI csproj with
`-p:Platform=x64`.

Both apps run as a standard user (`asInvoker`) — required for the Microsoft Store. Editing the real
hosts file (save / enable / disable) needs admin, so those operations **elevate on demand** by
launching the `HostsFileEditor.Elevate` helper with the `runas` verb (a UAC prompt at save time).
Everything else — viewing, archiving, backups — is per-user (`%LocalAppData%\HostsFileEditor`) and
needs no elevation. Tests never touch the real hosts file — see the test hooks below.

## Conventions (enforced by build)

`Directory.Build.props` applies to **every** project:

- `Nullable=enable`, `ImplicitUsings=enable`
- `TreatWarningsAsErrors=true` — **warnings fail the build.** Don't introduce warnings; don't blanket-
  suppress them. If a suppression is genuinely needed, scope it narrowly and justify it.
- `EnableNETAnalyzers=true`, `AnalysisLevel=latest`
- `RootNamespace=HostsFileEditor` (note: not per-folder; namespaces are hand-written)

`.editorconfig` is the source of truth for style: 4-space indent, CRLF, file-scoped namespaces,
`_camelCase` private fields, `PascalCase` constants, `I`-prefixed interfaces, `var` preferred,
expression-bodied members when on a single line. Run `dotnet format HostsFileEditor.slnx` before
finishing a change.

Per-project `NoWarn` exists for real constraints (WinForms + AOT/trim limitations in the WinForm
project, a few analyzer IDs in Core/WinUI). Read the comment next to a `NoWarn` before assuming it's
safe to remove.

## Core domain model — the important parts

- **`HostsFile`** (`HostsFile.cs`) — central model, exposed as a lazy singleton `HostsFile.Instance`.
  Reads/writes the hosts file, backs it up on load to `%LocalAppData%\HostsFileEditor\hosts.bak`,
  enable/disable the whole file by renaming to `hosts.disabled`, import/export, archive,
  restore-default. Privileged writes/renames go through `Elevation.PrivilegedFileOperations.Current`
  (not `File.*` directly). Calls `NativeMethods.FlushDns()` after mutating the live file.
- **`Elevation/`** — `IPrivilegedFileOperations` with two implementations: `InProcess…` (direct
  `File` ops) and `ElevatedHelper…` (tries direct first, falls back to the `runas` helper on
  access-denied). `PrivilegedFileOperations.Current` is the process-wide gateway; apps call
  `UseElevationHelper()` at startup. `ElevationCancelledException` signals a declined UAC prompt.
- **`HostsEntry`** (`HostsEntry.cs`) — one line of the hosts file. Span-parsed (regex kept as a
  differential-test oracle) into ip/hostnames/comment/enabled/valid; entry-ness is gated on a
  syntactically valid IP first token (`IsValidIpToken`). Implements `INotifyPropertyChanged` and
  `IDataErrorInfo`. Property setters push undo/redo actions onto `UndoManager` (only when the value
  actually changes). `Ping()` runs on demand and disposes its `Ping` itself, so entries are not
  `IDisposable`; a static begin/end ping counter drives `PingActivityChanged` (the UIs' status-bar
  indicator) and `PingFailed` flags per-entry failures. `UnparsedText` lazily re-serializes when
  fields change. Also home to the canonical cross-edition helpers: `MatchesFilter` (the one filter
  predicate) and `GetComparer`/`SortColumn` (the one display-sort comparer; IP sorts numerically via
  a cached per-entry `IpSortKey`).
- **`HostsEntryList`** (`HostsEntryList.cs`) — `BindingList<HostsEntry>` with move/insert/remove/
  enable batch operations, all undo-aware. `InsertItem`/`RemoveItem` overrides register undo actions.
  `MergeLines` appends another file's entries minus duplicates (canonical-IP + hostnames identity)
  as a single undoable step; `PingAll` pings every entry on demand.
- **`HostsArchive` / `HostsArchiveList`** — named snapshots ("presets") stored under
  `%LocalAppData%\HostsFileEditor\archive` (migrated once from the legacy `…\drivers\etc\archive`).
  `HostsArchiveList.Instance` is a singleton `BindingList`; `FindByName` resolves a preset name
  case-insensitively (CLI, Jump List).
- **`CommandLine/`** — `HostsCli` (verb parser + runner for the headless CLI; hand-rolled, no NuGet,
  AOT-safe; exit codes 0/1/2) and `ConsoleAttach` (`AttachConsole` so the GUI-subsystem exes can print
  to the launching console without breaking redirection). Both editions' `Main` route any non-GUI
  argument here — classic excludes the Jump List's `--open-archive`, modern excludes the
  `open-archive:` activation prefix. The modern app supplies its own `Program.Main` via
  `DISABLE_XAML_GENERATED_MAIN` so the CLI runs without booting WinUI.
- **`UndoManager`** (`Utilities/UndoManager.cs`) — singleton `UndoManager.Instance`. Linked-list of
  linked-lists of `Action`. Supports batching (`BatchActions`), suspension (`SuspendUndo/Redo/
  UndoRedo`), capped history (`MaximumHistorySize`), and raises `HistoryChanged`.
- **`Utilities/FileEx`** — `DisableAttributes(path, attrs)` returns an `IDisposable` that clears
  (e.g. ReadOnly) attributes for the duration of a write and restores them on dispose. The hosts file
  is frequently read-only.
- **`Win32/NativeMethods`** — `LibraryImport` P/Invoke for `RegisterWindowMessage`, `SendMessage`,
  `DnsFlushResolverCache`.
- **`Win32/Win32FileDialogs`** — hand-rolled `comdlg32` open/save dialogs (used by the WinUI app,
  which has no built-in file dialog matching the needs).
- **`ProgramSingleInstance`** — mutex-based single-instance enforcement (WinForm). The WinUI app uses
  `AppInstance.FindOrRegisterForKey` instead (`App.xaml.cs`). CLI invocations bypass single-instance
  (they run headless in their own process and exit).

### Per-edition extras (not in Core)

- **`TaskbarJumpList`** (one per edition, deliberately separate): classic uses WindowsAPICodePack
  (COM interop — cannot be used in the AOT'd modern app: `IL3052`); modern uses the WinRT
  `Windows.UI.StartScreen.JumpList` API. Both list the archives as taskbar Jump List entries.
- **`NativeHotkey` + `MainForm.RegisterGlobalHotkey`** (classic) — global show/hide hot key
  (`RegisterHotKey`), combo configurable via the `GlobalShowHideHotkey` user setting (default
  `Control, Shift, H`; blank disables; registration failure shows a one-time guidance popup).

### Test hooks (do not remove)

Core exposes `internal` overrides so tests run without elevation or touching the real system:

- `HostsFile.TestBackupHostFilePathOverride`
- `HostsArchiveList.TestArchiveDirectoryOverride` / `EffectiveArchiveDirectory`

Tests are MSTest classes (`[TestClass]`/`[TestMethod]`) using **Shouldly** assertions
(`value.ShouldBe(...)`). Keep tests free of any dependency on the live hosts file or admin rights.

## WinUI app structure

- **`App.xaml.cs`** — builds the DI container (`DialogService`, `AnimationService`, `MainWindow` as
  singletons), enforces single instance, activates `MainWindow`.
- **`MainWindow.xaml[.cs]` + `MainWindow.Utilities.cs`** — the bulk of the UI. Manual
  `INotifyPropertyChanged`, `{x:Bind}` bindings, keyboard accelerators, filter/archive panels,
  minimal-diff list refresh. It talks to `HostsFile.Instance` / `UndoManager.Instance` directly.
- **`Services/`** — `DialogService` (async dialogs), `AnimationService` (slide animations),
  `SelectionStateService` (selection-dependent button/menu state).
- **`LocalSettings` + `LocalSettingsJsonContext`** — JSON settings persisted to
  `%LocalAppData%\HostsFileEditor\settings.json`, using a source-generated `JsonSerializerContext`
  (required because the app is trimmed/AOT).

## Packaging (Directory.Build.targets)

`dotnet publish` runs a `SignAndPackage` target after publish: signs the binaries (app exe, the
`Elevate` helper, and `hfe.exe`), builds an MSIX with `makeappx`, optionally signs the MSIX
(`SignPackage=true`, sideload only — needs the manifest Publisher to match the cert subject; see
`docs/signing.md`), optionally zips, and copies outputs to `artifacts\<flavor>\`. Per-app
`PublishElevateHelper`/`PublishCliLauncher` targets AOT-publish the helper exes into the publish
output first, and strip targets delete unused runtime libraries (WPF natives from classic, WinApp SDK
ML/Widgets libs from modern). Tool paths (`signtool.exe`/`makeappx.exe`) resolve from the Windows SDK
via several fallbacks (VS dev shell env vars, then registry `KitsRoot10`). Test signing cert is
`HostsFileEditorTestCert.pfx` (password `test`) — for local/dev only.

## Store release automation (`publish-store.ps1`)

Both editions ship to the Microsoft Store as separate apps — **classic `9NF73PSPK332`**, **modern
`9NBQWCDXGF9R`**. Instead of the Partner Center portal UI, `publish-store.ps1` scripts the
submissions with **StoreBroker** — Microsoft's PowerShell module over the Store submission REST API.
(The `msstore` CLI was tried first and abandoned: its `publish` builds a project of a recognized
type and can't push our pre-built, externally-signed MSIX to an existing app by product id.)

- **Install tooling (one-time):** `.\publish-store.ps1 -InstallTooling` — `Install-Module StoreBroker
  -Scope CurrentUser`. Pure PowerShell; no winget/runtime prerequisites.
- **Authenticate:** create an Azure AD app in Partner Center (Account settings → User management →
  *Azure AD applications*, **Manager** role) to get the **TenantId / ClientId** and a **client secret**
  (a "Key"). The script calls `Set-StoreBrokerAuthentication` for you from those three values, resolved
  in order: `-TenantId`/`-ClientId`/`-ClientSecret` params → `STOREBROKER_TENANTID`/`STOREBROKER_CLIENTID`/
  `STOREBROKER_CLIENTSECRET` env vars → interactive prompt. The secret stays a `SecureString` and is
  never written to disk by the script. No SellerId is needed (that was a `msstore`-ism). Never commit
  the secret — env var or prompt only.
- **Each release:** `.\build-all.ps1 -Sign` (produces `artifacts\store\HostsFileEditor-<flavor>-<arch>.msix`)
  then `.\publish-store.ps1` — for each edition it **clones the current published submission**
  (`New-ApplicationSubmission -Force`), marks the existing packages `PendingDelete` and adds the new
  x64+arm64 msix as `PendingUpload`, sets the "What's new" on every listing locale
  (`listings.<locale>.baseListing.releaseNotes` — note StoreBroker uses the **raw API camelCase**, not
  msstore's PascalCase), pushes it (`Set-ApplicationSubmission`), uploads the packages as one zip
  (`Set-SubmissionPackage`), then commits (`Complete-ApplicationSubmission`). Cloning preserves the
  rich listing (description/screenshots); only packages + notes change. `-NoCommit` leaves a Draft for
  a final portal check; `-Edition classic|modern` limits scope; `-Inspect` dumps the current submission.
- **Verify on the first run:** use `-NoCommit` and confirm in Partner Center that **both** architectures
  uploaded at the new version before committing there. The per-package summary line prints each msix's
  manifest version so a forgotten version bump is caught before upload.
- **Reruns / partial failure:** each edition publishes independently — if one fails, the other still
  completes, a per-edition summary prints, and the script exits non-zero. `New-ApplicationSubmission
  -Force` discards any half-built pending submission and re-clones from what's published, so a rerun is
  clean; `-Edition <name>` retries just one.

## Gotchas

- **Warnings are errors.** A change that compiles in isolation can still fail the solution build.
- **Singletons everywhere.** `HostsFile.Instance`, `HostsArchiveList.Instance`, `UndoManager.Instance`
  are global mutable state. Order of operations matters (e.g. creating `HostsFile` clears undo
  history). Be careful introducing parallelism or re-entrancy.
- **Two UIs, one core.** Verify Core changes don't break WinForm bindings (it uses
  `BindingListView` + `IDataErrorInfo`) when modernizing.
- **AOT/trimming (WinUI) + reflection.** The WinUI app is trimmed and AOT-published; anything
  reflective (JSON, `BindingList` property descriptors) needs source generation or a documented
  `UnconditionalSuppressMessage`. Several already exist in Core — keep them.
- **`hosts` file specifics.** Often read-only and admin-owned; don't write it with `File.*` directly —
  go through `Elevation.PrivilegedFileOperations.Current` so it elevates on demand. Flush DNS after
  changing the live file. Never write the real hosts file from tests.

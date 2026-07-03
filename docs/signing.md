# Code signing

Hosts File Editor's binaries are Authenticode-signed with **Azure Trusted Signing** (formerly
Azure Code Signing / "Artifact Signing"). Signing the app exe and the elevation helper gives a
verified publisher on the UAC prompt &mdash; for **both** Store and self-distributed builds &mdash;
and builds SmartScreen reputation for the self-distributed builds (GitHub Releases).

> **The MSIX *package* does not need signing for the Microsoft Store** &mdash; the Store re-signs
> the `.msix` with Microsoft's certificate on ingestion, so Store submissions work with the package
> unsigned. **The binaries inside it are a different matter.** The Store re-signs the *package*, not
> the exe files within it, so signing the app exe and the elevation helper is recommended for **both**
> the Store and self-distributed builds: the UAC "Verified publisher" line on the on-demand elevation
> prompt comes from the Authenticode signature embedded in `HostsFileEditor.Elevate.exe` itself, not
> from the package signature. Without it, that prompt reads "Unknown Publisher" even on a Store install.
> Exe/helper signing also builds SmartScreen reputation for the self-distributed GitHub-release exe/zip.

## What is signed

- `HostsFileEditor.exe` (the app)
- `Elevate\HostsFileEditor.Elevate.exe` (the on-demand elevation helper)

The MSIX package itself is **not** signed by default (`SignPackage=false`) — the Store re-signs
it, and a Store-identity package can't be signed with a normal cert anyway. See
[Signing a self-distributed MSIX](#signing-a-self-distributed-msix) below.

## Account details

These are captured in [`signing/metadata.json`](../signing/metadata.json) (the signtool `/dmdf`
file), which the build picks up automatically:

| Field | Value |
|---|---|
| Endpoint | `https://eus.codesigning.azure.net/` (East US) |
| Code signing account | `scottlerchcodesign` |
| Certificate profile | `ScottLerch` (Public Trust, Program = none) |
| Certificate subject | `CN=Scott Lerch, O=Scott Lerch, L=San Mateo, S=ca, C=US` |

## One-time setup

### 1. Azure (portal)

1. **Certificate profile** — in the Trusted Signing account → *Certificate profiles* → create a
   **Public Trust** profile named `ScottLerch`, referencing the completed identity validation.
2. **RBAC** — on the account (or profile) → *Access control (IAM)* → assign
   **"Trusted Signing Certificate Profile Signer"** to whoever signs: your user (local signing)
   and/or a service principal (CI). *Missing this role is the usual 403 at sign time.*

### 2. Local machine

1. **Signtool** — needs Windows SDK **10.0.22621+** for `/dlib` support (the build resolves
   signtool from the installed SDK; run from a VS Developer PowerShell so it's on `PATH`).
2. **Trusted Signing dlib** — restore the NuGet package once so it lands in your global cache:
   ```powershell
   dotnet nuget locals global-packages --list   # note the folder
   # then, from any throwaway project or via your package manager, restore:
   #   Microsoft.Trusted.Signing.Client  (>= 1.0.60)
   ```
   `build-all.ps1 -Sign` then auto-discovers `Azure.CodeSigning.Dlib.dll` under
   `…\microsoft.trusted.signing.client\<version>\bin\x64\` — you don't pass a path.
3. **Authenticate** — the dlib uses `DefaultAzureCredential`, so sign in as the account that has
   the Signer role:
   ```powershell
   az login            # use the personal account that owns the Trusted Signing resource,
                       # e.g. az login --use-device-code if the browser account is wrong
   az account show     # confirm the right tenant/subscription is active
   ```

## Signing a build

Everything is wired, so after `az login`:

```powershell
.\build-all.ps1 -Sign
```

This publishes all four packages (classic/modern × x64/arm64) and signs the exe + helper in each.
Under the hood it sets `-p:EnableSigning=true`, auto-resolves `SigningDlib`, defaults
`SigningMetadata` to `signing/metadata.json`, and uses Microsoft's timestamp server
(`http://timestamp.acs.microsoft.com`) — timestamping is **mandatory** because Trusted Signing
certificates are short-lived (a missing timestamp means the signature expires in days).

To sign just one project via the SDK directly:

```powershell
dotnet publish HostsFileEditor.WinForm\HostsFileEditor.WinForm.csproj -c Release -r win-x64 `
  -p:EnableSigning=true `
  -p:SigningDlib="D:\...\microsoft.trusted.signing.client\1.0.60\bin\x64\Azure.CodeSigning.Dlib.dll"
```

## Verify a signature

```powershell
signtool verify /pa /v .\artifacts\classic\x64\HostsFileEditor.exe
```

You should see the chain to Microsoft's Trusted Signing PCA, the subject `CN=Scott Lerch`, and a
valid RFC3161 timestamp.

## CI

Use a service principal (or workload-identity/OIDC) that has the Signer role, and provide its
credentials via the standard `AZURE_TENANT_ID` / `AZURE_CLIENT_ID` / `AZURE_CLIENT_SECRET`
environment variables (picked up by `DefaultAzureCredential`). Everything else is identical.

## Signing a self-distributed MSIX

Signing the `.msix` package (rather than just the exe inside it) requires
`-p:SignPackage=true` **and** the package `Identity/Publisher` in `AppxManifest.xml` to exactly
match the signing certificate's subject (`CN=Scott Lerch, O=Scott Lerch, L=San Mateo, S=ca,
C=US`). The manifests currently use the **Store** publisher ID (`CN=427E0D6A-…`) because they're
built for Store submission, so package signing needs a separate self-distribution manifest
variant. The exe/helper signing above has no such constraint.

## Signing modes (build reference)

`Directory.Build.targets` auto-selects the signing mode (first match wins):

1. **Trusted Signing** — `SigningDlib` + `SigningMetadata` (→ `signtool /dlib /dmdf`). ← what we use
2. **Cert store / HSM** — `SigningCertThumbprint` (→ `signtool /sha1`)
3. **PFX file** — `CertFile` + `CertPassword` (defaults to the local test cert — dev only)

# Distribution

The lightest default package for HaloLight is a framework-dependent `win-x64` publish.

## Why this default

- Smallest output size
- Simple local distribution as a zip file
- Good fit for lightweight zip-based Windows distribution

Use the self-contained option only when you need a no-prerequisite zip for machines that do not already have the .NET 8 Desktop Runtime installed.

## Build packages

Framework-dependent:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-local.ps1
```

Self-contained:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-local.ps1 -SelfContained
```

EXE installer (recommended default for non-technical users):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

EXE installer using framework-dependent publish output:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -FrameworkDependent
```

MSI installer:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1
```

MSI installer using framework-dependent publish output:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1 -FrameworkDependent
```

The EXE installer build requires `Inno Setup 6` with `ISCC.exe` available in `PATH` or installed in its default Windows location.

The MSI installer build requires `WiX Toolset 3.14` with `heat.exe`, `candle.exe`, and `light.exe` available in `PATH` or installed in their default Windows location.

## Output

- Publish folder: `artifacts\publish\win-x64\framework-dependent`
- Zip package: `artifacts\HaloLight-win-x64-framework-dependent.zip`
- EXE installer output: `artifacts\installer\HaloLight-0.1.0-Setup.exe`
- MSI installer output: `artifacts\installer\HaloLight-0.1.0.msi`

## Automatic GitHub releases

`.github/workflows/build-windows-release.yml` runs on pull requests targeting `main` and on pushes to `main`.

- Pull requests use it as a packaging/build validation check only
- Pushes to `main` also create the tag and publish the GitHub Release

- Release tags use the format `vX.Y.Z`
- If the current commit already has a semver tag, reruns reuse that tag and update the existing release assets
- If no release tags exist yet, the workflow starts from the `<Version>` in `src\HaloLight\HaloLight.csproj`
- Otherwise the workflow increments the latest release tag by one patch version per new push, unless the project version is manually moved ahead for a new major or minor train

## Runtime note

Framework-dependent builds require the `.NET 8 Desktop Runtime x64` on the target machine.

The installer scripts default to a self-contained publish so end users do not need to install the runtime separately.

# Distribution

The recommended release package for HaloLight is the Inno Setup `setup.exe` installer.

## Why this default

- Best fit for normal Windows users
- Includes shortcuts and uninstall support
- Avoids confusion from multiple downloadable package types

The publish script is still useful when you want a raw output folder for testing or manual packaging.

## Build packages

Framework-dependent publish folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-local.ps1
```

Self-contained publish folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-local.ps1 -SelfContained
```

Recommended installer (`setup.exe`):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

Installer using framework-dependent publish output:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -FrameworkDependent
```

The EXE installer build requires `Inno Setup 6` with `ISCC.exe` available in `PATH` or installed in its default Windows location.

## Output

- Publish folder: `artifacts\publish\win-x64\framework-dependent`
- EXE installer output: `artifacts\installer\HaloLight-0.1.0-Setup.exe`

## Automatic GitHub releases

`.github/workflows/build-windows-release.yml` runs on pull requests targeting `main` and on pushes to `main`.

- Pull requests use it as an installer validation check only
- Pushes to `main` also create the tag and publish the GitHub Release with the `setup.exe` asset

- Release tags use the format `vX.Y.Z`
- If the current commit already has a semver tag, reruns reuse that tag and update the existing release assets
- If no release tags exist yet, the workflow starts from the `<Version>` in `src\HaloLight\HaloLight.csproj`
- Otherwise the workflow increments the latest release tag by one patch version per new push, unless the project version is manually moved ahead for a new major or minor train

## Runtime note

Framework-dependent builds require the `.NET 8 Desktop Runtime x64` on the target machine.

The installer scripts default to a self-contained publish so end users do not need to install the runtime separately.

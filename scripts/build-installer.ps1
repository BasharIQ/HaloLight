param(
    [string]$Project = ".\src\HaloLight\HaloLight.csproj",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$AppVersion,
    [switch]$FrameworkDependent,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot $Project
$publishScript = Join-Path $PSScriptRoot "publish-local.ps1"
$installerScript = Join-Path $repoRoot "installer\HaloLight.iss"
$outputDir = Join-Path $repoRoot "artifacts\installer"

$packageKind = if ($FrameworkDependent) { "framework-dependent" } else { "self-contained" }
$publishDir = Join-Path $repoRoot ("artifacts\publish\{0}\{1}" -f $Runtime, $packageKind)

if (-not (Test-Path $projectPath)) {
    throw "Project not found: $projectPath"
}

if (-not (Test-Path $installerScript)) {
    throw "Installer script not found: $installerScript"
}

if (-not $SkipPublish) {
    $publishArgs = @{
        Project = $Project
        Configuration = $Configuration
        Runtime = $Runtime
    }

    if (-not [string]::IsNullOrWhiteSpace($AppVersion)) {
        $publishArgs.AppVersion = $AppVersion
    }

    if (-not $FrameworkDependent) {
        $publishArgs.SelfContained = $true
    }

    & $publishScript @publishArgs
}

if (-not (Test-Path $publishDir)) {
    throw "Publish folder not found: $publishDir"
}

[xml]$projectXml = Get-Content -Path $projectPath
$version = $AppVersion
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "0.1.0"
}

$iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
if (-not $iscc) {
    $commonPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $commonPaths) {
        if (Test-Path $candidate) {
            $iscc = Get-Item $candidate
            break
        }
    }
}

if (-not $iscc) {
    throw "Inno Setup compiler not found. Install Inno Setup 6 and rerun this script."
}

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

$compilerPath = if ($iscc -is [System.Management.Automation.CommandInfo]) { $iscc.Source } else { $iscc.FullName }

& $compilerPath `
    "/DAppVersion=$version" `
    "/DPublishDir=$publishDir" `
    "/DOutputDir=$outputDir" `
    $installerScript

Write-Host "Created installer in: $outputDir"
Write-Host "Installer package type: $packageKind"

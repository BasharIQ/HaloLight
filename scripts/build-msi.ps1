param(
    [string]$Project = ".\src\HaloLight\HaloLight.csproj",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$AppVersion,
    [switch]$FrameworkDependent,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

function Get-ToolPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandName,
        [Parameter(Mandatory = $true)]
        [string[]]$CandidatePaths
    )

    $command = Get-Command $CommandName -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    foreach ($candidate in $CandidatePaths) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "Required tool not found: $CommandName"
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot $Project
$publishScript = Join-Path $PSScriptRoot "publish-local.ps1"
$msiTemplate = Join-Path $repoRoot "installer\HaloLight.msi.wxs"
$outputDir = Join-Path $repoRoot "artifacts\installer"
$tempDir = Join-Path $outputDir "wix-temp"

$packageKind = if ($FrameworkDependent) { "framework-dependent" } else { "self-contained" }
$publishDir = Join-Path $repoRoot ("artifacts\publish\{0}\{1}" -f $Runtime, $packageKind)

if (-not (Test-Path $projectPath)) {
    throw "Project not found: $projectPath"
}

if (-not (Test-Path $msiTemplate)) {
    throw "MSI template not found: $msiTemplate"
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

$heatPath = Get-ToolPath -CommandName "heat.exe" -CandidatePaths @(
    "C:\Program Files (x86)\WiX Toolset v3.14\bin\heat.exe",
    "C:\Program Files\WiX Toolset v3.14\bin\heat.exe"
)

$candlePath = Get-ToolPath -CommandName "candle.exe" -CandidatePaths @(
    "C:\Program Files (x86)\WiX Toolset v3.14\bin\candle.exe",
    "C:\Program Files\WiX Toolset v3.14\bin\candle.exe"
)

$lightPath = Get-ToolPath -CommandName "light.exe" -CandidatePaths @(
    "C:\Program Files (x86)\WiX Toolset v3.14\bin\light.exe",
    "C:\Program Files\WiX Toolset v3.14\bin\light.exe"
)

if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}

New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

$harvestedFile = Join-Path $tempDir "PublishedFiles.wxs"
$msiPath = Join-Path $outputDir ("HaloLight-{0}.msi" -f $version)

if (Test-Path $msiPath) {
    Remove-Item $msiPath -Force
}

& $heatPath dir $publishDir `
    -nologo `
    -cg PublishedFiles `
    -dr INSTALLFOLDER `
    -srd `
    -scom `
    -sreg `
    -gg `
    -var var.PublishDir `
    -out $harvestedFile

& $candlePath `
    -nologo `
    -arch x64 `
    -dAppVersion=$version `
    -dPublishDir=$publishDir `
    -dRepoRoot=$repoRoot `
    -out (Join-Path $tempDir "") `
    $msiTemplate `
    $harvestedFile

& $lightPath `
    -nologo `
    -out $msiPath `
    (Join-Path $tempDir "HaloLight.msi.wixobj") `
    (Join-Path $tempDir "PublishedFiles.wixobj")

Write-Host "Created MSI installer: $msiPath"
Write-Host "MSI package type:     $packageKind"

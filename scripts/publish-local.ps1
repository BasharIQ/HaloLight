param(
    [string]$Project = ".\src\HaloLight\HaloLight.csproj",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$AppVersion,
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }
$packageKind = if ($SelfContained) { "self-contained" } else { "framework-dependent" }

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot $Project
$publishDir = Join-Path $repoRoot ("artifacts\publish\{0}\{1}" -f $Runtime, $packageKind)
$zipPath = Join-Path $repoRoot ("artifacts\HaloLight-{0}-{1}.zip" -f $Runtime, $packageKind)

$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", $selfContainedValue,
    "-o", $publishDir,
    "-p:PublishSingleFile=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

if (-not [string]::IsNullOrWhiteSpace($AppVersion)) {
    $assemblyVersion = "{0}.0" -f $AppVersion
    $publishArgs += @(
        "-p:Version=$AppVersion",
        "-p:InformationalVersion=$AppVersion",
        "-p:AssemblyVersion=$assemblyVersion",
        "-p:FileVersion=$assemblyVersion"
    )
}

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

& dotnet @publishArgs

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host "Created publish folder: $publishDir"
Write-Host "Created zip package:   $zipPath"
Write-Host "Package type:          $packageKind"
if (-not [string]::IsNullOrWhiteSpace($AppVersion)) {
    Write-Host "App version:           $AppVersion"
}

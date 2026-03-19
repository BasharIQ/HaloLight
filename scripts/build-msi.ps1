param(
    [string]$Project = ".\src\HaloLight\HaloLight.csproj",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$AppVersion,
    [switch]$FrameworkDependent,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

function ConvertTo-MsiVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $parts = $Version.Split('.')
    if ($parts.Count -lt 1 -or $parts.Count -gt 4) {
        throw "MSI version must have between 1 and 4 numeric parts: $Version"
    }

    $normalizedParts = @()
    foreach ($part in $parts) {
        $value = 0
        if (-not [int]::TryParse($part, [ref]$value)) {
            throw "MSI version parts must be numeric: $Version"
        }

        if ($value -lt 0 -or $value -gt 65534) {
            throw "MSI version parts must be between 0 and 65534: $Version"
        }

        $normalizedParts += $value
    }

    while ($normalizedParts.Count -lt 4) {
        $normalizedParts += 0
    }

    return ($normalizedParts -join '.')
}

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

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath"
    }
}

function Update-HarvestedWixFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    [xml]$wixXml = Get-Content -Path $FilePath
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($wixXml.NameTable)
    $namespaceManager.AddNamespace('wix', 'http://schemas.microsoft.com/wix/2006/wi')

    $components = $wixXml.SelectNodes('//wix:Component', $namespaceManager)
    foreach ($component in $components) {
        $fileNode = $component.SelectSingleNode('wix:File[@KeyPath="yes"]', $namespaceManager)
        if ($fileNode -ne $null) {
            $fileNode.SetAttribute('KeyPath', 'no')
        }

        $componentId = $component.GetAttribute('Id')
        if ([string]::IsNullOrWhiteSpace($componentId)) {
            throw 'Harvested WiX component is missing an Id attribute.'
        }

        $registryValue = $wixXml.CreateElement('RegistryValue', $wixXml.DocumentElement.NamespaceURI)
        $registryValue.SetAttribute('Root', 'HKCU')
        $registryValue.SetAttribute('Key', 'Software\HaloLight\Components')
        $registryValue.SetAttribute('Name', $componentId)
        $registryValue.SetAttribute('Type', 'integer')
        $registryValue.SetAttribute('Value', '1')
        $registryValue.SetAttribute('KeyPath', 'yes')
        $null = $component.AppendChild($registryValue)
    }

    $wixXml.Save($FilePath)
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

$msiVersion = ConvertTo-MsiVersion -Version $version

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

Invoke-NativeCommand -FilePath $heatPath -Arguments @(
    'dir',
    $publishDir,
    '-nologo',
    '-cg', 'PublishedFiles',
    '-dr', 'INSTALLFOLDER',
    '-srd',
    '-scom',
    '-sreg',
    '-gg',
    '-var', 'var.PublishDir',
    '-out', $harvestedFile
)

Update-HarvestedWixFile -FilePath $harvestedFile

Invoke-NativeCommand -FilePath $candlePath -Arguments @(
    '-nologo',
    '-arch', 'x64',
    "-dAppVersion=$version",
    "-dMsiVersion=$msiVersion",
    "-dPublishDir=$publishDir",
    "-dRepoRoot=$repoRoot",
    '-out', (Join-Path $tempDir ''),
    $msiTemplate,
    $harvestedFile
)

Invoke-NativeCommand -FilePath $lightPath -Arguments @(
    '-nologo',
    '-out', $msiPath,
    (Join-Path $tempDir 'HaloLight.msi.wixobj'),
    (Join-Path $tempDir 'PublishedFiles.wixobj')
)

Write-Host "Created MSI installer: $msiPath"
Write-Host "MSI package type:     $packageKind"
Write-Host "MSI product version:  $msiVersion"

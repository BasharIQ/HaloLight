param(
    [string]$ProcessName = "HaloLight",
    [string]$OutputDirectory = "docs/screenshots",
    [string]$AppPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class WindowCapture
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
}
"@

function Wait-ForMainWindow {
    param(
        [int]$Id,
        [int]$TimeoutSeconds = 15
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 250
        $candidate = Get-Process -Id $Id -ErrorAction SilentlyContinue
        if ($null -ne $candidate) {
            $candidate.Refresh()
            if ($candidate.MainWindowHandle -ne 0) {
                return $candidate
            }
        }
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for process $Id to create its main window."
}

function Restore-Settings {
    param(
        [string]$Path,
        [string]$OriginalContent
    )

    if ($null -eq $OriginalContent) {
        if (Test-Path $Path) {
            Remove-Item $Path -Force
        }
        return
    }

    Set-Content -Path $Path -Value $OriginalContent -Encoding UTF8
}

function New-ScaledBitmap {
    param(
        [System.Drawing.Bitmap]$SourceBitmap,
        [int]$MaxWidth
    )

    if ($SourceBitmap.Width -le $MaxWidth) {
        return $SourceBitmap
    }

    $scale = $MaxWidth / [double]$SourceBitmap.Width
    $targetWidth = $MaxWidth
    $targetHeight = [Math]::Round($SourceBitmap.Height * $scale)
    $scaledBitmap = New-Object System.Drawing.Bitmap $targetWidth, $targetHeight
    $graphics = [System.Drawing.Graphics]::FromImage($scaledBitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.DrawImage($SourceBitmap, 0, 0, $targetWidth, $targetHeight)
    $graphics.Dispose()
    $SourceBitmap.Dispose()
    return $scaledBitmap
}

function Save-Bitmap {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path,
        [int]$MaxWidth = 0
    )

    $bitmapToSave = $Bitmap
    if ($MaxWidth -gt 0) {
        $bitmapToSave = New-ScaledBitmap -SourceBitmap $Bitmap -MaxWidth $MaxWidth
    }

    try {
        $bitmapToSave.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmapToSave.Dispose()
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$resolvedOutputDirectory = Join-Path $projectRoot $OutputDirectory
New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($AppPath)) {
    $AppPath = Join-Path $projectRoot "src/HaloLight/bin/Debug/net8.0-windows/HaloLight.exe"
}

if (-not (Test-Path $AppPath)) {
    throw "Could not find HaloLight executable at '$AppPath'."
}

$settingsDirectory = Join-Path $env:LOCALAPPDATA "HaloLight"
$settingsPath = Join-Path $settingsDirectory "settings.json"
New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null

$originalSettingsContent = if (Test-Path $settingsPath) {
    Get-Content -Path $settingsPath -Raw
}
else {
    $null
}

$primaryScreen = [System.Windows.Forms.Screen]::PrimaryScreen
$settingsWindowSettings = [ordered]@{
    IsEnabled = $true
    Brightness = 68
    ColorTemperature = 4700
    EdgeThickness = 140
    SecondaryColorHex = "#29D7FF"
    MonitorDeviceName = $primaryScreen.DeviceName
    LaunchAtStartup = $false
    ExcludeFromCapture = $true
}

$overlaySettings = [ordered]@{
    IsEnabled = $true
    Brightness = 68
    ColorTemperature = 4700
    EdgeThickness = 140
    SecondaryColorHex = "#29D7FF"
    MonitorDeviceName = $primaryScreen.DeviceName
    LaunchAtStartup = $false
    ExcludeFromCapture = $false
}

$wasRunning = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue).Count -gt 0
$screenshotProcess = $null
$backdrop = $null

try {
    Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500

    $settingsWindowSettings | ConvertTo-Json | Set-Content -Path $settingsPath -Encoding UTF8

    $screenshotProcess = Start-Process -FilePath $AppPath -PassThru
    $process = Wait-ForMainWindow -Id $screenshotProcess.Id
    $handle = $process.MainWindowHandle

    [WindowCapture]::MoveWindow($handle, 120, 80, 480, 700, $true) | Out-Null
    [WindowCapture]::ShowWindow($handle, 5) | Out-Null
    [WindowCapture]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Milliseconds 900

    $windowRect = New-Object WindowCapture+RECT
    [WindowCapture]::GetWindowRect($handle, [ref]$windowRect) | Out-Null

    $windowWidth = $windowRect.Right - $windowRect.Left
    $windowHeight = $windowRect.Bottom - $windowRect.Top
    $settingsBitmap = New-Object System.Drawing.Bitmap $windowWidth, $windowHeight
    $settingsGraphics = [System.Drawing.Graphics]::FromImage($settingsBitmap)
    $settingsGraphics.CopyFromScreen($windowRect.Left, $windowRect.Top, 0, 0, $settingsBitmap.Size)
    $settingsGraphics.Dispose()
    Save-Bitmap -Bitmap $settingsBitmap -Path (Join-Path $resolvedOutputDirectory "settings-window.png")

    Stop-Process -Id $process.Id -Force
    Start-Sleep -Milliseconds 500

    $overlaySettings | ConvertTo-Json | Set-Content -Path $settingsPath -Encoding UTF8
    $screenshotProcess = Start-Process -FilePath $AppPath -PassThru
    $process = Wait-ForMainWindow -Id $screenshotProcess.Id
    $handle = $process.MainWindowHandle
    [WindowCapture]::ShowWindow($handle, 0) | Out-Null
    Start-Sleep -Milliseconds 500

    $backdrop = New-Object System.Windows.Forms.Form
    $backdrop.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $backdrop.Bounds = $primaryScreen.Bounds
    $backdrop.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
    $backdrop.TopMost = $false
    $backdrop.BackColor = [System.Drawing.Color]::FromArgb(16, 23, 35)
    $backdrop.ShowInTaskbar = $false

    $headline = New-Object System.Windows.Forms.Label
    $headline.Text = "HaloLight"
    $headline.ForeColor = [System.Drawing.Color]::FromArgb(245, 248, 252)
    $headline.BackColor = [System.Drawing.Color]::Transparent
    $headline.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 34, [System.Drawing.FontStyle]::Bold)
    $headline.AutoSize = $true
    $headline.Location = New-Object System.Drawing.Point(250, 220)

    $tagline = New-Object System.Windows.Forms.Label
    $tagline.Text = "Soft display fill light for calls, streaming, and late-night work."
    $tagline.ForeColor = [System.Drawing.Color]::FromArgb(197, 214, 232)
    $tagline.BackColor = [System.Drawing.Color]::Transparent
    $tagline.Font = New-Object System.Drawing.Font("Segoe UI", 14)
    $tagline.AutoSize = $true
    $tagline.Location = New-Object System.Drawing.Point(254, 284)

    $featurePanel = New-Object System.Windows.Forms.Panel
    $featurePanel.BackColor = [System.Drawing.Color]::FromArgb(120, 24, 36, 54)
    $featurePanel.Location = New-Object System.Drawing.Point(254, 360)
    $featurePanel.Size = New-Object System.Drawing.Size(420, 172)

    $featureText = New-Object System.Windows.Forms.Label
    $featureText.Text = "Brightness`r`nColor temperature`r`nEdge thickness`r`nCapture exclusion"
    $featureText.ForeColor = [System.Drawing.Color]::FromArgb(232, 240, 248)
    $featureText.BackColor = [System.Drawing.Color]::Transparent
    $featureText.Font = New-Object System.Drawing.Font("Segoe UI", 16)
    $featureText.AutoSize = $true
    $featureText.Location = New-Object System.Drawing.Point(28, 24)
    $featurePanel.Controls.Add($featureText)

    $backdrop.Controls.Add($headline)
    $backdrop.Controls.Add($tagline)
    $backdrop.Controls.Add($featurePanel)
    $backdrop.Add_Paint({
        param($sender, $eventArgs)

        $graphics = $eventArgs.Graphics
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::FromArgb(16, 23, 35))

        $gradientRect = New-Object System.Drawing.Rectangle 0, 0, $sender.ClientSize.Width, $sender.ClientSize.Height
        $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $gradientRect,
            [System.Drawing.Color]::FromArgb(17, 24, 39),
            [System.Drawing.Color]::FromArgb(34, 78, 102),
            35
        )
        $graphics.FillRectangle($brush, $gradientRect)
        $brush.Dispose()

        $glowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(42, 81, 217, 255))
        $graphics.FillEllipse($glowBrush, $sender.ClientSize.Width - 420, 90, 280, 280)
        $graphics.FillEllipse($glowBrush, $sender.ClientSize.Width - 620, 420, 220, 220)
        $glowBrush.Dispose()
    })

    $backdrop.Show()
    [System.Windows.Forms.Application]::DoEvents()
    Start-Sleep -Milliseconds 900

    $screenBounds = $primaryScreen.Bounds
    $overlayBitmap = New-Object System.Drawing.Bitmap $screenBounds.Width, $screenBounds.Height
    $overlayGraphics = [System.Drawing.Graphics]::FromImage($overlayBitmap)
    $overlayGraphics.CopyFromScreen($screenBounds.Location, [System.Drawing.Point]::Empty, $screenBounds.Size)
    $overlayGraphics.Dispose()
    Save-Bitmap -Bitmap $overlayBitmap -Path (Join-Path $resolvedOutputDirectory "desktop-overlay.png") -MaxWidth 1600
}
finally {
    if ($null -ne $backdrop) {
        $backdrop.Close()
        $backdrop.Dispose()
    }

    Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Stop-Process -Force
    Restore-Settings -Path $settingsPath -OriginalContent $originalSettingsContent

    if ($wasRunning) {
        Start-Process -FilePath $AppPath | Out-Null
    }
}

Write-Host "Saved screenshots to $resolvedOutputDirectory"

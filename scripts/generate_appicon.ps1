#requires -Version 5.1

<#
.SYNOPSIS
    Generate MarvelHeroes.DpsMeter\AppIcon.ico from docs\cerebro-banner.png.

.DESCRIPTION
    Loads the Cerebro banner image, crops it to a centered square (no stretching --
    just trimming the wider sides), downscales to every standard icon size
    (16, 24, 32, 48, 64, 128, 256) with high-quality bicubic interpolation, and
    packs the resulting PNG payloads into a modern PNG-in-ICO container at
    MarvelHeroes.DpsMeter\AppIcon.ico.

    Deterministic -- running twice on the same source produces byte-identical
    output (no random dither, no timestamp embedded).

    Crop strategy:
      The source banner is 1300x650 (aspect 2.000).  We take the largest
      square centered on the image -- 650x650 starting at x=325, y=0 -- which
      captures the helmet, visor V-point, face, and chin without stretching.

      If you want a tighter crop later (e.g. visor-only for higher impact at
      16x16), tweak the $cropSize / $cropOffsetX / $cropOffsetY constants below.

.NOTES
    Previous version of this script drew a procedural orange lightning bolt --
    if you ever want that back, `git log scripts/generate_appicon.ps1` will
    show the prior implementation.

    System.Drawing.Common is Windows-only but this project already targets
    net8.0-windows so there's no portability constraint to worry about.
#>

[CmdletBinding()]
param(
    [string] $SourcePath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'docs\cerebro-banner.png'),
    [string] $OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'MarvelHeroes.DpsMeter\AppIcon.ico')
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $SourcePath)) {
    throw ("Source image not found at {0}. Either drop a banner image there or pass -SourcePath." -f $SourcePath)
}

# Sizes to include in the ICO container.  16/24/32 cover taskbar + tray, 48 is
# Alt+Tab, 64/128/256 cover Explorer "large icons" / high-DPI scaling.
[int[]] $sizes = 16, 24, 32, 48, 64, 128, 256

# Load source once, hold it for all the per-size downscales.
$src = New-Object System.Drawing.Bitmap($SourcePath)
Write-Host ("Source: {0}x{1}" -f $src.Width, $src.Height)

# Largest centered square crop -- no stretching, just trims the long axis.
[int] $cropSize    = [Math]::Min($src.Width, $src.Height)
[int] $cropOffsetX = [int][Math]::Floor(($src.Width  - $cropSize) / 2.0)
[int] $cropOffsetY = [int][Math]::Floor(($src.Height - $cropSize) / 2.0)
Write-Host ("Crop:   {0}x{0} at offset ({1},{2})" -f $cropSize, $cropOffsetX, $cropOffsetY)

function New-IconBitmap {
    param(
        [System.Drawing.Bitmap] $Source,
        [int] $CropSize,
        [int] $OffsetX,
        [int] $OffsetY,
        [int] $TargetSize
    )

    $bmp = New-Object System.Drawing.Bitmap($TargetSize, $TargetSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)

    try {
        $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

        # DrawImage with explicit src + dst rects is the GDI+ idiom for
        # crop-and-resize in a single pass.  No intermediate bitmap, no
        # double-downscale quality loss.
        $dstRect = New-Object System.Drawing.Rectangle(0, 0, $TargetSize, $TargetSize)
        $g.DrawImage(
            $Source,
            $dstRect,
            $OffsetX, $OffsetY, $CropSize, $CropSize,
            [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $g.Dispose()
    }

    return ,$bmp
}

# Render each size to an in-memory PNG.
$pngBlobs = New-Object 'System.Collections.Generic.List[object]'
foreach ($s in $sizes) {
    $bmp = New-IconBitmap -Source $src -CropSize $cropSize -OffsetX $cropOffsetX -OffsetY $cropOffsetY -TargetSize $s
    try {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bytes = $ms.ToArray()
        $ms.Dispose()
        $pngBlobs.Add([pscustomobject] @{ Size = $s; Bytes = $bytes })
        Write-Host ("  {0,3}x{0,-3}  {1,7:N0} bytes" -f $s, $bytes.Length)
    }
    finally {
        $bmp.Dispose()
    }
}
$src.Dispose()

# Pack into ICO container.  Header = 6 bytes; per-entry = 16 bytes; PNG payloads tail.
[int] $count        = $pngBlobs.Count
[int] $headerSize   = 6
[int] $dirEntrySize = 16
[int] $dirSize      = $count * $dirEntrySize
$out = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter($out)

# ICONDIR header
$bw.Write([UInt16] 0)     # reserved
$bw.Write([UInt16] 1)     # type 1 = ICO
$bw.Write([UInt16] $count)

# ICONDIRENTRY[count]
[int] $dataOffset = $headerSize + $dirSize
foreach ($blob in $pngBlobs) {
    # 256 wraps to 0 in the 1-byte width/height field per spec.
    [int] $w = if ($blob.Size -eq 256) { 0 } else { $blob.Size }
    $bw.Write([Byte]   $w)
    $bw.Write([Byte]   $w)
    $bw.Write([Byte]   0)        # palette colour count, 0 for true colour
    $bw.Write([Byte]   0)        # reserved
    $bw.Write([UInt16] 1)        # colour planes
    $bw.Write([UInt16] 32)       # bits per pixel
    $bw.Write([UInt32] $blob.Bytes.Length)
    $bw.Write([UInt32] $dataOffset)
    $dataOffset = $dataOffset + $blob.Bytes.Length
}

# PNG payloads
foreach ($blob in $pngBlobs) {
    $bw.Write($blob.Bytes)
}

$bw.Flush()
$bytes = $out.ToArray()
$out.Dispose()

# Write atomically -- temp + rename so a partial write doesn't leave a corrupt .ico.
$dir = Split-Path -Parent $OutputPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$tmp = "$OutputPath.tmp"
[System.IO.File]::WriteAllBytes($tmp, $bytes)
Move-Item -Force $tmp $OutputPath

Write-Host ""
Write-Host ("Wrote {0:N0} bytes to {1}" -f $bytes.Length, $OutputPath)

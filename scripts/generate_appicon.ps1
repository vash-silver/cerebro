#requires -Version 5.1

<#
.SYNOPSIS
    Generate MarvelHeroes.DpsMeter\AppIcon.ico from a programmatic lightning-bolt design.

.DESCRIPTION
    Renders the same artwork at every standard icon size (16, 24, 32, 48, 64, 128, 256),
    packs the PNG payloads into a modern PNG-in-ICO container, and overwrites
    MarvelHeroes.DpsMeter\AppIcon.ico.

    Deterministic -- running twice produces the same bytes (no anti-alias dither RNG and
    no timestamp embedded), so re-running on CI doesn't churn the commit.

    Design:
      * Dark rounded-square tile  (#1B1B1B)        -- matches the splinter banner bg
      * Orange lightning bolt     (#FFB347)        -- "DPS" iconography + the app accent
      * Faint orange edge stroke                    -- gives it presence at 16x16
      * No text -- glyphs render unevenly at small sizes; a shape holds up better

.NOTES
    System.Drawing.Common is Windows-only but this project already targets net8.0-windows
    so there's no portability constraint to worry about.

    Defensive type-casting throughout -- PowerShell 5.1 is finicky about choosing
    between [Math]::Max overloads when one arg is an int and the other is a double, and
    `$a * $b` falls back to reflection-based op_Multiply when PS can't infer the type.
    We force [double] everywhere arithmetic happens.
#>

[CmdletBinding()]
param(
    [string] $OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'MarvelHeroes.DpsMeter\AppIcon.ico')
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

# Sizes to include in the ICO container.  16/24/32 cover taskbar + tray, 48 is Alt+Tab,
# 64/128/256 cover Explorer "large icons" / high-DPI scaling.
[int[]] $sizes = 16, 24, 32, 48, 64, 128, 256

# Colours
$bg     = [System.Drawing.Color]::FromArgb(255, 27, 27, 27)     # #1B1B1B
$bolt   = [System.Drawing.Color]::FromArgb(255, 255, 179,  71)  # #FFB347
$stroke = [System.Drawing.Color]::FromArgb(120, 255, 179,  71)  # 47% alpha edge

function New-IconBitmap {
    param([int] $Size)

    [double] $sz = $Size  # keep all arithmetic in doubles
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)

    try {
        $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

        # Inset by 0.5px so the stroke isn't clipped on the bottom-right edge.
        [double] $inset = if ($Size -ge 64) { ($sz * 0.04) } else { 0.5 }
        [double] $w     = $sz - (2.0 * $inset)
        [double] $h     = $sz - (2.0 * $inset)
        $rect           = New-Object System.Drawing.RectangleF([single]$inset, [single]$inset, [single]$w, [single]$h)
        [double] $corner = [Math]::Max([double] 2.0, [double] ($sz * 0.18))
        [double] $diam   = $corner * 2.0

        # Rounded rectangle path -- System.Drawing has no built-in rounded rect.
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddArc([single] $rect.X,                            [single] $rect.Y,                              [single] $diam, [single] $diam, 180, 90)
        $path.AddArc([single] ($rect.X + $rect.Width  - $diam),   [single] $rect.Y,                              [single] $diam, [single] $diam, 270, 90)
        $path.AddArc([single] ($rect.X + $rect.Width  - $diam),   [single] ($rect.Y + $rect.Height - $diam),     [single] $diam, [single] $diam,   0, 90)
        $path.AddArc([single] $rect.X,                            [single] ($rect.Y + $rect.Height - $diam),     [single] $diam, [single] $diam,  90, 90)
        $path.CloseFigure()

        # Tile background.
        $bgBrush = New-Object System.Drawing.SolidBrush($bg)
        try { $g.FillPath($bgBrush, $path) }
        finally { $bgBrush.Dispose() }

        # Lightning bolt -- 7-point polygon in normalised coords.  Designed to read at
        # 16x16: thicker mid-section, sharp tips top + bottom, the classic "Z" zig.
        $pts = New-Object 'System.Drawing.PointF[]' 7
        $pts[0] = New-Object System.Drawing.PointF([single] (0.58 * $sz), [single] (0.08 * $sz))  # top tip
        $pts[1] = New-Object System.Drawing.PointF([single] (0.20 * $sz), [single] (0.55 * $sz))  # bottom-left of upper half
        $pts[2] = New-Object System.Drawing.PointF([single] (0.42 * $sz), [single] (0.55 * $sz))  # inner notch
        $pts[3] = New-Object System.Drawing.PointF([single] (0.30 * $sz), [single] (0.92 * $sz))  # bottom tip
        $pts[4] = New-Object System.Drawing.PointF([single] (0.78 * $sz), [single] (0.40 * $sz))  # top-right of lower half
        $pts[5] = New-Object System.Drawing.PointF([single] (0.54 * $sz), [single] (0.40 * $sz))  # inner notch
        $pts[6] = New-Object System.Drawing.PointF([single] (0.58 * $sz), [single] (0.08 * $sz))  # close

        $boltBrush = New-Object System.Drawing.SolidBrush($bolt)
        try { $g.FillPolygon($boltBrush, $pts) }
        finally { $boltBrush.Dispose() }

        # Subtle stroke around the tile so the icon has presence on a similarly-dark
        # taskbar.  Only applied at >= 32 -- below that, a 1-px stroke eats the bolt.
        if ($Size -ge 32) {
            [double] $strokeWidth = [Math]::Max([double] 1.0, [double] ($sz * 0.012))
            $pen = New-Object System.Drawing.Pen($stroke, [single] $strokeWidth)
            try { $g.DrawPath($pen, $path) }
            finally { $pen.Dispose() }
        }

        $path.Dispose()
    }
    finally {
        $g.Dispose()
    }

    return ,$bmp  # comma keeps PS from unwrapping a single return value
}

# Render each size to an in-memory PNG.
$pngBlobs = New-Object 'System.Collections.Generic.List[object]'
foreach ($s in $sizes) {
    $bmp = New-IconBitmap -Size $s
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

# Pack into ICO container.  Header = 6 bytes; per-entry = 16 bytes; PNG payloads tail.
[int] $count        = $pngBlobs.Count
[int] $headerSize   = 6
[int] $dirEntrySize = 16
[int] $dirSize      = $count * $dirEntrySize
$out = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter($out)

# ICONDIR header
$bw.Write([UInt16] 0)
$bw.Write([UInt16] 1)
$bw.Write([UInt16] $count)

# ICONDIRENTRY[count]
[int] $dataOffset = $headerSize + $dirSize
foreach ($blob in $pngBlobs) {
    # 256 wraps to 0 in the 1-byte width/height field per spec.
    [int] $w = if ($blob.Size -eq 256) { 0 } else { $blob.Size }
    $bw.Write([Byte]   $w)
    $bw.Write([Byte]   $w)
    $bw.Write([Byte]   0)
    $bw.Write([Byte]   0)
    $bw.Write([UInt16] 1)
    $bw.Write([UInt16] 32)
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

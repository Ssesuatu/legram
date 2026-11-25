param(
    [string]$IcoPath = (Join-Path $PSScriptRoot '..\assets\legram.ico'),
    [string]$PngPath = (Join-Path $PSScriptRoot '..\assets\legram-256.png')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2.0
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-LegramBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(0.8, $size * 0.055)
    $box = $size - ($pad * 2.0)
    $rad = $box * 0.27

    # 1. Outer Sculpted Matte Obsidian Squircle Casing
    $outerPath = New-RoundedRectPath $pad $pad $box $box $rad
    $shellBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, 0)),
        (New-Object System.Drawing.PointF(0, $size)),
        [System.Drawing.Color]::FromArgb(255, 34, 36, 44),
        [System.Drawing.Color]::FromArgb(255, 13, 14, 18)
    )
    $g.FillPath($shellBrush, $outerPath)

    $rimPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120, 230, 236, 248), [Math]::Max(1.0, $size * 0.025))
    $g.DrawPath($rimPen, $outerPath)

    # 2. Recessed Inner Optical Well
    $inPad = $pad + ($box * 0.085)
    $inBox = $size - ($inPad * 2.0)
    $inRad = $inBox * 0.24
    $innerPath = New-RoundedRectPath $inPad $inPad $inBox $inRad $inRad
    $innerPath.Dispose()
    $innerPath = New-RoundedRectPath $inPad $inPad $inBox $inBox $inRad
    $wellBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, $inPad)),
        (New-Object System.Drawing.PointF(0, ($inPad + $inBox))),
        [System.Drawing.Color]::FromArgb(255, 14, 15, 19),
        [System.Drawing.Color]::FromArgb(255, 22, 24, 30)
    )
    $g.FillPath($wellBrush, $innerPath)

    # 3. Seamless Prismatic Dichroic Optical Ring (Gentle Monster Lens Coating × Google Spectrum)
    $cx = $size / 2.0
    $cy = $size / 2.0
    $ringR = $size * 0.295
    $ringThick = [Math]::Max(1.6, $size * 0.074)
    $rx = $cx - $ringR
    $ry = $cy - $ringR
    $rd = $ringR * 2.0

    $gradRing = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF($rx, $ry)),
        (New-Object System.Drawing.PointF(($rx + $rd), ($ry + $rd))),
        [System.Drawing.Color]::White,
        [System.Drawing.Color]::White
    )
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend(5)
    $blend.Colors = [System.Drawing.Color[]]@(
        [System.Drawing.Color]::FromArgb(255, 255, 255, 255), # Specular Pearl
        [System.Drawing.Color]::FromArgb(255, 255, 214, 122), # Warm Gold Bevel
        [System.Drawing.Color]::FromArgb(255, 246, 139, 130), # Dichroic Coral
        [System.Drawing.Color]::FromArgb(255, 168, 199, 250), # Google Ice Blue
        [System.Drawing.Color]::FromArgb(255, 124, 227, 161)  # Calm Mint
    )
    $blend.Positions = [float[]]@(0.0, 0.22, 0.45, 0.72, 1.0)
    $gradRing.InterpolationColors = $blend

    $pRing = New-Object System.Drawing.Pen($gradRing, $ringThick)
    $g.DrawEllipse($pRing, $rx, $ry, $rd, $rd)
    $pRing.Dispose()
    $gradRing.Dispose()

    # 4. Inner Smoked Glass Lens Disc
    $coreDiscR = $ringR - ($ringThick * 0.48)
    $discBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF($cx, ($cy - $coreDiscR))),
        (New-Object System.Drawing.PointF($cx, ($cy + $coreDiscR))),
        [System.Drawing.Color]::FromArgb(255, 38, 41, 50),
        [System.Drawing.Color]::FromArgb(255, 16, 17, 22)
    )
    $g.FillEllipse($discBrush, ($cx - $coreDiscR), ($cy - $coreDiscR), ($coreDiscR * 2.0), ($coreDiscR * 2.0))

    # 5. Precision 3-Bar Vertical Optical Telemetry Mark inside the Lens
    if ($size -ge 20) {
        $barW = [Math]::Max(1.4, $size * 0.048)
        $gap = $size * 0.088
        $hLeft  = $size * 0.13
        $hMid   = $size * 0.23
        $hRight = $size * 0.16

        $penWhite = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(245, 247, 252), $barW)
        $penWhite.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $penWhite.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

        $penAccent = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 129, 201, 149), $barW)
        $penAccent.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $penAccent.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

        $g.DrawLine($penWhite,  ($cx - $gap), ($cy - $hLeft * 0.5),  ($cx - $gap), ($cy + $hLeft * 0.5))
        $g.DrawLine($penAccent, $cx,          ($cy - $hMid * 0.5),   $cx,          ($cy + $hMid * 0.5))
        $g.DrawLine($penWhite,  ($cx + $gap), ($cy - $hRight * 0.5), ($cx + $gap), ($cy + $hRight * 0.5))

        $penWhite.Dispose()
        $penAccent.Dispose()
    } else {
        # Crisp pearl core at 16x16
        $dotR = $size * 0.11
        $dotB = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 245, 247, 252))
        $g.FillEllipse($dotB, ($cx - $dotR), ($cy - $dotR), ($dotR * 2.0), ($dotR * 2.0))
        $dotB.Dispose()
    }

    $discBrush.Dispose()
    $wellBrush.Dispose()
    $innerPath.Dispose()
    $rimPen.Dispose()
    $shellBrush.Dispose()
    $outerPath.Dispose()
    $g.Dispose()
    return $bmp
}

$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
$pngBuffers = New-Object System.Collections.Generic.List[byte[]]

$dir = Split-Path -Parent ([IO.Path]::GetFullPath($IcoPath))
New-Item -ItemType Directory -Force $dir | Out-Null

foreach ($sz in $sizes) {
    $bmp = New-LegramBitmap $sz
    if ($sz -eq 256) {
        $bmp.Save([IO.Path]::GetFullPath($PngPath), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBuffers.Add($ms.ToArray())
    $ms.Dispose()
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create([IO.Path]::GetFullPath($IcoPath))
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $data = $pngBuffers[$i]
    $wByte = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }
    $bw.Write($wByte)
    $bw.Write($wByte)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$data.Length)
    $bw.Write([uint32]$offset)
    $offset += $data.Length
}

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $bw.Write($pngBuffers[$i])
}
$bw.Flush()
$bw.Close()
$fs.Close()
Write-Output "Generated Legram multi-size icon: $IcoPath"

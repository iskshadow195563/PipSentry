param([string]$OutDir = (Join-Path $PSScriptRoot "../src/PipSentry/Assets"))
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function New-AppBitmap([int]$Size) {
    $bmp = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    [double]$pad = $Size * 0.055
    [double]$inner = $Size - (2.0 * $pad)
    $rect = [System.Drawing.RectangleF]::new([single]$pad, [single]$pad, [single]$inner, [single]$inner)
    [single]$radius = [single]($Size * 0.24)
    [single]$d = $radius * 2.0
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $c1 = [System.Drawing.Color]::FromArgb(255, 63, 210, 245)
    $c2 = [System.Drawing.Color]::FromArgb(255, 76, 125, 255)
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($rect, $c1, $c2, [single]45.0)
    $g.FillPath($brush, $path)

    $inkColor = [System.Drawing.Color]::FromArgb(255, 6, 19, 28)
    $ink = [System.Drawing.SolidBrush]::new($inkColor)
    [single]$penW = [single]($Size * 0.052)
    $pen = [System.Drawing.Pen]::new($inkColor, $penW)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    [single]$mx = [single]($Size * 0.23)
    [single]$my = [single]($Size * 0.26)
    [single]$mw = [single]($Size * 0.54)
    [single]$mh = [single]($Size * 0.33)
    $screenRect = [System.Drawing.RectangleF]::new($mx, $my, $mw, $mh)
    $g.DrawRectangle($pen, $mx, $my, $mw, $mh)

    [single]$sx = [single]($Size * 0.5)
    [single]$standTop = $my + $mh
    [single]$standBottom = $standTop + ($Size * 0.075)
    $g.DrawLine($pen, $sx, $standTop, $sx, $standBottom)
    $g.DrawLine($pen, [single]($Size * 0.39), $standBottom, [single]($Size * 0.61), $standBottom)

    [single]$px = [single]($Size * 0.52)
    [single]$py = [single]($Size * 0.53)
    [single]$pw = [single]($Size * 0.28)
    [single]$ph = [single]($Size * 0.22)
    [single]$rr = [single]($Size * 0.055)
    $pipRect = [System.Drawing.RectangleF]::new($px, $py, $pw, $ph)
    $small = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $small.AddArc($px, $py, $rr*2, $rr*2, 180, 90)
    $small.AddArc(($px + $pw - $rr*2), $py, $rr*2, $rr*2, 270, 90)
    $small.AddArc(($px + $pw - $rr*2), ($py + $ph - $rr*2), $rr*2, $rr*2, 0, 90)
    $small.AddArc($px, ($py + $ph - $rr*2), $rr*2, $rr*2, 90, 90)
    $small.CloseFigure()
    $g.FillPath($ink, $small)
    $rimPen = [System.Drawing.Pen]::new($c1, [single]($Size * 0.02))
    $g.DrawPath($rimPen, $small)

    $g.Dispose()
    return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-AppBitmap -Size $s
    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,$ms.ToArray()
    if ($s -eq 256) {
        $bmp.Save((Join-Path $OutDir "logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $bmp.Dispose()
    $ms.Dispose()
}

$icoPath = Join-Path $OutDir "app.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = [System.IO.BinaryWriter]::new($fs)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $data = $pngs[$i]
    [byte]$dim = 0
    if ($s -lt 256) { $dim = [byte]$s }
    $bw.Write($dim)
    $bw.Write($dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($data in $pngs) { $bw.Write($data) }
$bw.Flush()
$bw.Close()
$fs.Close()
Write-Output ("ico bytes: " + (Get-Item $icoPath).Length)

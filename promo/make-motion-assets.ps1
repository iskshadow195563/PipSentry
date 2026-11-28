Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"
$a = "D:/double_window/promo/assets"

function Canvas([int]$w, [int]$h) {
    $b = [System.Drawing.Bitmap]::new($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    @{ B = $b; G = $g }
}

# cursor: white arrow with dark outline
$c = Canvas 64 64
$pts = @(
    [System.Drawing.PointF]::new(8, 4),
    [System.Drawing.PointF]::new(8, 46),
    [System.Drawing.PointF]::new(18, 36),
    [System.Drawing.PointF]::new(26, 56),
    [System.Drawing.PointF]::new(34, 52),
    [System.Drawing.PointF]::new(26, 33),
    [System.Drawing.PointF]::new(42, 31)
)
$path = [System.Drawing.Drawing2D.GraphicsPath]::new()
$path.AddPolygon($pts)
$shadow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(90, 0, 0, 0))
$c.G.TranslateTransform(2, 2); $c.G.FillPath($shadow, $path); $c.G.ResetTransform()
$white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(220, 10, 10, 14), 2)
$c.G.FillPath($white, $path)
$c.G.DrawPath($pen, $path)
$c.G.Dispose(); $c.B.Save((Join-Path $a "cursor.png"), [System.Drawing.Imaging.ImageFormat]::Png); $c.B.Dispose()

# ripple frames
for ($i = 0; $i -lt 12; $i++) {
    $r = 14 + $i * 9
    $alpha = [int](200 * (1 - $i / 12.0))
    $s = 240
    $canvas = Canvas $s $s
    $pen2 = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb($alpha, 34, 211, 238), 3)
    $canvas.G.DrawEllipse($pen2, [single](($s - $r * 2) / 2), [single](($s - $r * 2) / 2), [single]($r * 2), [single]($r * 2))
    $pen2.Dispose(); $canvas.G.Dispose()
    $canvas.B.Save((Join-Path $a ("ripple_{0:D2}.png" -f $i)), [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.B.Dispose()
}

# highlight ring for the start button
$ring = Canvas 460 150
$pen3 = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(230, 34, 211, 238), 4)
$ring.G.DrawEllipse($pen3, 6, 6, 448, 138)
$pen3.Dispose(); $ring.G.Dispose()
$ring.B.Save((Join-Path $a "ring.png"), [System.Drawing.Imaging.ImageFormat]::Png); $ring.B.Dispose()

# extra captions
Add-Type -AssemblyName System.Drawing
function TextPng([string]$name, [string]$text, [single]$size, [string]$hex) {
    $font = [System.Drawing.Font]::new("Microsoft YaHei UI", $size, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $probe = Canvas 8 8
    $m = $probe.G.MeasureString($text, $font, 4000)
    $probe.G.Dispose(); $probe.B.Dispose()
    $w = [int]$m.Width + 60; $h = [int]$m.Height + 30
    $canvas = Canvas $w $h
    $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($hex))
    $canvas.G.DrawString($text, $font, $brush, 30, 15)
    $canvas.G.Dispose(); $canvas.B.Save((Join-Path $a $name), [System.Drawing.Imaging.ImageFormat]::Png); $canvas.B.Dispose()
}
TextPng "t_resize.png" "拖一下，就能改大小" 46 "#E5E7EB"
TextPng "t_region.png" "框一块，只看这一块" 46 "#E5E7EB"
TextPng "t_click.png" "点这里开始" 40 "#22D3EE"

"assets ok"

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"
$root = "D:/double_window/promo"
$assets = Join-Path $root "assets"
New-Item -ItemType Directory -Force -Path $assets | Out-Null

$SW = 1920
$SH = 1080
$fontName = "Microsoft YaHei UI"

function New-Canvas([int]$cw, [int]$ch, [bool]$transparent) {
    $bmp = [System.Drawing.Bitmap]::new($cw, $ch, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    if ($transparent) { $g.Clear([System.Drawing.Color]::Transparent) }
    return @{ Bitmap = $bmp; Graphics = $g }
}

function New-TextFont([single]$emSize, [bool]$bold) {
    $style = if ($bold) { [System.Drawing.FontStyle]::Bold } else { [System.Drawing.FontStyle]::Regular }
    return [System.Drawing.Font]::new($fontName, $emSize, $style, [System.Drawing.GraphicsUnit]::Pixel)
}

function New-TextPng([string]$file, [string]$text, [single]$emSize, [string]$hex, [bool]$bold) {
    $font = New-TextFont $emSize $bold
    $probe = New-Canvas 8 8 $false
    $measure = $probe.Graphics.MeasureString($text, $font, 4000)
    $probe.Graphics.Dispose(); $probe.Bitmap.Dispose()

    $pad = 30
    $cw = [int][Math]::Ceiling($measure.Width) + $pad * 2
    $ch = [int][Math]::Ceiling($measure.Height) + $pad
    $canvas = New-Canvas $cw $ch $true
    $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($hex))
    $canvas.Graphics.DrawString($text, $font, $brush, $pad, $pad / 2)
    $canvas.Graphics.Dispose(); $brush.Dispose(); $font.Dispose()
    $canvas.Bitmap.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Bitmap.Dispose()
}

# stage background
$stage = New-Canvas $SW $SH $false
$stage.Graphics.Clear([System.Drawing.Color]::FromArgb(255, 5, 5, 8))

# purple horizon: stacked translucent bands, stronger towards the bottom
for ($i = 0; $i -lt 320; $i++) {
    $ratio = $i / 320.0
    $alpha = [int](90 * $ratio * $ratio)
    $bandColor = [System.Drawing.Color]::FromArgb($alpha, 124, 58, 237)
    $band = [System.Drawing.SolidBrush]::new($bandColor)
    $stage.Graphics.FillRectangle($band, 0, $SH - 320 + $i, $SW, 1)
    $band.Dispose()
}

# stars
$rnd = [System.Random]::new(20261128)
for ($i = 0; $i -lt 240; $i++) {
    $x = $rnd.Next(0, $SW)
    $y = $rnd.Next(0, [int]($SH * 0.82))
    $d = 0.6 + $rnd.NextDouble() * 1.8
    $a = [int](10 + $rnd.NextDouble() * 65)
    $star = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb($a, 255, 255, 255))
    $stage.Graphics.FillEllipse($star, [single]$x, [single]$y, [single]$d, [single]$d)
    $star.Dispose()
}

# vignette rings
for ($i = 0; $i -lt 36; $i++) {
    $a = [int](1 + $i * 0.6)
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb($a, 0, 0, 0), 80)
    $stage.Graphics.DrawRectangle($pen, -$i * 10, -$i * 10, $SW + $i * 20, $SH + $i * 20)
    $pen.Dispose()
}

$stage.Graphics.Dispose()
$stage.Bitmap.Save((Join-Path $assets "bg.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$stage.Bitmap.Dispose()

New-TextPng (Join-Path $assets "t1.png") "挂机的时候，你不用一直切回去" 74 "#FFFFFF" $true
New-TextPng (Join-Path $assets "w1.png") "切窗口" 96 "#FFFFFF" $true
New-TextPng (Join-Path $assets "w2.png") "被打断" 96 "#FFFFFF" $true
New-TextPng (Join-Path $assets "w3.png") "看不到进度" 96 "#FFFFFF" $true
New-TextPng (Join-Path $assets "t2.png") "选一个窗口，框一块画面，钉在最上层" 54 "#E5E7EB" $false
New-TextPng (Join-Path $assets "t3.png") "它抓的是窗口，不是你整个屏幕" 52 "#E5E7EB" $false
New-TextPng (Join-Path $assets "t4.png") "在 GPU 上缩放，只把要看的那块搬回来" 50 "#E5E7EB" $false
New-TextPng (Join-Path $assets "t4b.png") "一块画面就够" 72 "#FFFFFF" $true
New-TextPng (Join-Path $assets "t5.png") "更新，由你决定" 58 "#E5E7EB" $false
New-TextPng (Join-Path $assets "t5b.png") "启动只提示，不自动更新" 38 "#9AA3AF" $false
New-TextPng (Join-Path $assets "t6.png") "把画面钉在最上层" 82 "#FFFFFF" $true
New-TextPng (Join-Path $assets "t7.png") "免费・开源・单文件免安装" 38 "#9AA3AF" $false

Get-ChildItem $assets -Filter *.png | Select-Object Name, @{n="KB";e={[math]::Round($_.Length/1KB,1)}} | Format-Table -AutoSize
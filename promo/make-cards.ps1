Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"
$assets = "D:/double_window/promo/assets"
$ico = "D:/double_window/src/PipSentry/Assets/app.ico"
$fontName = "Microsoft YaHei UI"

function Canvas([int]$w, [int]$h, [bool]$alpha) {
    $b = [System.Drawing.Bitmap]::new($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    if ($alpha) { $g.Clear([System.Drawing.Color]::Transparent) }
    @{ B = $b; G = $g }
}

function Font([single]$size, [bool]$bold) {
    $s = if ($bold) { [System.Drawing.FontStyle]::Bold } else { [System.Drawing.FontStyle]::Regular }
    [System.Drawing.Font]::new($fontName, $size, $s, [System.Drawing.GraphicsUnit]::Pixel)
}

function RoundPath([single]$w, [single]$h, [single]$r) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $r * 2
    $p.AddArc(0, 0, $d, $d, 180, 90)
    $p.AddArc($w - $d, 0, $d, $d, 270, 90)
    $p.AddArc($w - $d, $h - $d, $d, $d, 0, 90)
    $p.AddArc(0, $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

function Save($canvas, [string]$name) {
    $canvas.G.Dispose()
    $canvas.B.Save((Join-Path $assets $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.B.Dispose()
}

# logo mark: rounded gradient square with a screen glyph
$logoCanvas = Canvas 256 256 $true
$logoPath = RoundPath 256 256 64
$logoGrad = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(0, 0, 256, 256), [System.Drawing.Color]::FromArgb(255, 34, 211, 238), [System.Drawing.Color]::FromArgb(255, 232, 121, 249), [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
$logoCanvas.G.FillPath($logoGrad, $logoPath)
$screenPath = RoundPath 168 112 20
$screenBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(245, 8, 8, 14))
$logoCanvas.G.TranslateTransform(44, 62)
$logoCanvas.G.FillPath($screenBrush, $screenPath)
$logoCanvas.G.ResetTransform()
$dot = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 8, 8, 14))
$logoCanvas.G.FillEllipse($dot, 44, 190, 34, 34)
Save $logoCanvas "logo.png"

# CTA pill with cyan to magenta gradient
$cta = Canvas 360 88 $true
$path = RoundPath 360 88 44
$grad = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(0, 0, 360, 88), [System.Drawing.Color]::FromArgb(255, 34, 211, 238), [System.Drawing.Color]::FromArgb(255, 232, 121, 249), [System.Drawing.Drawing2D.LinearGradientMode]::Horizontal)
$cta.G.FillPath($grad, $path)
$f = Font 32 $true
$tb = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$fmt = [System.Drawing.StringFormat]::new()
$fmt.Alignment = [System.Drawing.StringAlignment]::Center
$fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
$cta.G.DrawString("开始监控", $f, $tb, [System.Drawing.RectangleF]::new(0, 0, 360, 88), $fmt)
Save $cta "cta.png"

# capability pills
$pills = @("任意窗口", "局部框选", "4K 60 FPS", "GPU 缩放", "全局热键", "版本选择")
$i = 1
foreach ($text in $pills) {
    $f = Font 28 $false
    $probe = Canvas 8 8 $false
    $m = $probe.G.MeasureString($text, $f, 2000)
    $probe.G.Dispose(); $probe.B.Dispose()
    $w = [int]($m.Width + 70); $h = 66
    $c = Canvas $w $h $true
    $p = RoundPath $w $h 33
    $fill = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(235, 18, 18, 24))
    $c.G.FillPath($fill, $p)
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(120, 34, 211, 238), 1.5)
    $c.G.DrawPath($pen, $p)
    $c.G.DrawString($text, $f, $tb, [single]35, [single]18)
    Save $c ("pill_$i.png")
    $i++
}

# proof numbers
$numbers = @("1,637 MB/s|4K 全窗口 60fps 的 CPU↔GPU 流量", "35 MB/s|同样画面，只搬要看的", "8.9%|4K 30fps 的单核占用", "0.47%|GPU 3D 引擎", "26 fps|4K 全窗口，仍稳定出帧")
$i = 1
foreach ($entry in $numbers) {
    $parts = $entry.Split("|")
    $num = $parts[0]; $label = $parts[1]
    $fn = Font 96 $true
    $fl = Font 26 $false
    $probe = Canvas 8 8 $false
    $mn = $probe.G.MeasureString($num, $fn, 2000)
    $ml = $probe.G.MeasureString($label, $fl, 2000)
    $probe.G.Dispose(); $probe.B.Dispose()
    $w = [int][Math]::Max($mn.Width, $ml.Width) + 40
    $h = [int]($mn.Height + $ml.Height + 20)
    $c = Canvas $w $h $true
    $c.G.DrawString($num, $fn, $tb, [single]20, [single]0)
    $muted = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 154, 163, 175))
    $c.G.DrawString($label, $fl, $muted, [single]22, [single]($mn.Height + 4))
    Save $c ("num_$i.png")
    $i++
}

# end card
$bg = [System.Drawing.Image]::FromFile((Join-Path $assets "bg.png"))
$end = Canvas 1920 1080 $false
$end.G.DrawImage($bg, 0, 0, 1920, 1080)
$bg.Dispose()
$logoImage = [System.Drawing.Image]::FromFile((Join-Path $assets "logo.png"))
$end.G.DrawImage($logoImage, 880, 236, 160, 160)
$logoImage.Dispose()
$fh = Font 64 $true
$fs = Font 40 $false
$fm = Font 26 $false
$fmt2 = [System.Drawing.StringFormat]::new()
$fmt2.Alignment = [System.Drawing.StringAlignment]::Center
$end.G.DrawString("PipSentry", $fh, $tb, [System.Drawing.RectangleF]::new(0, 440, 1920, 90), $fmt2)
$end.G.DrawString("把画面钉在最上层", $fs, $tb, [System.Drawing.RectangleF]::new(0, 540, 1920, 60), $fmt2)
$muted2 = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 154, 163, 175))
$end.G.DrawString("免费・开源・单文件免安装", $fm, $muted2, [System.Drawing.RectangleF]::new(0, 620, 1920, 40), $fmt2)
$end.G.DrawString("github.com/iskshadow195563/PipSentry", $fm, $muted2, [System.Drawing.RectangleF]::new(0, 700, 1920, 40), $fmt2)
Save $end "endcard.png"

# typing frames for scene 1
Remove-Item (Join-Path $assets "type_*.png") -Force -ErrorAction SilentlyContinue
$text = "挂机的时候，你不用一直切回去"
$bg2 = [System.Drawing.Image]::FromFile((Join-Path $assets "bg.png"))
$frameFont = Font 74 $true
$n = $text.Length
for ($k = 1; $k -le $n; $k++) {
    $c = Canvas 1920 1080 $false
    $c.G.DrawImage($bg2, 0, 0, 1920, 1080)
    $prefix = $text.Substring(0, $k)
    $c.G.DrawString($prefix, $frameFont, $tb, [System.Drawing.RectangleF]::new(0, 470, 1920, 140), $fmt2)
    Save $c ("type_{0:D2}.png" -f $k)
}
$bg2.Dispose()

# cover 1146x717
$cover = Canvas 1146 717 $false
$bg3 = [System.Drawing.Image]::FromFile((Join-Path $assets "bg.png"))
$cover.G.DrawImage($bg3, 0, 0, 1146, 717)
$bg3.Dispose()
$logoImage2 = [System.Drawing.Image]::FromFile((Join-Path $assets "logo.png"))
$cover.G.DrawImage($logoImage2, 70, 70, 96, 96)
$logoImage2.Dispose()
$cf = Font 44 $true
$cs = Font 56 $true
$cm = Font 24 $false
$cover.G.DrawString("PipSentry", $cf, $tb, [single]185, [single]92)
$cover.G.DrawString("把游戏画面", $cs, $tb, [single]70, [single]250)
$cover.G.DrawString("钉在最上层", $cs, $tb, [single]70, [single]330)
$cover.G.DrawString("挂机不用一直切回去・免费开源", $cm, $muted2, [single]74, [single]440)
$cover.G.DrawString("Windows 10 / 11 ・ 单文件免安装", $cm, $muted2, [single]74, [single]484)
Save $cover "cover.png"

Get-ChildItem $assets -Filter *.png | Measure-Object | Select-Object Count
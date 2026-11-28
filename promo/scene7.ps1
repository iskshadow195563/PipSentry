$ErrorActionPreference = "Stop"
$a = "D:/double_window/promo/assets"
$out = "D:/double_window/promo/out"
& ffmpeg -hide_banner -loglevel error -y -loop 1 -t 5.5 -i (Join-Path $a "endcard.png") -vf "scale=w='1920*(1+0.025*t/5.5)':h=-1:eval=frame,crop=1920:1080,fade=t=in:st=0:d=0.6" -r 30 -t 5.5 -c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p (Join-Path $out "s7.mp4")
if ($LASTEXITCODE -ne 0) { throw "s7 failed" }
"s7 ok"

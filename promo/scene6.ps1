$ErrorActionPreference = "Stop"
$a = "D:/double_window/promo/assets"
$out = "D:/double_window/promo/out"
$fx = "[0:v]scale=1920:1080,setsar=1[bg];" +
      "[1:v]format=rgba,fade=t=in:st=0.2:d=0.5:alpha=1[cap];[bg][cap]overlay=140:210[v1];" +
      "[2:v]scale=800:-1,format=rgba[shot];[v1][shot]overlay=x=1040:y='200+3*sin(2*PI*t*0.3)':enable='gte(t,0.3)'[v2];" +
      "[3:v]format=rgba,fade=t=in:st=1.0:d=0.4:alpha=1[p6];[v2][p6]overlay=x=100:y=9999:enable='gte(t,999)'[v3];" +
      "[4:v]format=rgba,fade=t=in:st=1.4:d=0.4:alpha=1[sub];[v3][sub]overlay=x=140:y=380:enable='gte(t,1.0)'[v4]"
& ffmpeg -hide_banner -loglevel error -y -loop 1 -t 6 -i (Join-Path $a "bg.png") -loop 1 -t 6 -i (Join-Path $a "t5.png") -loop 1 -t 6 -i (Join-Path $a "settings.png") -loop 1 -t 6 -i (Join-Path $a "pill_6.png") -loop 1 -t 6 -i (Join-Path $a "t5b.png") -filter_complex $fx -map "[v4]" -r 30 -t 6 -c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p (Join-Path $out "s6.mp4")
if ($LASTEXITCODE -ne 0) { throw "s6 failed" }
"s6 ok"
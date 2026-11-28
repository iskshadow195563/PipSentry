$ErrorActionPreference = "Stop"
$a = "D:/double_window/promo/assets"
$out = "D:/double_window/promo/out"
$fx = "[0:v]scale=1920:1080,setsar=1[bg];" +
      "[1:v]scale=1240:-1,format=rgba[mon];" +
      "[bg][mon]overlay=x=(W-w)/2:y='120+4*sin(2*PI*t*0.25)'[v1];" +
      "[2:v]format=rgba,fade=t=in:st=0.6:d=0.5:alpha=1[cap];" +
      "[v1][cap]overlay=(W-w)/2:900[v2];" +
      "[3:v]format=rgba,fade=t=in:st=1.4:d=0.4:alpha=1[p1];" +
      "[v2][p1]overlay=x='120+20*sin(2*PI*(t-1.4)*0.3)':y=812:enable='gte(t,1.4)'[v3];" +
      "[4:v]format=rgba,fade=t=in:st=1.9:d=0.4:alpha=1[p2];" +
      "[v3][p2]overlay=x='720+18*sin(2*PI*(t-1.9)*0.3+1)':y=812:enable='gte(t,1.9)'[v4];" +
      "[5:v]format=rgba,fade=t=in:st=2.4:d=0.4:alpha=1[p3];" +
      "[v4][p3]overlay=x='1330+16*sin(2*PI*(t-2.4)*0.3+2)':y=812:enable='gte(t,2.4)'[v5]"
& ffmpeg -hide_banner -loglevel error -y -loop 1 -t 7 -i (Join-Path $a "bg.png") -loop 1 -t 7 -i (Join-Path $a "monitor.png") -loop 1 -t 7 -i (Join-Path $a "t3.png") -loop 1 -t 7 -i (Join-Path $a "pill_1.png") -loop 1 -t 7 -i (Join-Path $a "pill_2.png") -loop 1 -t 7 -i (Join-Path $a "pill_3.png") -filter_complex $fx -map "[v5]" -r 30 -t 7 -c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p (Join-Path $out "s3.mp4")
if ($LASTEXITCODE -ne 0) { throw "s3 failed" }
"s3 ok"

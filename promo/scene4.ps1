$ErrorActionPreference = "Stop"
$a = "D:/double_window/promo/assets"
$out = "D:/double_window/promo/out"
$fx = "[0:v]scale=1920:1080,setsar=1[bg];" +
      "[1:v]format=rgba,fade=t=in:st=0.2:d=0.5:alpha=1[cap];" +
      "[8:v]format=rgba,fade=t=in:st=0.6:d=0.6:alpha=1[core];" +
      "[bg][cap]overlay=(W-w)/2:120[v0];[v0][core]overlay=(W-w)/2:500[v1];" +
      "[2:v]format=rgba,fade=t=in:st=0.5:d=0.4:alpha=1[p1];[v1][p1]overlay=x='960+520*cos(0.35*t)-90':y='600+250*sin(0.35*t)-33':enable='gte(t,0.5)'[v2];" +
      "[3:v]format=rgba,fade=t=in:st=0.8:d=0.4:alpha=1[p2];[v2][p2]overlay=x='960+520*cos(0.35*t+1.05)-90':y='600+250*sin(0.35*t+1.05)-33':enable='gte(t,0.8)'[v3];" +
      "[4:v]format=rgba,fade=t=in:st=1.1:d=0.4:alpha=1[p3];[v3][p3]overlay=x='960+520*cos(0.35*t+2.09)-90':y='600+250*sin(0.35*t+2.09)-33':enable='gte(t,1.1)'[v4];" +
      "[5:v]format=rgba,fade=t=in:st=1.4:d=0.4:alpha=1[p4];[v4][p4]overlay=x='960+520*cos(0.35*t+3.14)-90':y='600+250*sin(0.35*t+3.14)-33':enable='gte(t,1.4)'[v5];" +
      "[6:v]format=rgba,fade=t=in:st=1.7:d=0.4:alpha=1[p5];[v5][p5]overlay=x='960+520*cos(0.35*t+4.19)-90':y='600+250*sin(0.35*t+4.19)-33':enable='gte(t,1.7)'[v6];" +
      "[7:v]format=rgba,fade=t=in:st=2.0:d=0.4:alpha=1[p6];[v6][p6]overlay=x='960+520*cos(0.35*t+5.24)-90':y='600+250*sin(0.35*t+5.24)-33':enable='gte(t,2.0)'[v7]"
& ffmpeg -hide_banner -loglevel error -y -loop 1 -t 7 -i (Join-Path $a "bg.png") -loop 1 -t 7 -i (Join-Path $a "t4.png") -loop 1 -t 7 -i (Join-Path $a "pill_1.png") -loop 1 -t 7 -i (Join-Path $a "pill_2.png") -loop 1 -t 7 -i (Join-Path $a "pill_3.png") -loop 1 -t 7 -i (Join-Path $a "pill_4.png") -loop 1 -t 7 -i (Join-Path $a "pill_5.png") -loop 1 -t 7 -i (Join-Path $a "pill_6.png") -loop 1 -t 7 -i (Join-Path $a "t4b.png") -filter_complex $fx -map "[v7]" -r 30 -t 7 -c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p (Join-Path $out "s4.mp4")
if ($LASTEXITCODE -ne 0) { throw "s4 failed" }
"s4 ok"
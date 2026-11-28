$ErrorActionPreference = "Stop"
$a = "D:/double_window/promo/assets"
$out = "D:/double_window/promo/out"
$fx = "[0:v]scale=1920:1080,setsar=1[bg];" +
      "[1:v]scale=1100:-1,zoompan=z='1+0.16*sin(PI*min(on/96,1))':d=1:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1100x621:fps=30[card];" +
      "[bg][card]overlay=x='(W-w)/2':y='150+40*sin(PI*min(t/3.2,1))'[v1];" +
      "[2:v]format=rgba,fade=t=in:st=0.25:d=0.35:alpha=1[cap];" +
      "[v1][cap]overlay=(W-w)/2:900[v2]"
& ffmpeg -hide_banner -loglevel error -y -loop 1 -t 3.2 -i (Join-Path $a "bg.png") -loop 1 -t 3.2 -i (Join-Path $a "monitor.png") -loop 1 -t 3.2 -i (Join-Path $a "t_resize.png") -filter_complex $fx -map "[v2]" -r 30 -t 3.2 -c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p (Join-Path $out "s3b.mp4")
if ($LASTEXITCODE -ne 0) { throw "s3b failed" }
"s3b ok"

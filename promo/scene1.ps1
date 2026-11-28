$ErrorActionPreference = "Stop"
$a = "D:/double_window/promo/assets"
$out = "D:/double_window/promo/out"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$fx = "[0:v]scale=1920:1080,setsar=1[bg];" +
      "[1:v]format=rgba,tpad=stop_mode=clone:stop_duration=4,setpts=PTS-STARTPTS+0.30/TB[ty];" +
      "[bg][ty]overlay=0:0:enable='between(t,0.30,6.0)'[v1];" +
      "[2:v]format=rgba,fade=t=in:st=2.90:d=0.12:alpha=1[w1];" +
      "[v1][w1]overlay=(W-w)/2:735:enable='between(t,2.90,3.70)'[v2];" +
      "[3:v]format=rgba,fade=t=in:st=3.70:d=0.12:alpha=1[w2];" +
      "[v2][w2]overlay=(W-w)/2:735:enable='between(t,3.70,4.50)'[v3];" +
      "[4:v]format=rgba,fade=t=in:st=4.50:d=0.12:alpha=1[w3];" +
      "[v3][w3]overlay=(W-w)/2:735:enable='between(t,4.50,6.0)'[vout]"

& ffmpeg -hide_banner -loglevel error -y -loop 1 -t 6 -i (Join-Path $a "bg.png") -framerate 8 -i (Join-Path $a "type_%02d.png") -loop 1 -t 6 -i (Join-Path $a "w1.png") -loop 1 -t 6 -i (Join-Path $a "w2.png") -loop 1 -t 6 -i (Join-Path $a "w3.png") -filter_complex $fx -map "[vout]" -r 30 -t 6 -c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p (Join-Path $out "s1.mp4")
if ($LASTEXITCODE -ne 0) { throw "s1 failed" }
"s1 ok"
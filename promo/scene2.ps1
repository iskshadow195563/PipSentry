$ErrorActionPreference = "Stop"
$a = "D:/double_window/promo/assets"
$out = "D:/double_window/promo/out"

# scene 2: 6.5s product shot rises, headline, CTA
$fx = "[0:v]scale=1920:1080,setsar=1[bg];" +
      "[1:v]format=rgba,fade=t=in:st=0.15:d=0.45:alpha=1[head];" +
      "[bg][head]overlay=(W-w)/2:96[v1];" +
      "[2:v]scale=1080:-1,format=rgba[shot];" +
      "[v1][shot]overlay=x=(W-w)/2:y='210+(1-min(max((t-0.20)/0.90,0),1))*320':enable='gte(t,0.20)'[v2];" +
      "[3:v]scale=300:-1,format=rgba,fade=t=in:st=2.60:d=0.30:alpha=1[cta];" +
      "[v2][cta]overlay=x=60:y='880+6*sin(2*PI*(t-2.6)*0.8)':enable='gte(t,2.60)'[v3]"

& ffmpeg -hide_banner -loglevel error -y -loop 1 -t 6.5 -i (Join-Path $a "bg.png") -loop 1 -t 6.5 -i (Join-Path $a "t2.png") -loop 1 -t 6.5 -i (Join-Path $a "shell.png") -loop 1 -t 6.5 -i (Join-Path $a "cta.png") -filter_complex $fx -map "[v3]" -r 30 -t 6.5 -c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p (Join-Path $out "s2.mp4")
if ($LASTEXITCODE -ne 0) { throw "s2 failed" }
"s2 ok"
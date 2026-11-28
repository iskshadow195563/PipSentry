$ErrorActionPreference = "Stop"
$a = "D:/double_window/promo/assets"
$out = "D:/double_window/promo/out"
$fx = "[0:v]scale=1920:1080,setsar=1[bg];" +
      "[1:v]scale=1000:-1,zoompan=z='1+0.06*(on/195)':d=1:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1000x754:fps=30[card];" +
      "[bg][card]overlay=460:200[v1];" +
      "[2:v]format=rgba,fade=t=in:st=0.10:d=0.45:alpha=1[head];" +
      "[v1][head]overlay=(W-w)/2:64[v2];" +
      "[3:v]scale=56:-1[cursor];" +
      "[v2][cursor]overlay=x='760+(1389-760)*min(max((t-0.8)/1.6,0),1)':y='720+(920-720)*min(max((t-0.8)/1.6,0),1)':enable='between(t,0.8,5.4)'[v3];" +
      "[4:v]scale=380:-1[ring];" +
      "[v3][ring]overlay=x=1199:y=860:enable='between(t,2.5,5.4)'[v4];" +
      "[5:v]setpts=PTS-STARTPTS+2.50/TB[rip];" +
      "[v4][rip]overlay=x=1269:y=800:enable='between(t,2.50,2.85)'[v5]"
& ffmpeg -hide_banner -loglevel error -y -loop 1 -t 6.5 -i (Join-Path $a "bg.png") -loop 1 -t 6.5 -i (Join-Path $a "shell.png") -loop 1 -t 6.5 -i (Join-Path $a "t2.png") -loop 1 -t 6.5 -i (Join-Path $a "cursor.png") -loop 1 -t 6.5 -i (Join-Path $a "ring.png") -framerate 60 -i (Join-Path $a "ripple_%02d.png") -filter_complex $fx -map "[v5]" -r 30 -t 6.5 -c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p (Join-Path $out "s2.mp4")
if ($LASTEXITCODE -ne 0) { throw "s2 failed" }
"s2 ok"
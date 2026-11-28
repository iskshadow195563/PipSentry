$ErrorActionPreference = "Stop"
$a = "D:/double_window/promo/assets"
$out = "D:/double_window/promo/out"
$fx = "[0:v]scale=1920:1080,setsar=1[bg];" +
      "[bg]drawbox=x=160:y=500:w='1600*(if(lt(t,1.2),1,max(0.02,1-(t-1.2)/1.4)))':h=8:color=0x22D3EE@0.55:t=fill[v0];" +
      "[1:v]format=rgba,fade=t=in:st=0.3:d=0.5:alpha=1[n1];[v0][n1]overlay=150:260:enable='gte(t,0.3)'[v1];" +
      "[2:v]format=rgba,fade=t=in:st=0.9:d=0.5:alpha=1[n2];[v1][n2]overlay=1080:260:enable='gte(t,0.9)'[v2];" +
      "[3:v]format=rgba,fade=t=in:st=1.7:d=0.5:alpha=1[n3];[v2][n3]overlay=150:600:enable='gte(t,1.7)'[v3];" +
      "[4:v]format=rgba,fade=t=in:st=2.1:d=0.5:alpha=1[n4];[v3][n4]overlay=760:600:enable='gte(t,2.1)'[v4];" +
      "[5:v]format=rgba,fade=t=in:st=2.5:d=0.5:alpha=1[n5];[v4][n5]overlay=1330:600:enable='gte(t,2.5)'[v5]"
& ffmpeg -hide_banner -loglevel error -y -loop 1 -t 8 -i (Join-Path $a "bg.png") -loop 1 -t 8 -i (Join-Path $a "num_1.png") -loop 1 -t 8 -i (Join-Path $a "num_2.png") -loop 1 -t 8 -i (Join-Path $a "num_3.png") -loop 1 -t 8 -i (Join-Path $a "num_4.png") -loop 1 -t 8 -i (Join-Path $a "num_5.png") -filter_complex $fx -map "[v5]" -r 30 -t 8 -c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p (Join-Path $out "s5.mp4")
if ($LASTEXITCODE -ne 0) { throw "s5 failed" }
"s5 ok"

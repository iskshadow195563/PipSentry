$ErrorActionPreference = "Stop"
$a = "D:/double_window/promo/assets"
$out = "D:/double_window/promo/out"
$fx = "[0:v]scale=1920:1080,setsar=1[bg];" +
      "[1:v]scale=1240:-1,zoompan=z='if(lt(on,24),0.86+0.14*(on/24),1.0+0.05*min((on-24)/171,1))':d=1:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1240x700:fps=30[card];" +
      "[bg][card]overlay=340:130[v1];" +
      "[v1]drawbox=x=470:y=250:w='380*min(max((t-1.15)/0.9,0),1)':h='230*min(max((t-1.15)/0.9,0),1)':color=0x22D3EE@0.95:t=3[v2];" +
      "[2:v]format=rgba,fade=t=in:st=1.0:d=0.45:alpha=1[cap];" +
      "[v2][cap]overlay=(W-w)/2:900[v3]"
& ffmpeg -hide_banner -loglevel error -y -loop 1 -t 6.5 -i (Join-Path $a "bg.png") -loop 1 -t 6.5 -i (Join-Path $a "monitor.png") -loop 1 -t 6.5 -i (Join-Path $a "t_region.png") -filter_complex $fx -map "[v3]" -r 30 -t 6.5 -c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p (Join-Path $out "s3.mp4")
if ($LASTEXITCODE -ne 0) { throw "s3 failed" }
"s3 ok"

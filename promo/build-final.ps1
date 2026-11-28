$ErrorActionPreference = "Stop"
$o = "D:/double_window/promo/out"
$p = "D:/double_window/promo"

$fx = "[0:v][1:v]xfade=transition=wipeleft:duration=0.35:offset=5.65[x1];" +
      "[x1][2:v]xfade=transition=wipeleft:duration=0.35:offset=11.80[x2];" +
      "[x2][3:v]xfade=transition=wipeleft:duration=0.35:offset=17.95[x3];" +
      "[x3][4:v]xfade=transition=wipeleft:duration=0.35:offset=20.80[x4];" +
      "[x4][5:v]xfade=transition=wipeleft:duration=0.35:offset=27.45[x5];" +
      "[x5][6:v]xfade=transition=wipeleft:duration=0.35:offset=35.10[x6];" +
      "[x6][7:v]xfade=transition=wipeleft:duration=0.35:offset=40.75[x7];" +
      "[8:a]lowpass=f=14000,loudnorm=I=-15:TP=-1.5:LRA=9,afade=t=out:st=44.4:d=1.8,apad[aout]"

& ffmpeg -hide_banner -loglevel error -y -i (Join-Path $o "s1.mp4") -i (Join-Path $o "s2.mp4") -i (Join-Path $o "s3.mp4") -i (Join-Path $o "s3b.mp4") -i (Join-Path $o "s4.mp4") -i (Join-Path $o "s5.mp4") -i (Join-Path $o "s6.mp4") -i (Join-Path $o "s7.mp4") -i (Join-Path $p "assets/music.wav") -filter_complex $fx -map "[x7]" -map "[aout]" -t 46.25 -r 30 -c:v libx264 -crf 18 -preset medium -pix_fmt yuv420p -c:a aac -b:a 192k (Join-Path $p "PipSentry-promo-1080p.mp4")
if ($LASTEXITCODE -ne 0) { throw "concat failed" }
"final ok"
ffprobe -v error -show_entries format=duration,size -of default=nw=1 (Join-Path $p "PipSentry-promo-1080p.mp4")
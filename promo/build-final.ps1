$ErrorActionPreference = "Stop"
$o = "D:/double_window/promo/out"
$p = "D:/double_window/promo"

$fx = "[0:v][1:v]xfade=transition=wipeleft:duration=0.35:offset=5.65[x1];" +
      "[x1][2:v]xfade=transition=wipeleft:duration=0.35:offset=11.80[x2];" +
      "[x2][3:v]xfade=transition=wipeleft:duration=0.35:offset=18.45[x3];" +
      "[x3][4:v]xfade=transition=wipeleft:duration=0.35:offset=25.10[x4];" +
      "[x4][5:v]xfade=transition=wipeleft:duration=0.35:offset=32.75[x5];" +
      "[x5][6:v]xfade=transition=wipeleft:duration=0.35:offset=38.40[x6];" +
      "[7:a]volume=0.14,lowpass=f=700,afade=t=in:st=0:d=1.2,afade=t=out:st=41.6:d=2.2[pad];" +
      "[8:a]asplit=6[n1][n2][n3][n4][n5][n6];" +
      "[n1]volume=0.22,afade=t=in:st=0:d=0.06,afade=t=out:st=0.3:d=0.15,adelay=5600|5600[w1];" +
      "[n2]volume=0.22,afade=t=in:st=0:d=0.06,afade=t=out:st=0.3:d=0.15,adelay=11750|11750[w2];" +
      "[n3]volume=0.22,afade=t=in:st=0:d=0.06,afade=t=out:st=0.3:d=0.15,adelay=18400|18400[w3];" +
      "[n4]volume=0.22,afade=t=in:st=0:d=0.06,afade=t=out:st=0.3:d=0.15,adelay=25050|25050[w4];" +
      "[n5]volume=0.22,afade=t=in:st=0:d=0.06,afade=t=out:st=0.3:d=0.15,adelay=32700|32700[w5];" +
      "[n6]volume=0.22,afade=t=in:st=0:d=0.06,afade=t=out:st=0.3:d=0.15,adelay=38350|38350[w6];" +
      "[w1][w2][w3][w4][w5][w6][pad]amix=inputs=7:normalize=0,loudnorm=I=-18:TP=-1.5:LRA=11[aout];" +
      "[x6]null[vout]"

& ffmpeg -hide_banner -loglevel error -y -i (Join-Path $o "s1.mp4") -i (Join-Path $o "s2.mp4") -i (Join-Path $o "s3.mp4") -i (Join-Path $o "s4.mp4") -i (Join-Path $o "s5.mp4") -i (Join-Path $o "s6.mp4") -i (Join-Path $o "s7.mp4") -f lavfi -t 44 -i "sine=frequency=110:sample_rate=48000" -f lavfi -t 2 -i "anoisesrc=d=0.45:c=pink:r=48000" -filter_complex $fx -map "[vout]" -map "[aout]" -t 43.9 -r 30 -c:v libx264 -crf 18 -preset medium -pix_fmt yuv420p -c:a aac -b:a 160k (Join-Path $p "PipSentry-promo-1080p.mp4")
if ($LASTEXITCODE -ne 0) { throw "concat failed" }
"final ok"
ffprobe -v error -show_entries format=duration,size -of default=nw=1 (Join-Path $p "PipSentry-promo-1080p.mp4")
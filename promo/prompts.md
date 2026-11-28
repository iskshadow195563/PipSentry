# PipSentry 宣傳片 · AI 提示詞包

> 給影片生成模型（Sora / Veo / 可靈 / 即夢 / Runway / 海螺）、圖像模型（MJ / Seedream）、
> 音樂（Suno / Udio）、剪輯 Agent（HyperFrames / Remotion）用。

## 先講最重要的一件事

影片模型**畫不準中文 UI 文字**，硬要它寫「开始监控」只會得到亂碼。
正確分工：

1. **生成模型只做「無字畫面」**：光影、運鏡、粒子、材質、抽象 UI 動態。
2. **真實介面用錄屏**：`PipSentry.exe --shot` / `--grab`，或 OBS 錄實際操作。
3. **字卡、標題、數字用後期加**（剪映／AE／HyperFrames／Remotion），字體微軟雅黑 Bold。

以下每個鏡頭都給「無字版」提示詞，另外標明字卡內容。

## 全局風格種子（貼在每個提示詞前面）

**中文**
```
暗色科技產品短片風格：近黑背景（#050507），畫面下緣有極淡的紫色地平線輝光（#7C3AED），
空氣中有少量細微塵埃粒子；主體是青藍（#22D3EE）到洋紅（#E879F9）的漸層光；
質感偏玻璃與金屬，乾淨、克制、不要霓虹橫線背景；
鏡頭運動緩慢穩定，帶一點手持呼吸感；畫面內不出現任何文字、字母、數字、Logo、浮水印。
16:9，電影感，高對比，暗部保留細節。
```

**English**
```
Dark tech product film look: near-black stage (#050507), a very faint purple horizon glow at the bottom edge (#7C3AED), sparse dust particles in the air, cyan-to-magenta (#22D3EE → #E879F9) gradient accents, glass and brushed-metal surfaces, clean and restrained, no neon horizontal lines, slow stabilized camera with a subtle handheld breath. No text, no letters, no numbers, no logos, no watermarks. 16:9, cinematic, high contrast, detail preserved in shadows.
```

**負面提示（通用）**
```
text, letters, numbers, watermark, logo, UI text, subtitles, distorted hands, extra fingers, low resolution, jpeg artifacts, oversaturated, lens flare spam, neon grid background, purple-blue gradient wallpaper, cartoon, anime
```

## 八個鏡頭

### S1 開場鉤子（0–6s）

**中文**
```
微距鏡頭：一台顯示器的邊角在黑暗中被一束冷光照亮，螢幕裡是一個模糊的遊戲畫面在緩慢流動；
鏡頭從側面極慢推近，焦点落在螢幕邊緣的玻璃反光；
畫面下緣浮起極淡的紫色光暈，空氣中有細塵飄動；
整體像「深夜掛機」的孤獨感。無文字。
```

**English**
```
Macro shot: the corner of a monitor glowing in a dark room, a blurred game scene moving slowly inside the screen; the camera pushes in extremely slowly from the side, focus on the glass reflection at the bezel; a faint purple glow rises from the bottom of the frame, dust drifting in the air; a lonely late-night vibe. No text.
```

字卡（後期）：「挂机的时候，你不用一直切回去」→ 硬切三個詞「切窗口」「被打断」「看不到进度」

### S2 一鍵釘住（6–12s）

**中文**
```
黑色空間中，一塊懸浮的半透明玻璃面板從畫面下方緩緩升起，面板裡是流動的深色介面光影；
升到畫面中央時輕輕一定，邊緣亮起一圈青色輪廓光；
背景其他模糊的視窗向後退去、變暗；鏡頭緩慢環繞 15 度。無文字。
```

**English**
```
A floating translucent glass panel rises slowly from the bottom of a black stage; inside it, dark interface light flows like liquid; it settles near the center and a thin cyan rim light ignites around its edge; blurred background windows recede and dim; the camera orbits 15 degrees slowly. No text.
```

字卡：「选一个窗口 → 框一块画面 → 点一下开始监控」；後期加游標與點擊漣漪（可用真實錄屏取代）

### S3 框選區域（12–18s）

**中文**
```
俯拍一塊正在運行的遊戲畫面，畫面其餘部分逐漸變暗；
一個青色發光的矩形邊框在畫面中自己畫出來（先畫上邊、再畫右邊、下邊、左邊），
框內保持清晰、框外降飽和並變暗；邊框有輕微呼吸光暈；鏡頭極慢推近框內。無文字。
```

**English**
```
Top-down view of a running game frame; the rest of the frame dims and desaturates while a cyan glowing rectangle draws itself (top edge, then right, bottom, left); inside the box stays sharp and colorful, outside goes dark and muted; the border has a soft breathing glow; the camera slowly pushes into the box. No text.
```

字卡：「框一块，只看这一块」

### S4 拖曳縮放（18–21.5s）

**中文**
```
同一塊懸浮小窗在黑色空間中，用彈簧曲線放大到 1.16 倍、再縮回 0.9 倍、最後回到原尺寸；
每次縮放邊緣有輕微的速度拖影，角上有四個小光點提示可拖曳；背景粒子被推開。無文字。
```

**English**
```
The same floating mini window in a black void scales up to 1.16x with a spring ease, then down to 0.9x, then back; each scale has a slight motion smear at the edges; four small glowing handles appear at the corners; background particles get pushed outward. No text.
```

字卡：「拖一下，就能改大小」

### S5 GPU 縮放（21.5–28s）

**中文**
```
抽象資料流：一張巨大的 4K 畫面粒子雲在左側，粒子像被吸入一樣穿過一個發光的晶片透鏡，
在右側凝聚成一塊很小的清晰畫面；粒子流由青轉洋紅，速度先快後慢；
鏡頭跟隨粒子流橫向移動。無文字。
```

**English**
```
Abstract data flow: a huge cloud of 4K-frame particles on the left gets sucked through a glowing silicon lens and condenses into a small, sharp frame on the right; the particle stream shifts from cyan to magenta and decelerates; the camera tracks sideways with the flow. No text.
```

字卡：「GPU 上缩放，只把要看的那块搬回来」＋ 藥丸標籤（任意窗口／局部框选／4K 60FPS／全局热键／版本选择）

### S6 數字證明（28–35.5s）

**中文**
```
純黑背景，五條極細的青色光條從左側延伸，其中一條很長很亮、其餘很短很暗；
長光條在 1.5 秒內迅速收縮到只剩一小段，粒子從收縮處散開；鏡頭固定。無文字、無數字。
```

**English**
```
Pure black stage, five thin cyan light bars extend from the left; one is long and bright, the others short and dim; the long bar contracts quickly to a stub over 1.5 seconds, particles bursting from the collapse point; static camera. No text, no numbers.
```

字卡（後期）：「1,637 MB/s → 35 MB/s」「8.9% 单核」「0.47% GPU」「26 fps」

### S7 版本選擇（35.5–41s）

**中文**
```
黑色空間中，一疊半透明的玻璃卡片從右側滑入並停住，卡片邊緣有細青色描邊；
最上面一張微微彈起、其他卡片在後面錯位堆疊；鏡頭緩慢橫移。卡片內不出現任何文字。
```

**English**
```
A stack of translucent glass cards slides in from the right and settles in a black void; each card has a thin cyan edge; the top card lifts slightly with a spring, the others offset behind it; slow lateral camera move. No text inside the cards.
```

字卡：「更新，由你决定」「启动只提示，不自动更新」

### S8 結尾（41–46.3s）

**中文**
```
黑色空間緩慢推近，一個青色到洋紅的漸層圓角方塊從霧中浮現、輕微旋轉後定住；
底部紫色輝光輕微呼吸，粒子緩慢上升；鏡頭最後 1 秒完全靜止。方塊內不出現任何圖案或文字。
```

**English**
```
Slow push into a black void; a cyan-to-magenta rounded square emerges from the haze, rotates slightly and locks; the purple bottom glow breathes gently, particles drift upward; the camera holds still for the last second. No icon, no text inside the square.
```

字卡（後期）：Logo＋PipSentry＋「把画面钉在最上层」＋「免费・开源・单文件免安装」＋GitHub

## 圖像模型（封面／關鍵幀）

**Midjourney / Seedream / 即夢**
```
dark cinematic product key visual, near-black background, faint purple horizon glow at the bottom, a floating translucent glass UI panel with cyan rim light, cyan-to-magenta gradient accent, volumetric dust, ultra clean, high contrast, 16:10, no text --ar 16:10 --style raw
```

封面文字後期加：主標「把游戏画面 / 钉在最上层」，副標「挂机不用一直切回去・免费开源」。

## 配樂（Suno / Udio）

```
[Instrumental] minimal dark tech-house, 100 BPM, A minor, soft analog pad chords Am–F–C–G, plucked arpeggio, deep sub bass pulses, four-on-the-floor kick, soft clap on 2 and 4, filtered hats, one sparse lead melody in the second half, no vocals, no harsh highs, gentle build from intro to full groove and back down, cinematic, clean mix
```

匯出後在剪輯裡壓到 **−15 LUFS**、峰值 **−1.5 dB**，並 lowpass 14 kHz。

## 配音（TTS 或真人）

**聲音設定**：男聲，30 歲上下，語速偏慢，句尾下沉，不要播音腔、不要每句都上揚。

```
挂机的时候，你真的不用一直切回去。
选一个窗口，框一块画面，点一下开始监控，它就被钉在最上层了。
它抓的是那个窗口，不是你整个屏幕。
大小、位置，拖一下就能改。
缩放放在 GPU 上做，只把你要看的那一块搬回来。
4K 全窗口 60fps，流量从每秒 1637 MB 降到 35 MB，单核 8.9%，GPU 0.47%。
更新只提示，不偷偷装；想回旧版，列表里选一个就行。
PipSentry，免费开源，单文件免安装。GitHub 搜 PipSentry。
```

## 給剪輯 Agent（HyperFrames / Remotion / 剪映 AI）

```
把 8 段無字素材按以下時間線組合成 46 秒 16:9 影片：
0–6 鉤子、6–12 產品釘住、12–18 框選、18–21.5 縮放、21.5–28 能力、28–35.5 數字、35.5–41 版本、41–46.3 結尾。
轉場一律用 0.35 秒的白色速度擦除（wipe），不要用淡入淡出。
字卡統一字體 Microsoft YaHei Bold，主標 72–84px、副標 44–54px，白色 #FFFFFF，副標灰 #9AA3AF；
位置：主標垂直居中偏上（y≈42%），副標在主標下方 40px；
每張字卡入場 0.25 秒上浮 12px＋淡入，出場 0.2 秒淡出；
數字卡用等寬字體，進場時做 0.4 秒的數字滾動。
音軌：墊底音樂 −15 LUFS，轉場音 −23 dB，段落撞擊 −9 dB；人聲（若有）壓在 −16 LUFS。
輸出 H.264 1080p30，CRF 18，AAC 192k。
```

## 常見失敗與修法

| 症狀 | 原因 | 修法 |
| --- | --- | --- |
| 生成出亂碼文字 | 模型硬寫字 | 提示詞加 `no text`，字卡改後期 |
| 畫面像 PPT | 只有靜態卡片 | 每個鏡頭都要有「物件運動」：推鏡、縮放、粒子、光暈呼吸 |
| 太亮太花 | 背景用了漸層壁紙 | 改近黑底＋單一紫色地平線光 |
| 轉場很土 | 用淡入淡出 | 改速度擦除或白閃（≤0.35s） |
| 節奏拖 | 每段等長 | 鉤子 6s、演示 3–6s、數字 7s，結尾 5s |
| 音樂平 | 只有 pad | 加 kick／hat／bass 脈衝，做段落動態 |

## B 站交付檢查

- 16:9、1080p、≤ 60 秒（本片 46s 合適）
- 前 3 秒必須出現痛點或結果，不要放 logo 開頭
- 字幕用 CC（SRT），不要燒死（可切換）
- 封面 1146×717，主標 ≤ 12 字
- 標題帶關鍵詞：挂机 / 游戏 / 开源 / Windows

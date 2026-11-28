# 宣傳片素材（B 站）

| 檔案 | 用途 |
| --- | --- |
| `PipSentry-promo-1080p.mp4` | 正片，46.3 秒，1920×1080 / 30fps |
| `assets/cover.png` | B 站封面 1146×717 |
| `script.md` | 分鏡、口播稿、15 秒短版腳本 |
| `upload.md` | 標題、簡介、標籤、分區、置頂評論 |
| `subtitles.srt` | 上傳成 CC 字幕用（沒有燒進畫面） |
| `assets/shell.png`、`assets/monitor.png`、`assets/settings.png` | 由程式本身匯出的真實截圖 |

## 風格來源

依 `github.com/Pluviobyte/video-production-skills` 的 `dark-saas-magic-video` 方法：
黑底舞台＋微弱底部紫光、大字動效、青色→洋紅漸層 CTA、漂浮 UI、速度擦除轉場。
沒有做逐幀複製，屬於風格級創作。

## 素材怎麼來的

截圖不是拼貼，是程式自己的輸出路徑：

```powershell
PipSentry.exe --shot shell.png --lang zh-Hans
PipSentry.exe --shot monitor.png --monitor --lang zh-Hans
PipSentry.exe --render settings.png --window settings
```

字卡、藥丸、數字、結尾卡、封面由 `make-text.ps1` 與 `make-cards.ps1` 以 GDI+ 產生（微軟雅黑）。

## 重建

```powershell
powershell -File promo/make-text.ps1
powershell -File promo/make-cards.ps1
powershell -File promo/scene1.ps1   # 依序 2..7
powershell -File promo/build-final.ps1
```

需要 ffmpeg（winget install Gyan.FFmpeg）。PowerShell 5.1 讀 UTF-8 需要 BOM，腳本已帶。

## QC（對照 dark-saas-magic-video/qc-checklist）

| 檢查 | 結果 |
| --- | --- |
| 黑底空間、星點、底部紫光 | ✅ 紫光壓在畫面下緣 |
| 沒有持續的霓虹橫線背景 | ✅ |
| 開頭與結尾都有大字動效 | ✅ 打字開場、結尾主張 |
| 青→洋紅漸層 CTA 且觸發變化 | ✅ 第 2 段的「开始监控」 |
| 主體夠大、可讀 | ✅ 截圖 1080–1240px 寬 |
| 轉場用速度／擦除 | ✅ xfade wipeleft 0.35s |
| 沒有無意義的全屏色閃 | ✅ |
| contact sheet 看得出推進 | ✅ 痛點→操作→能力→數字→更新→CTA |
| 音訊 | 自製 100 BPM 曲（pad／bass／kick／clap／hat／arp／lead）＋低頻撞擊與柔化 whoosh，−14.8 LUFS |
| 字幕 | 未燒進畫面，另附 SRT 供 B 站 CC |

## 已知限制

- 沒有真人旁白；口播稿在 `script.md`，想配音可以直接照唸。
- 監控畫面用的是遊戲啟動器視窗（實機抓取），不是遊戲內畫面；要換成真實遊玩畫面，用 `--grab` 抽一張替換 `assets/monitor.png` 後重跑 `scene3.ps1`。
- 音樂是 numpy 合成的，情緒偏冷；`assets/music.py` 可改 BPM／和弦／段落，或直接換成 B 站曲庫。
- 動態是「用截圖做運動」（推鏡、縮放、游標、漣漪、選取框），不是實機錄影；要實錄可照 `script.md` 的鏡頭表用 OBS 補拍。
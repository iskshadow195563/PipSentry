# PipSentry

Windows 上的視窗監控小工具。

遊戲掛機的時候，把視窗（或整個螢幕）的一小塊畫面釘在最上層，不用切回去也知道進度。
主要是為了我自己多螢幕、多視窗的工作方式寫的。

![深色](docs/screenshot-dark-en.png)

## 功能

- 用 Windows Graphics Capture 抓畫面，被其他視窗蓋住也抓得到
- 可以只框畫面的一小塊，之後換解析度會自己換算
- 監控窗無邊框、置頂，可調透明度，可以讓滑鼠穿過去
- 深色 / 淺色主題，繁體中文 / 简体中文 / English
- 免安裝單檔 exe，或 MSI 安裝

## 系統需求

Windows 10 1903 以上或 Windows 11，x64。不用另外裝 .NET。

## 怎麼用

1. 開 `PipSentry.exe`
2. 左邊清單選要看的視窗，右邊會即時預覽
3. 只要看局部就切到「自訂區域」，按「框選區域」，拖一個範圍後按 Enter
4. 按「開始監控」，主視窗會收起來，剩懸浮窗

滑鼠移到懸浮窗上會出現工具列。右鍵有貼齊角落、大小、穿透、截圖等選項。

## 快捷鍵

| 按鍵 | 作用 |
| --- | --- |
| `Ctrl+Alt+M` | 顯示 / 隱藏監控窗 |
| `Ctrl+Alt+P` | 暫停 / 繼續 |
| `Ctrl+Alt+S` | 存一張截圖（存到「圖片\PipSentry」） |
| `Ctrl+Alt+R` | 回設定畫面 |
| `Ctrl+Alt+T` | 切換點擊穿透 |
| `F5` | 重新整理視窗清單（在設定畫面） |

設定存在 `%LOCALAPPDATA%\PipSentry\settings.json`，關掉再開會還原。

## 限制

- 獨佔全螢幕抓不到，遊戲請用無邊框視窗或視窗化
- 最小化的視窗抓不到（Windows 不會合成它）
- 有些程式是自己畫在子視窗上，實測像 MuMu 這類模擬器會是黑畫面
- 抓需要管理員權限的視窗時，PipSentry 也要用同樣權限開

## 背景不要停

掛機最麻煩的是遊戲切到背景就被降頻或停止更新。程式裡有幾個開關：

- **保持來源持續更新**：只要監控工作階段還在，DWM 就會一直合成來源視窗
- **防止系統休眠**：用 `SetThreadExecutionState` 讓系統和螢幕不睡
- **停用視窗遊戲最佳化**：對目標 exe 寫 `HKCU\Software\Microsoft\DirectX\UserGpuPreferences`
  的 `SwapEffectUpgradeEnable=0`，改完要重開遊戲
- **強制重繪目標**：每秒送一次 `RedrawWindow`，對老式 GDI 視窗有用，DirectX 遊戲影響不大

## 從原始碼建置

需要 .NET 8 SDK，打包 MSI 還需要 WiX 5。

```powershell
./build.ps1
```

或分開做：

```powershell
dotnet publish src/PipSentry -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -o dist/PipSentry-portable

dotnet tool install --tool-path .tools/wix wix --version "5.*"
cd packaging
../.tools/wix/wix.exe build -arch x64 -ext WixToolset.UI.wixext/5.0.2 PipSentry.wxs `
  -o ../dist/PipSentry-1.1.1-x64.msi
```

輸出在 `dist/`，執行檔和 MSI 沒有進版控，放在 Releases。

## 截圖怎麼來的

```powershell
PipSentry.exe --render out.png --lang zh-Hant --theme Dark
```

離屏渲染整個介面，不會抓到任何真實視窗內容，所以可以放心放進 repo。

## 授權

MIT，見 `LICENSE`。

功能上有參考 OnTopReplica（區域框選、貼齊、尺寸預設）、WindowTop（右鍵選單、深淺色）、
OBS（WGC 的用法）的做法，但沒有用它們的程式碼。那三個專案的授權分別是 MS-RL、專有、GPL-2.0，
直接抄會影響這裡的授權。

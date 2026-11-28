# 程式碼簽章

## 為什麼會跳「不明的發行者」

Windows 判斷發行者只看一件事：檔案有沒有**由信任的憑證頒發機構（CA）簽發的 Authenticode 簽章**。沒有簽章，UAC 與 SmartScreen 就只能顯示「不明的發行者」。

專案名稱、Company、Manufacturer 這些欄位不會改變那個提示，它們只影響檔案內容與安裝清單的顯示。

## 簽章後會變什麼

| 位置 | 未簽章 | 已簽章（憑證受信任） |
| --- | --- | --- |
| UAC 安裝提示 | 不明的發行者 | 憑證上的名稱（例如 PipSentry Studio） |
| 檔案內容 → 數位簽章 | 沒有 | 有效，含時間戳 |
| SmartScreen | 一定會擋 | 新憑證仍可能提示，累積下載量後才會消失 |

注意：**簽章不會讓 SmartScreen 立刻放行**。OV 憑證需要累積信譽；EV 憑證與 Azure Trusted Signing 較快，但仍取決於服務。

## 取得憑證的選項

| 選項 | 費用 | 驗證方式 | 適合 |
| --- | --- | --- | --- |
| **Azure Trusted Signing** | 約 US$9.99/月 | Azure 訂閱 + 身分驗證（個人可） | 獨立開發者，CI 直接簽 |
| OV 程式碼簽章憑證 | 約 US$150–400/年 | 公司或個人身分文件 | 一般商業發佈 |
| EV 程式碼簽章憑證 | 約 US$300–600/年 | 較嚴格，通常附硬體 Token／HSM | 需要最快建立信任 |
| SignPath Foundation | 免費（開源審核） | 專案需為開源且審核通過 | 開源專案 |
| 自簽憑證 | 免費 | 無 | 只在自己的機器或內部網路 |

## 這個 repo 怎麼簽

### 本機自簽（只對這台機器有效）

```powershell
./tools/new-signing-cert.ps1
./tools/trust-signing-cert.ps1 -CerPath "$env:LOCALAPPDATA\PipSentry\signing\PipSentry.cer"
./build.ps1 -Sign -AllowUntrustedSign
```

其他機器要顯示同樣的發行者，必須先匯入那張 .cer 並信任它；否則仍顯示不明的發行者。所以自簽只適合自己或內部使用。

### 使用 CA 憑證

把憑證安裝到 Cert:\CurrentUser\My（或提供 PFX），然後：

```powershell
./build.ps1 -Sign -SignThumbprint <憑證指紋>
./build.ps1 -Sign -SignPfx C:\keys\pip.pfx -SignPfxPassword <密碼>
```

build.ps1 會先簽 exe，再打包 MSI（所以 MSI 內含已簽章的 exe），最後簽 MSI。

### CI 自動簽

在 GitHub repo 設定兩個 secret：

- SIGNING_PFX_BASE64：PFX 內容的 base64
- SIGNING_PFX_PASSWORD：PFX 密碼

workflow 的 Sign artifacts 步驟會在 secret 存在時簽章，不存在就跳過。簽章後才上傳 artifact。

## 驗證

```powershell
Get-AuthenticodeSignature dist/PipSentry-portable/PipSentry.exe | Format-List Status, SignerCertificate
Get-AuthenticodeSignature dist/PipSentry-*-x64.msi | Format-List Status, SignerCertificate
```

Status 必須是 Valid。若是 UnknownError，代表憑證鏈不受信任（自簽或漏裝中繼憑證）。

## 常見問題

- 時間戳很重要：憑證過期後，有時間戳的簽章仍然有效；sign.ps1 預設用 http://timestamp.digicert.com。
- 換憑證要重新簽所有發佈檔，包括舊版 release 的資產。
- MSI 內的 exe 也要簽，本專案的建置順序已經處理。
- 不要在 repo 放 PFX 或密碼，只放 base64 到 GitHub secret。

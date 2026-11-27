<#
    PipSentry build script
    Produces:
      dist/PipSentry-portable/PipSentry.exe   (standalone, no install)
      dist/PipSentry-1.0.0-x64.msi            (WiX installer)
#>
param(
    [string]$Configuration = "Release",
    [string]$Dotnet = "",
    [string]$Wix = "",
    [switch]$SkipMsi
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

if (-not $Dotnet) {
    $Dotnet = Join-Path $root "sdk/dotnet.exe"
    if (-not (Test-Path $Dotnet)) { $Dotnet = "dotnet" }
}

Write-Host "==> Stopping running instances" -ForegroundColor Cyan
Get-Process PipSentry -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

Write-Host "==> Publishing portable executable" -ForegroundColor Cyan
& $Dotnet publish "src/PipSentry/PipSentry.csproj" -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -o "dist/PipSentry-portable" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

if ($SkipMsi) { Write-Host "Skipped MSI." -ForegroundColor Yellow; return }

if (-not $Wix) {
    $Wix = Join-Path $root ".tools/wix/wix.exe"
    if (-not (Test-Path $Wix)) {
        Write-Host "==> Installing WiX toolset" -ForegroundColor Cyan
        & $Dotnet tool install --tool-path (Join-Path $root ".tools/wix") wix --version "5.*"
        $Wix = Join-Path $root ".tools/wix/wix.exe"
    }
}

Write-Host "==> Building MSI" -ForegroundColor Cyan
Push-Location (Join-Path $root "packaging")
try {
    & $Wix extension add "WixToolset.UI.wixext/5.0.2" | Out-Host
    & $Wix build -arch x64 -ext "WixToolset.UI.wixext/5.0.2" PipSentry.wxs `
        -o (Join-Path $root "dist/PipSentry-1.1.0-x64.msi")
    if ($LASTEXITCODE -ne 0) { throw "wix build failed" }
}
finally {
    Pop-Location
}

Write-Host "Done:" -ForegroundColor Green
Get-ChildItem (Join-Path $root "dist") -Recurse -File | Select-Object FullName, @{n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize

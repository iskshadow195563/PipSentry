<#
    PipSentry build script
    Produces:
      dist/PipSentry-portable/PipSentry.exe   (standalone, no install)
      dist/PipSentry-<version>-x64.msi        (WiX installer)

    Signing (optional):
      ./build.ps1 -Sign                  uses CN=PipSentry Studio from the user store
      ./build.ps1 -Sign -SignThumbprint <thumb>
      ./build.ps1 -Sign -SignPfx <pfx> -SignPfxPassword <password>
      Add -AllowUntrustedSign for a self-signed certificate.
#>
param(
    [string]$Configuration = "Release",
    [string]$Dotnet = "",
    [string]$Wix = "",
    [switch]$SkipMsi,
    [switch]$Sign,
    [string]$SignThumbprint = $env:PIPSENTRY_SIGN_THUMBPRINT,
    [string]$SignPfx = $env:PIPSENTRY_SIGN_PFX,
    [string]$SignPfxPassword = $env:PIPSENTRY_SIGN_PFX_PASSWORD,
    [switch]$AllowUntrustedSign
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$version = (Select-String -Path "src/PipSentry/PipSentry.csproj" -Pattern "<Version>([^<]+)</Version>").Matches[0].Groups[1].Value
$exePath = Join-Path $root "dist/PipSentry-portable/PipSentry.exe"
$msiPath = Join-Path $root "dist/PipSentry-$version-x64.msi"

if (-not $Dotnet) {
    $Dotnet = Join-Path $root "sdk/dotnet.exe"
    if (-not (Test-Path $Dotnet)) { $Dotnet = "dotnet" }
}

function Invoke-Sign([string[]]$files) {
    if (-not $Sign) { return }
    $parameters = @{ Path = $files }
    if ($AllowUntrustedSign) { $parameters.AllowUntrusted = $true }
    if ($SignThumbprint) { $parameters.Thumbprint = $SignThumbprint }
    if ($SignPfx) {
        $parameters.PfxPath = $SignPfx
        $parameters.PfxPassword = $SignPfxPassword
    }
    & (Join-Path $root "sign.ps1") @parameters
    if ($LASTEXITCODE -ne 0) { throw "signing failed" }
}

Write-Host "==> Stopping running instances" -ForegroundColor Cyan
Get-Process PipSentry -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

Write-Host "==> Publishing portable executable" -ForegroundColor Cyan
& $Dotnet publish "src/PipSentry/PipSentry.csproj" -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "dist/PipSentry-portable" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Sign the exe before packaging so the MSI carries a signed payload.
Invoke-Sign @($exePath)

if ($SkipMsi) { Write-Host "Skipped MSI." -ForegroundColor Yellow; return }

if (-not $Wix) {
    $Wix = Join-Path $root ".tools/wix/wix.exe"
    if (-not (Test-Path $Wix)) {
        Write-Host "==> Installing WiX toolset" -ForegroundColor Cyan
        & $Dotnet tool install --tool-path (Join-Path $root ".tools/wix") wix --version "5.0.2"
        if ($LASTEXITCODE -ne 0) { throw "wix install failed" }
        $Wix = Join-Path $root ".tools/wix/wix.exe"
    }
}

Write-Host "==> Building MSI" -ForegroundColor Cyan
Push-Location (Join-Path $root "packaging")
try {
    & $Wix extension add -g "WixToolset.UI.wixext/5.0.2" | Out-Host
    & $Wix build -arch x64 -ext "WixToolset.UI.wixext/5.0.2" PipSentry.wxs -o $msiPath
    if ($LASTEXITCODE -ne 0) { throw "wix build failed" }
}
finally {
    Pop-Location
}

Invoke-Sign @($msiPath)

Write-Host "Done:" -ForegroundColor Green
Get-ChildItem (Join-Path $root "dist") -Recurse -File | Select-Object FullName, @{n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
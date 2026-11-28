<#
.SYNOPSIS
    Authenticode-signs PipSentry binaries and verifies the result.

.DESCRIPTION
    Uses a certificate from the current user store (by thumbprint or subject)
    or a PFX file. Signs with SHA-256 and an RFC 3161 timestamp, then verifies.

.EXAMPLE
    ./sign.ps1 -Thumbprint 4270938584CAD3059EC26EE6EA5DB281B0DB9F13
#>
[CmdletBinding()]
param(
    [string[]]$Path,
    [string]$Thumbprint,
    [string]$Subject = "CN=PipSentry Studio",
    [string]$PfxPath,
    [string]$PfxPassword,
    [string]$TimestampServer = "http://timestamp.digicert.com",
    [switch]$AllowUntrusted
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $Path -or $Path.Count -eq 0) {
    $msi = Get-ChildItem (Join-Path $root "dist") -Filter "PipSentry-*-x64.msi" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
    $Path = @((Join-Path $root "dist/PipSentry-portable/PipSentry.exe"), $msi) | Where-Object { $_ }
}

$certificate = $null
if ($PfxPath) {
    if (-not (Test-Path $PfxPath)) { throw "PFX not found: $PfxPath" }
    $secure = if ($PfxPassword) { ConvertTo-SecureString $PfxPassword -AsPlainText -Force } else { New-Object System.Security.SecureString }
    $certificate = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation Cert:\CurrentUser\My -Password $secure
}
elseif ($Thumbprint) {
    $certificate = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1
}
else {
    $certificate = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $Subject -and $_.NotAfter -gt (Get-Date) } |
        Sort-Object NotAfter -Descending | Select-Object -First 1
}

if (-not $certificate) {
    throw "No code-signing certificate found for subject " + $Subject + ". Run tools/new-signing-cert.ps1 for a local one, or install a CA-issued certificate."
}

Write-Host ("Signing with " + $certificate.Subject + " (" + $certificate.Thumbprint + ")") -ForegroundColor Cyan

$failed = $false
foreach ($file in $Path) {
    if (-not (Test-Path $file)) { Write-Warning ("missing: " + $file); $failed = $true; continue }
    $signature = Set-AuthenticodeSignature -FilePath $file -Certificate $certificate -HashAlgorithm SHA256 -TimestampServer $TimestampServer
    $status = $signature.Status
    $ok = ($status -eq "Valid") -or ($AllowUntrusted -and $status -eq "UnknownError")
    if ($ok) { Write-Host ("  " + (Split-Path -Leaf $file) + " -> " + $status) -ForegroundColor Green }
    else { Write-Host ("  " + (Split-Path -Leaf $file) + " -> " + $status + " " + $signature.StatusMessage) -ForegroundColor Red }
    if (-not $ok) { $failed = $true }
}

if ($failed) { exit 1 }
foreach ($file in $Path) {
    $hash = (Get-FileHash $file -Algorithm SHA256).Hash
    Write-Host ("  SHA256 " + (Split-Path -Leaf $file) + " " + $hash)
}

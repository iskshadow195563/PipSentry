<#
.SYNOPSIS
    Creates a self-signed code-signing certificate for local builds.

.DESCRIPTION
    Writes a .pfx (private key, password "pip") and a .cer to
    %LOCALAPPDATA%\PipSentry\signing.
    A self-signed certificate only removes the "unknown publisher" prompt on
    machines that trust it. Public releases need a CA-issued certificate.
#>
[CmdletBinding()]
param(
    [string]$Subject = "CN=PipSentry Studio",
    [int]$Years = 3
)

$ErrorActionPreference = "Stop"
$dir = Join-Path $env:LOCALAPPDATA "PipSentry/signing"
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$existing = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Subject } | Select-Object -First 1
if ($existing) {
    Write-Host "Reusing existing certificate" -ForegroundColor Yellow
    $certificate = $existing
}
else {
    $certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject $Subject -FriendlyName "PipSentry code signing" -CertStoreLocation Cert:\CurrentUser\My -KeyUsage DigitalSignature -KeyExportPolicy Exportable -NotAfter (Get-Date).AddYears($Years)
}

$password = ConvertTo-SecureString -String "pip" -Force -AsPlainText
$pfx = Join-Path $dir "PipSentry.pfx"
$cer = Join-Path $dir "PipSentry.cer"
Export-PfxCertificate -Cert $certificate -FilePath $pfx -Password $password | Out-Null
Export-Certificate -Cert $certificate -FilePath $cer -Type CERT | Out-Null

Write-Host ("thumbprint: " + $certificate.Thumbprint)
Write-Host ("pfx:        " + $pfx + "  (password: pip)")
Write-Host ("cer:        " + $cer)
Write-Host ""
Write-Host "To trust it on this machine (Windows shows a confirmation prompt):" -ForegroundColor Cyan
Write-Host ("  ./tools/trust-signing-cert.ps1 -CerPath " + $cer)

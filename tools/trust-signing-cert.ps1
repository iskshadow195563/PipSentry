<#
.SYNOPSIS
    Trusts a code-signing certificate for the current user.

.DESCRIPTION
    Imports the .cer into CurrentUser\Root and CurrentUser\TrustedPublisher so
    signatures made with it validate on this account. Windows shows a
    confirmation prompt; that cannot be scripted away.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CerPath
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $CerPath)) { throw "not found: " + $CerPath }

$certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2((Resolve-Path $CerPath).Path)
Write-Host ("Trusting " + $certificate.Subject + " (" + $certificate.Thumbprint + ")") -ForegroundColor Cyan

foreach ($name in @("Root", "TrustedPublisher")) {
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($name, "CurrentUser")
    $store.Open("ReadWrite")
    try {
        $already = $store.Certificates | Where-Object { $_.Thumbprint -eq $certificate.Thumbprint }
        if ($already) { Write-Host ("  already in " + $name); continue }
        $store.Add($certificate)
        Write-Host ("  added to " + $name) -ForegroundColor Green
    }
    finally {
        $store.Close()
    }
}

Write-Host "Done. Signed builds show the certificate subject as the publisher on this account."

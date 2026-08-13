<#
.SYNOPSIS
    Focused regression check for the local Development target/proxy contract.

    This test does not start or stop applications and never needs a password.
    Authentication and session behavior remain covered by the backend and
    Angular test suites plus the real frontend-origin smoke.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$launcherPath = Join-Path $PSScriptRoot 'Start-MiniErpDevelopment.ps1'
$trackedProxyPath = Join-Path $repositoryRoot 'frontend\proxy.conf.json'
$generatedProxyPath = Join-Path $repositoryRoot '.runtime\proxy.conf.json'

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)]$Actual,
        [Parameter(Mandatory = $true)]$Expected,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if ($Actual -ne $Expected) {
        throw "$Message. Expected '$Expected', received '$Actual'."
    }
}

if (-not (Test-Path -LiteralPath $launcherPath)) {
    throw "Launcher not found: $launcherPath"
}

$trackedProxy = Get-Content -LiteralPath $trackedProxyPath -Raw | ConvertFrom-Json
$trackedTarget = $trackedProxy.PSObject.Properties['/api'].Value.target
Assert-Equal -Actual $trackedTarget -Expected 'http://localhost:5000' -Message 'Tracked proxy default changed unexpectedly'

$listener = New-Object System.Net.Sockets.TcpListener -ArgumentList @([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
try {
    $customPort = $listener.LocalEndpoint.Port
}
finally {
    $listener.Stop()
}

$previousApiUrl = $env:MESP_DEV_API_URL
$previousApiPort = $env:MESP_DEV_API_PORT
try {
    Remove-Item Env:MESP_DEV_API_PORT -ErrorAction SilentlyContinue
    $env:MESP_DEV_API_URL = "http://localhost:$customPort"
    & $PSHOME\powershell.exe -NoProfile -ExecutionPolicy Bypass -File $launcherPath -ValidateOnly
    if ($LASTEXITCODE -ne 0) {
        throw "Launcher validation returned exit code $LASTEXITCODE."
    }

$generatedProxy = Get-Content -LiteralPath $generatedProxyPath -Raw | ConvertFrom-Json
$generatedTarget = $generatedProxy.PSObject.Properties['/api'].Value.target
Assert-Equal -Actual $generatedTarget -Expected "http://localhost:$customPort" -Message 'Generated proxy did not follow MESP_DEV_API_URL'
$proxyBytes = [System.IO.File]::ReadAllBytes($generatedProxyPath)
if ($proxyBytes.Length -ge 3 -and $proxyBytes[0] -eq 0xEF -and $proxyBytes[1] -eq 0xBB -and $proxyBytes[2] -eq 0xBF) {
    throw 'Generated Angular proxy must be UTF-8 without a BOM for the current Angular CLI toolchain.'
}
}
finally {
    if ($null -eq $previousApiUrl) {
        Remove-Item Env:MESP_DEV_API_URL -ErrorAction SilentlyContinue
    }
    else {
        $env:MESP_DEV_API_URL = $previousApiUrl
    }

    if ($null -eq $previousApiPort) {
        Remove-Item Env:MESP_DEV_API_PORT -ErrorAction SilentlyContinue
    }
    else {
        $env:MESP_DEV_API_PORT = $previousApiPort
    }
}

Write-Host 'MiniERP Development runtime configuration tests passed: default tracked target and custom generated proxy target.'

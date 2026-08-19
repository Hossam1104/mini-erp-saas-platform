<#
.SYNOPSIS
    Safe dedicated entry point for the full MiniERP backend test suite.

.DESCRIPTION
    This script runs the complete backend test suite safely by:

    1. Constructing a disposable LocalDB connection in process memory only.
    2. Assigning it exclusively to MESP_SQLSERVER_SAFETY_CONNECTION_STRING.
    3. Leaving MESP_SQLSERVER_CONNECTION_STRING (the persistent runtime variable)
       completely unchanged.
    4. Running the full backend regression including the SQL Server
       safety-harness tests.
    5. Restoring / removing the safety variable in a guaranteed finally block.

    This ensures the destructive SQL safety harness (which creates and drops its
    own MiniErpFoundation_* database) can never operate against the persistent
    MESP development database.

    Environment variable roles:
      MESP_SQLSERVER_CONNECTION_STRING        - persistent Owner Development DB (MESP on server .)
      MESP_SQLSERVER_SAFETY_CONNECTION_STRING - disposable LocalDB MiniErpFoundation_* target

    Neither role authorizes the other. The safety harness rejects any connection
    whose data source is not (localdb)\MSSQLLocalDB or whose database name does
    not match MiniErpFoundation_[A-Za-z0-9_]+.

.PARAMETER Configuration
    .NET build configuration to use for testing. Defaults to Release.

.PARAMETER NoBuild
    Skip restore and build and run tests against existing build output.
    Defaults to $true (no rebuild).

.PARAMETER VerbosityLevel
    MSBuild/dotnet test verbosity level. Defaults to 'minimal'.

.EXAMPLE
    # Typical developer run - tests only, against existing Release build:
    .\scripts\Test-MiniErpBackend.ps1

.EXAMPLE
    # With a fresh Release build:
    .\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false
#>

[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$NoBuild = $true,
    [string]$VerbosityLevel = 'minimal'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localDbServer  = '(localdb)\MSSQLLocalDB'

# -----------------------------------------------------------------------
# 1. Generate a unique disposable database name.
#    Pattern: MiniErpFoundation_<yyyyMMddHHmmss>_<8-char guid token>
#    This matches MiniErpFoundation_[A-Za-z0-9_]+ which is the pattern
#    the safety validator enforces and the cleanup logic matches.
# -----------------------------------------------------------------------
$safeToken    = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$databaseName = "MiniErpFoundation_{0}_{1}" -f (Get-Date -Format 'yyyyMMddHHmmss'), $safeToken

# -----------------------------------------------------------------------
# 2. Build the safety connection string in process memory only.
#    Integrated Security = no password, no credential to protect.
#    TrustServerCertificate avoids TLS negotiation with LocalDB.
# -----------------------------------------------------------------------
$safetyConnectionString = "Server=$localDbServer;Database=$databaseName;Integrated Security=True;TrustServerCertificate=True;"

# -----------------------------------------------------------------------
# 3. Save the existing safety variable value (may be empty/null) so we
#    can restore it exactly in the finally block.
#    The runtime variable is intentionally never read, never changed.
# -----------------------------------------------------------------------
$previousSafetyConnectionString = $env:MESP_SQLSERVER_SAFETY_CONNECTION_STRING
$previousDevAuthBypass          = $env:MESP_DEV_AUTH_BYPASS

Write-Host "Generated disposable safety target: $localDbServer / $databaseName"
Write-Host "MESP_SQLSERVER_CONNECTION_STRING (runtime): unchanged"

try {
    # ------------------------------------------------------------------
    # 4. Assign the disposable connection to the dedicated safety variable.
    #    Neutralize inherited MESP_DEV_AUTH_BYPASS so host tests (which run in
    #    Production/Staging modes) are not invalidated by development bypass settings.
    # ------------------------------------------------------------------
    $env:MESP_SQLSERVER_SAFETY_CONNECTION_STRING = $safetyConnectionString
    Remove-Item Env:MESP_DEV_AUTH_BYPASS -ErrorAction SilentlyContinue

    Push-Location $repositoryRoot
    try {
        if (-not $NoBuild) {
            Write-Host '--- Backend restore ---'
            & dotnet restore .\backend\MiniErp.sln
            if ($LASTEXITCODE -ne 0) { throw 'Backend restore failed.' }

            Write-Host "--- Backend $Configuration build ---"
            & dotnet build .\backend\MiniErp.sln --configuration $Configuration --no-restore
            if ($LASTEXITCODE -ne 0) { throw "Backend $Configuration build failed." }
        }

        Write-Host "--- Full backend test suite (includes SQL Server LocalDB safety harness) ---"
        & dotnet test .\backend\MiniErp.sln `
            --configuration $Configuration `
            --no-restore `
            --no-build `
            --verbosity $VerbosityLevel
        if ($LASTEXITCODE -ne 0) { throw 'Full backend test suite failed.' }

        Write-Host "Backend suite passed against disposable database $databaseName."
    }
    finally {
        Pop-Location -ErrorAction SilentlyContinue
    }
}
finally {
    # ------------------------------------------------------------------
    # 5. Restore / remove the safety variable. The runtime variable is
    #    never modified so no runtime restoration is needed.
    # ------------------------------------------------------------------
    if ($null -eq $previousSafetyConnectionString) {
        Remove-Item Env:MESP_SQLSERVER_SAFETY_CONNECTION_STRING -ErrorAction SilentlyContinue
    }
    else {
        $env:MESP_SQLSERVER_SAFETY_CONNECTION_STRING = $previousSafetyConnectionString
    }

    if ($null -eq $previousDevAuthBypass) {
        Remove-Item Env:MESP_DEV_AUTH_BYPASS -ErrorAction SilentlyContinue
    }
    else {
        $env:MESP_DEV_AUTH_BYPASS = $previousDevAuthBypass
    }

    # Remind the operator that the persistent runtime database was not touched.
    Write-Host "MESP_SQLSERVER_CONNECTION_STRING (runtime): unchanged. MESP data is intact."
}

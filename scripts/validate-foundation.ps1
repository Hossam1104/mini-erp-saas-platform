<#
.SYNOPSIS
    The single canonical command for the complete Foundation validation:
    backend restore/build/regression (including the SQL Server LocalDB
    probes and the safety-catalogue structural validator), disposable
    database cleanup and proof of removal, Angular unit tests, Angular
    production build, Playwright Foundation journeys, a production
    dependency audit and a git diff whitespace check. Any failing step
    makes this command fail closed with a non-zero exit code.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localDbInstanceName = 'MSSQLLocalDB'
$localDbServer = "(localdb)\$localDbInstanceName"

. (Join-Path $PSScriptRoot 'FoundationValidationLock.ps1')

function Resolve-SqlToolExecutable {
    <#
    Dynamic but bounded SQL Server tool discovery: PATH first, then a
    version-agnostic probe of the known SQL Server Tools\Binn and Client
    SDK\ODBC Tools\Binn layouts under Program Files. No specific SQL Server
    release/version is ever hard-coded, and this never performs a full
    recursive scan of the (potentially huge) database-engine tree -- only the
    single fixed "<version>\Tools\Binn\<exe>" and
    "Client SDK\ODBC\<version>\Tools\Binn\<exe>" shapes are probed, one path
    segment of wildcard expansion at a time.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$ExecutableName
    )

    $onPath = Get-Command -Name $ExecutableName -ErrorAction SilentlyContinue
    if ($null -ne $onPath) {
        return $onPath.Source
    }

    $searchRoots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) |
        Where-Object { $_ -and (Test-Path -LiteralPath $_) } |
        Select-Object -Unique

    $knownLayoutGlobs = @(
        'Microsoft SQL Server\*\Tools\Binn',
        'Microsoft SQL Server\Client SDK\ODBC\*\Tools\Binn'
    )

    $matches = foreach ($root in $searchRoots) {
        foreach ($layoutGlob in $knownLayoutGlobs) {
            $globPath = Join-Path (Join-Path $root $layoutGlob) $ExecutableName
            Get-Item -Path $globPath -ErrorAction SilentlyContinue
        }
    }

    # Highest version directory sorts last lexically for the supported
    # three-digit SQL Server tools version segments (e.g. 150 < 160 < 170).
    $best = $matches | Sort-Object -Property FullName | Select-Object -Last 1
    if ($null -ne $best) {
        return $best.FullName
    }

    return $null
}

function Resolve-SqlLocalDbExecutable {
    $path = Resolve-SqlToolExecutable -ExecutableName 'SqlLocalDB.exe'
    if ($null -eq $path) {
        throw 'SQL Server LocalDB (SqlLocalDB.exe) was not found on PATH or under any discovered SQL Server Tools installation under Program Files. Install SQL Server Express LocalDB, or add SqlLocalDB.exe to PATH, then retry. SQL validation is never silently skipped.'
    }
    return $path
}

function Resolve-SqlCmdExecutable {
    $path = Resolve-SqlToolExecutable -ExecutableName 'sqlcmd.exe'
    if ($null -eq $path) {
        throw 'sqlcmd.exe was not found on PATH or under any discovered SQL Server Tools installation under Program Files. Install the SQL Server command-line tools, or add sqlcmd.exe to PATH, then retry.'
    }
    return $path
}

function Get-OrphanFoundationDatabaseNames {
    param([Parameter(Mandatory = $true)][string]$SqlCmdPath)

    $query = "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE name LIKE N'MiniErpFoundation[_]%';"
    $output = & $SqlCmdPath -S $localDbServer -E -h -1 -W -Q $query
    if ($LASTEXITCODE -ne 0) {
        throw 'Querying sys.databases for disposable Foundation databases failed.'
    }

    return @($output | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^MiniErpFoundation_[A-Za-z0-9_]+$' })
}

function Remove-FoundationDatabase {
    <#
    Drops only a database whose name matches the disposable
    MiniErpFoundation_* convention. Never touches any other database.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$SqlCmdPath,
        [Parameter(Mandatory = $true)][string]$DatabaseName
    )

    if ($DatabaseName -notmatch '^MiniErpFoundation_[A-Za-z0-9_]+$') {
        throw "Refusing to drop '$DatabaseName': it does not match the disposable Foundation database naming convention and cleanup must never touch a database outside that convention."
    }

    $dropQuery = "IF DB_ID(N'$DatabaseName') IS NOT NULL BEGIN ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DatabaseName]; END;"
    & $SqlCmdPath -S $localDbServer -E -Q $dropQuery | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Dropping disposable database '$DatabaseName' failed."
    }
}

function Assert-NoOrphanFoundationDatabasesRemain {
    param([Parameter(Mandatory = $true)][string]$SqlCmdPath)

    $remaining = Get-OrphanFoundationDatabaseNames -SqlCmdPath $SqlCmdPath
    if ($remaining.Count -gt 0) {
        throw "$($remaining.Count) disposable Foundation database(s) remained on $localDbServer after cleanup: $($remaining -join ', ')"
    }
    Write-Host "Verified: 0 MiniErpFoundation_* databases remain on $localDbServer."
}

$sqlLocalDb = Resolve-SqlLocalDbExecutable
$sqlCmd = Resolve-SqlCmdExecutable
Write-Host "Using SqlLocalDB.exe: $sqlLocalDb"
Write-Host "Using sqlcmd.exe: $sqlCmd"

& $sqlLocalDb info $localDbInstanceName | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "The $localDbInstanceName instance is not available."
}

& $sqlLocalDb start $localDbInstanceName | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "The $localDbInstanceName instance could not be started."
}

# See FoundationValidationLock.ps1: LocalDB is scoped by Windows user, not by
# logon session, so the lock must coordinate across sessions for the same
# user rather than merely within one session. It is a Global-namespace named
# mutex whose name is suffixed with the current user's SID and whose ACL
# grants access only to that SID -- it does not serialize unrelated Windows
# users, and no other Windows user can open or signal it (MESP-94 F1).
$validationMutexName = Get-FoundationValidationLockName
$validationMutex = New-FoundationValidationLock
$ownsValidationLock = $false
$recoveredFromAbandonedLock = $false

try {
    # The prior validation run for this Windows user may have terminated
    # (crash, forced kill, forced session end) while still holding the lock.
    # Wait-FoundationValidationLock recovers ownership in that case rather
    # than treating it as an ordinary competing active run (MESP-94 F2).
    $lockResult = Wait-FoundationValidationLock -Mutex $validationMutex -Timeout ([TimeSpan]::FromMinutes(10))
    $ownsValidationLock = $lockResult.Acquired
    $recoveredFromAbandonedLock = $lockResult.RecoveredFromAbandonedLock
    if ($recoveredFromAbandonedLock) {
        Write-Warning "Recovered from an abandoned Foundation validation lock ('$validationMutexName'): the prior owner for this Windows user terminated without releasing it. Continuing into stale-database cleanup to recover safely."
    }

    if (-not $ownsValidationLock) {
        throw "Another canonical Foundation validation run already owns the '$validationMutexName' lock for this Windows user. Wait for it to finish, or investigate a stuck run, then retry. Startup cleanup is never run without holding this lock."
    }

    if ($recoveredFromAbandonedLock) {
        Write-Host '--- Recovered from an abandoned prior validation lock for this Windows user; proceeding to stale-database cleanup ---'
    }

    Write-Host '--- Pre-run cleanup of stale disposable Foundation databases from a prior run ---'
    foreach ($stale in (Get-OrphanFoundationDatabaseNames -SqlCmdPath $sqlCmd)) {
        Write-Host "Removing stale disposable database: $stale"
        Remove-FoundationDatabase -SqlCmdPath $sqlCmd -DatabaseName $stale
    }

    $databaseName = "MiniErpFoundation_{0}_{1}" -f (Get-Date -Format 'yyyyMMddHHmmss'), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
    $connectionString = "Server=$localDbServer;Database=$databaseName;Integrated Security=True;TrustServerCertificate=True;"
    # Save the safety variable's existing value (if any) so we can restore it afterward.
    # The persistent runtime variable MESP_SQLSERVER_CONNECTION_STRING is intentionally
    # left completely unchanged throughout; the two variables serve distinct purposes.
    $previousSafetyConnectionString = $env:MESP_SQLSERVER_SAFETY_CONNECTION_STRING

    try {
        $env:MESP_SQLSERVER_SAFETY_CONNECTION_STRING = $connectionString
        Push-Location $repositoryRoot

        Write-Host '--- Backend restore ---'
        & dotnet restore .\backend\MiniErp.sln
        if ($LASTEXITCODE -ne 0) { throw 'Backend restore failed.' }

        Write-Host '--- Backend Release build ---'
        & dotnet build .\backend\MiniErp.sln --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'Backend Release build failed.' }

        Write-Host '--- Backend full regression (includes SQL Server LocalDB probes and the safety-catalogue validator) ---'
        & dotnet test .\backend\MiniErp.sln --configuration Release --no-restore --no-build
        if ($LASTEXITCODE -ne 0) { throw 'The full backend regression failed.' }

        Push-Location (Join-Path $repositoryRoot 'frontend')
        try {
            Write-Host '--- Angular unit tests ---'
            & npm test -- --watch=false --no-progress
            if ($LASTEXITCODE -ne 0) { throw 'Angular unit tests failed.' }

            Write-Host '--- Angular production build ---'
            & npm run build
            if ($LASTEXITCODE -ne 0) { throw 'Angular production build failed.' }

            Write-Host '--- Playwright Foundation journeys ---'
            & npm run test:e2e
            if ($LASTEXITCODE -ne 0) { throw 'Playwright Foundation journeys failed.' }

            Write-Host '--- Production dependency audit ---'
            & npm audit --omit=dev --audit-level=high
            if ($LASTEXITCODE -ne 0) { throw 'npm audit reported a high or critical production vulnerability.' }
        }
        finally {
            Pop-Location
        }

        Write-Host '--- git diff --check (working tree against HEAD) ---'
        & git diff --check
        if ($LASTEXITCODE -ne 0) { throw 'git diff --check reported working-tree whitespace errors.' }

        Write-Host '--- git diff --check (branch delta against origin/main) ---'
        & git diff --check origin/main...HEAD
        if ($LASTEXITCODE -ne 0) { throw 'git diff --check reported whitespace errors in the branch delta against origin/main.' }

        Write-Host "Foundation validation passed against disposable database $databaseName."
    }
    finally {
        # Nested so environment-variable restoration is guaranteed even if
        # Pop-Location, the best-effort database removal, or the final
        # orphan-database proof itself throws.
        try {
            Pop-Location -ErrorAction SilentlyContinue

            try {
                Remove-FoundationDatabase -SqlCmdPath $sqlCmd -DatabaseName $databaseName
            }
            catch {
                Write-Warning "Best-effort direct removal of '$databaseName' reported: $($_.Exception.Message). Continuing to the final orphan-database proof."
            }

            Assert-NoOrphanFoundationDatabasesRemain -SqlCmdPath $sqlCmd
        }
        finally {
            if ($null -eq $previousSafetyConnectionString) {
                Remove-Item Env:MESP_SQLSERVER_SAFETY_CONNECTION_STRING -ErrorAction SilentlyContinue
            }
            else {
                $env:MESP_SQLSERVER_SAFETY_CONNECTION_STRING = $previousSafetyConnectionString
            }
            # MESP_SQLSERVER_CONNECTION_STRING is the persistent runtime variable and
            # is never modified by this script; no restoration is needed.
        }
    }
}
finally {
    if ($ownsValidationLock) {
        $validationMutex.ReleaseMutex()
    }
    $validationMutex.Dispose()
}

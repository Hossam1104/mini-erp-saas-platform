$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sqlLocalDb = 'C:\Program Files\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe'
if (-not (Test-Path -LiteralPath $sqlLocalDb)) {
    throw "SQL Server LocalDB was not found at the approved machine path: $sqlLocalDb"
}

& $sqlLocalDb info MSSQLLocalDB | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw 'The MSSQLLocalDB instance is not available.'
}

& $sqlLocalDb start MSSQLLocalDB | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw 'The MSSQLLocalDB instance could not be started.'
}

$databaseName = "MiniErpFoundation_{0}_{1}" -f (Get-Date -Format 'yyyyMMddHHmmss'), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=$databaseName;Integrated Security=True;TrustServerCertificate=True;"
$previousConnectionString = $env:MESP_SQLSERVER_CONNECTION_STRING

try {
    $env:MESP_SQLSERVER_CONNECTION_STRING = $connectionString
    Push-Location $repositoryRoot

    & dotnet restore .\backend\MiniErp.sln
    if ($LASTEXITCODE -ne 0) { throw 'Backend restore failed.' }

    & dotnet build .\backend\MiniErp.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Backend Release build failed.' }

    & dotnet test .\backend\tests\MiniErp.ArchitectureTests\MiniErp.ArchitectureTests.csproj --configuration Release --no-restore --no-build --filter 'FullyQualifiedName~SqlServerSafetyTests'
    if ($LASTEXITCODE -ne 0) { throw 'The targeted SQL Server safety suite failed.' }

    & dotnet test .\backend\MiniErp.sln --configuration Release --no-restore --no-build
    if ($LASTEXITCODE -ne 0) { throw 'The full backend regression failed.' }

    Write-Host "Foundation validation passed against disposable database $databaseName."
}
finally {
    Pop-Location
    if ($null -eq $previousConnectionString) {
        Remove-Item Env:MESP_SQLSERVER_CONNECTION_STRING -ErrorAction SilentlyContinue
    }
    else {
        $env:MESP_SQLSERVER_CONNECTION_STRING = $previousConnectionString
    }
}

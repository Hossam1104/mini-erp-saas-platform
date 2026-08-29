# MiniERP local development and integrated runtime guide

This guide covers the current merged-main ERP boundary. MESP-136 is accepted
and merged; MESP-137 and later capabilities are inactive. Local runtime
behavior is not a production-readiness claim.

## Recommended startup

From the repository root:

```powershell
dotnet build .\backend\MiniErp.sln --configuration Release
.\scripts\Start-MiniErpDevelopment.ps1 -Restart
```

The launcher starts the built Release API, selects an available loopback API
port, writes an ignored Angular proxy for that exact URL, starts the frontend,
waits for both processes, and prints the final URLs. It does not stop
unrelated services. Rebuild after source changes before restarting.

The normal examples use API `5300` and frontend `4300`; the generic API
default is `5000` when available:

```powershell
.\scripts\Start-MiniErpDevelopment.ps1 -ApiPort 5300 -FrontendPort 4300 -Restart
```

Use `-ValidateOnly` to validate the launcher and proxy without starting the
applications. `scripts/Test-MiniErpDevelopmentRuntime.ps1` is the no-process
configuration check.

## Development authentication

By default the launcher prompts for `MESP_DEV_ADMIN_PASSWORD` without
displaying or persisting it. The local login is `admin@minierp.local` and the
password is the value supplied to the running API.

For an explicit local QA loop, enable the Development-only, loopback-only
server shortcut:

```powershell
[Environment]::SetEnvironmentVariable('MESP_DEV_AUTH_BYPASS', 'true', 'User')
[Environment]::SetEnvironmentVariable('MESP_DEV_TENANT_DISPLAY_NAME', 'Wafra', 'User')
```

The shortcut authenticates only the server-configured Development actor. It
does not accept a browser-supplied identity, Tenant, role, permission, or
context. Disable it when testing ordinary credentials:

```powershell
[Environment]::SetEnvironmentVariable('MESP_DEV_AUTH_BYPASS', $null, 'User')
```

The Development host boundaries are `localhost` for common entry,
`tenant.localhost` for a generic Tenant host, and `admin.localhost` for the
platform-administration boundary. Hostnames are routing inputs only; the API
performs exact membership authorization.

## Runtime endpoints

Replace `5300` with the API port printed by the launcher:

| Endpoint | Purpose |
| --- | --- |
| `http://localhost:5300/health` | health |
| `http://localhost:5300/openapi/v1.json` | generated OpenAPI |
| `http://localhost:5300/scalar` | local Scalar rendering |
| `http://localhost:5300/api/v1/module-registration` | module identity |
| `http://localhost:4300` | Angular application |

The browser uses relative `/api` requests through the generated proxy. Do not
hardcode `localhost` or a port in Angular services or components.

## Browser/session recovery

If a stale local session remains, remove only MiniERP cookies for the current
origin (`MiniErp.Auth`, `MiniErp.AntiForgery`, or the host-prefixed equivalents)
and sign in again. Do not clear unrelated localhost cookies.

The normal sequence is:

1. `POST /api/v1/auth/sign-in`, or `/api/v1/auth/development-bypass` when the
   explicit shortcut is enabled.
2. `GET /api/v1/auth/session`.
3. `GET /api/v1/auth/entry` for server-resolved entry mode and Tenant.
4. `GET /api/v1/auth/antiforgery` before a write.
5. `POST /api/v1/auth/operational-context-switch` only for a permitted
   Company/Branch context.

Legacy context routes remain compatibility surfaces; they do not grant extra
Tenant authority.

## SQL Server development database

When `MESP_SQLSERVER_CONNECTION_STRING` is nonblank, Development uses SQL
Server as the authoritative local provider. The expected local persistent
target is server `.` and database `MESP`; startup does not auto-migrate in
production. Development applies the formal module migration sequence.

The six module contexts and tenancy context have separate schemas and history
tables. Inspect migrations using the Infrastructure project as both EF project
and startup project, for example:

```powershell
dotnet ef migrations list --project .\backend\src\MiniErp.Infrastructure --startup-project .\backend\src\MiniErp.Infrastructure --configuration Release --no-build --context TenantPersistenceDbContext
dotnet ef database update --project .\backend\src\MiniErp.Infrastructure --startup-project .\backend\src\MiniErp.Infrastructure --configuration Release --no-build --context TenantPersistenceDbContext
```

Do not use the persistent database for destructive safety tests.

## SQL safety separation

The two connection variables are not interchangeable:

| Variable | Role | Allowed target |
| --- | --- | --- |
| `MESP_SQLSERVER_CONNECTION_STRING` | application runtime | persistent local `MESP` when explicitly configured |
| `MESP_SQLSERVER_SAFETY_CONNECTION_STRING` | destructive test harness | disposable LocalDB `MiniErpFoundation_*` only |

The canonical safe runner is:

```powershell
.\scripts\Test-MiniErpBackend.ps1
```

It creates and removes its disposable LocalDB database and restores the
safety environment variable in a `finally` path. If LocalDB is unavailable,
report SQL validation as gated.

## Development data cutover

The separate local tool is inventory-first and is not a production migration:

```powershell
dotnet run --project .\backend\tools\MiniErp.DevelopmentDataCutover\MiniErp.DevelopmentDataCutover.csproj --configuration Release -- --inventory
dotnet run --project .\backend\tools\MiniErp.DevelopmentDataCutover\MiniErp.DevelopmentDataCutover.csproj --configuration Release -- --verify
dotnet run --project .\backend\tools\MiniErp.DevelopmentDataCutover\MiniErp.DevelopmentDataCutover.csproj --configuration Release -- --apply
```

It verifies source hashes and IDs and writes recoverable local backups. Do not
use it to claim backup/restore, cutover, or production readiness.

## Manual two-process fallback

If the launcher cannot be used, start the API first and pass the same target to
the Angular proxy:

```powershell
Set-Location .\backend
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:MESP_DEV_AUTH_BYPASS = 'false'
$env:MESP_DEV_BOOTSTRAP_ENABLED = 'true'
$env:Scalar__Enabled = 'true'
$env:MESP_DEV_ADMIN_LOGIN = 'admin@minierp.local'
$env:MESP_DEV_ADMIN_PASSWORD = '<YOUR-LOCAL-PASSWORD>'
dotnet run --project .\src\MiniErp.Api\MiniErp.Api.csproj --configuration Release --no-build --urls http://localhost:5300
```

In a second terminal:

```powershell
Set-Location .\frontend
npm start -- --port 4300 --proxy-config ..\.runtime\proxy.conf.json
```

Prefer the launcher because it generates the proxy and avoids configuration
drift. Never commit passwords, connection strings, generated proxy files, or
local runtime output.

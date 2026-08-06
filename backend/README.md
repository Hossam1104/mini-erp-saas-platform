# Mini ERP backend foundation

This directory contains the Foundation backend. It began as the MESP-57
Modular Monolith seam and now also carries the merged MESP-58/MESP-87 Tenant
context and persistence guardrails, the MESP-59/MESP-88/MESP-89 identity,
authorization and host-security seam, the MESP-60 REST/OpenAPI contracts, the
MESP-62 immutable audit and observability evidence, and the
MESP-61/MESP-91 durable-work, notification and private-file contracts.

It is still **not** a production system and implements no ERP business
workflow. Identity, sessions, audit, durable work, notifications and private
files are bounded in-memory or local seams; there is no production database,
migration, SQL work provider, broker, object-storage provider, notification
provider or deployment. The durable-work runtime is not composed into
`MiniErp.Api` at all. SQL Server evidence comes from disposable LocalDB probes
only. MESP-48 and MESP-50 remain open production gates.

## Prerequisites

- .NET SDK 10.0.302 (the repository pins this SDK in `global.json`)

## Commands

Run these commands from `backend/`:

```powershell
dotnet restore .\MiniErp.sln
dotnet build .\MiniErp.sln --configuration Release
dotnet test .\tests\MiniErp.ArchitectureTests\MiniErp.ArchitectureTests.csproj --configuration Release --no-restore
dotnet run --project .\src\MiniErp.Api\MiniErp.Api.csproj --configuration Release --no-build
```

While the API is running, verify the composition evidence:

```powershell
Invoke-RestMethod http://localhost:5000/health
Invoke-RestMethod http://localhost:5000/api/v1/module-registration
```

The port may be selected by the local ASP.NET Core launch environment; use the
URL printed by `dotnet run` when it differs from port 5000.

## Three-project direction

- `MiniErp.Contracts` contains only stable public module contracts and module
  identity records. It has no dependency on application internals.
- `MiniErp.App` contains the composition entry point and the internal Platform
  Administration implementation. The internal implementation is not public.
- `MiniErp.Api` is the host. It registers the Platform module through
  `PlatformModuleRegistration` and consumes only the public contract.

The permitted dependency direction is `MiniErp.Api -> MiniErp.App ->
MiniErp.Contracts`. `MiniErp.Api` also references `MiniErp.Contracts` for its
composition type. Contracts never reference the host or application internals;
the application never references the host. The architecture tests enforce the
forbidden directions and the absence of a cycle.

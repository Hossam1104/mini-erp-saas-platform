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
`MiniErp.Api` at all, and (as of the MESP-92 H92-06 correction, 7 August 2026)
`MiniErp.Api` no longer has `InternalsVisibleTo` friend access to
`MiniErp.App`'s internal durable-work ledger either — only
`MiniErp.ArchitectureTests` is granted that access. SQL Server evidence comes
from disposable LocalDB probes only. MESP-48 and MESP-50 remain open
production gates. MESP-96 added only non-persistent Master Data/Catalog and
Business Parties boundary contracts, Tenant/scope authorization hooks, stable
reference contracts, and audit/evidence integration; it did not add Master
Data entities, migrations, endpoints, or database access.

> **Current MESP-100/MESP-99 handoff - 9 August 2026.** MESP-100 is Done
> with closure evidence Jira comment `10663`; PR #32 merged at
> `511f6be9f005e54930f993aead9758d7a66b75a8`. MESP-99 is In Progress as the
> single active Category/UOM implementation item, and the root TASK.md now
> contains only that exact session. MESP-100 added no Category/UOM persistence
> or business behavior. The SQL Server harness remains gated by the explicit
> `MESP_SQLSERVER_CONNECTION_STRING` configuration.

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

## Four-project direction

- `MiniErp.Contracts` contains only stable public module contracts and module
  identity records, including the Master Data/Catalog and Business Parties
  composition seams and their non-persistent shared value contracts. It has no
  dependency on application, provider, or host internals.
- `MiniErp.App` contains the composition entry points, server-derived Tenant
  context consumption, policy-neutral scope and authorization hooks, and the
  internal Platform, Master Data/Catalog, and Business Parties
  implementations. Internal implementations are not public and App does not
  reference EF Core or Infrastructure.
- `MiniErp.Infrastructure` is the provider/persistence implementation project.
  It depends on App and Contracts, owns provider-specific EF Core code, and
  keeps future business-module contexts, mappings, schemas, and migrations in
  explicit module-owned areas. MESP-100 adds no Category/UOM persistence.
- `MiniErp.Api` is the host and composition root. It references App,
  Contracts, and Infrastructure; it registers host/application seams directly
  and will call Infrastructure registration methods when provider-backed
  composition is due.

The approved project-reference direction is `MiniErp.Api ->
MiniErp.Infrastructure -> MiniErp.App -> MiniErp.Contracts`, with Api also
referencing App and Contracts for existing host composition. Contracts never
reference the host/application/provider; App never references the host or
Infrastructure; Infrastructure never references the host. Architecture tests
enforce the project graph, forbidden directions, public persistence surface,
and absence of a cycle.

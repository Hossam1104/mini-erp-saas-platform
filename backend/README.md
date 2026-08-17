# Mini ERP backend foundation

> **Current MESP-143 runtime overlay - 17 August 2026.** The backend now
> carries the merged MESP-143 Tenant-aware entry routing, candidate host
> resolution, exact server-side membership authority, operational Company/Branch
> context switching, generic branding, and SAR presentation metadata, alongside
> the bounded Master Data, Business Parties, Purchase Request, and Supplier
> Quotation/comparison source-decision slices. With a nonblank
> `MESP_SQLSERVER_CONNECTION_STRING`, exact local `Development` uses the
> formal module-owned SQL Server migrations against server `.` / database
> `MESP`; the SQLite provider remains an explicit fallback when that setting is
> absent. Production startup never auto-migrates.
>
> The SQL Server safety-harness tests are run via a dedicated disposable
> LocalDB connection assigned only to `MESP_SQLSERVER_SAFETY_CONNECTION_STRING`.
> That variable is the exclusive input for the destructive create/drop lifecycle;
> `MESP_SQLSERVER_CONNECTION_STRING` (the persistent runtime variable) is never
> read by the safety harness. Use `scripts/Test-MiniErpBackend.ps1` or
> `scripts/validate-foundation.ps1` to run the full suite safely.
>
> This is still not a production deployment: MESP-48/MESP-50, production
> topology, deployment migrations, backup/restore, capacity, and specialist
> gates remain open. The next selected capability is MESP-124 (Purchase Order
> and Supplier Confirmation).

This directory contains the Foundation backend. It began as the MESP-57
Modular Monolith seam and now also carries the merged MESP-58/MESP-87 Tenant
context and persistence guardrails, the MESP-59/MESP-88/MESP-89 identity,
authorization and host-security seam, the MESP-60 REST/OpenAPI contracts, the
MESP-62 immutable audit and observability evidence, and the
MESP-61/MESP-91 durable-work, notification and private-file contracts.

It is still **not** a production system. Identity, sessions, audit, durable
work, notifications and private files remain bounded in-memory or local seams;
there is no production deployment, broker, object-storage provider,
notification provider, or production migration process. The durable-work
runtime is not composed into
`MiniErp.Api` at all, and (as of the MESP-92 H92-06 correction, 7 August 2026)
`MiniErp.Api` no longer has `InternalsVisibleTo` friend access to
`MiniErp.App`'s internal durable-work ledger either — only
`MiniErp.ArchitectureTests` is granted that access. SQL Server evidence comes
from disposable LocalDB probes only. MESP-48 and MESP-50 remain open
production gates. MESP-96 added only non-persistent Master Data/Catalog and
Business Parties boundary contracts, Tenant/scope authorization hooks, stable
reference contracts, and audit/evidence integration; it did not add Master
Data entities, migrations, endpoints, or database access.

> **Historical MESP-100/MESP-99 handoff - 9 August 2026.** MESP-100 is Done
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

## MESP-143 entry boundary

The API resolves entry mode from a normalized `TenantHostBinding` registry and
then combines that candidate with the authenticated server-side Identity
membership. Hostname is never authorization. `MESP_ENTRY_COMMON_HOSTS` and
`MESP_ENTRY_PLATFORM_HOSTS` configure common/platform hosts; indexed
`MESP_TENANT_HOST_BINDINGS` entries configure active Tenant bindings with
`Host`, `TenantId`, optional `CanonicalHost`, and optional `Active=false`.

Forwarded host/proto headers are ignored unless the request came through a
proxy IP listed in `MESP_TRUSTED_PROXY_IPS`. No client Tenant header is read.
`GET /api/v1/auth/entry` returns the bounded entry contract, including only
authorized ordinary Tenant choices, Tenant-host identity, safe platform/no-
access states, configured branding, SAR presentation metadata, and the
post-Overview Company/Branch context list. `POST
/api/v1/auth/operational-context-switch` uses the existing organization-scope
authority and optimistic eligibility/selection versions.

This seam does not create a second Tenant persistence model or migration. DNS,
TLS, full Platform Administration, external providers, and downstream ERP
effects remain outside MESP-143.

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
  currently owns the module contexts, mappings, schemas, migrations, design-
  time factories, and local SQL provider composition for Tenancy, Master Data,
  Business Parties, and Procurement. MESP-123 B2 keeps each migration history
  distinct and leaves shared `TenantOwnedRecords` physically owned by Tenancy.
- `MiniErp.Api` is the host and composition root. It references App,
  Contracts, and Infrastructure; it registers host/application seams directly
  and selects SQL Server or SQLite through Infrastructure based on the explicit
  local environment configuration.

The approved project-reference direction is `MiniErp.Api ->
MiniErp.Infrastructure -> MiniErp.App -> MiniErp.Contracts`, with Api also
referencing App and Contracts for existing host composition. Contracts never
reference the host/application/provider; App never references the host or
Infrastructure; Infrastructure never references the host. Architecture tests
enforce the project graph, forbidden directions, public persistence surface,
and absence of a cycle.

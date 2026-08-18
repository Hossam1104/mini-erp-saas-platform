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
> MESP-124 adds source-decision-gated Purchase Orders, immutable source and
> commercial snapshots, reuse of approval/SoD/delegation, issue evidence,
> manual full/partial/rejected/no-response confirmations, supplier-proposed
> changes with controlled reapproval, history/audit, and formal Procurement
> persistence. It adds no stock, receipt, invoice, AP/accounting, payment, or
> external supplier effects. MESP-48/MESP-50, production topology, deployment
> migration governance, backup/restore, capacity, and specialist gates remain
> open; the branch is awaiting independent pre-merge review.

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

## MESP-124 Procurement persistence and API

The Purchase Order implementation remains inside the existing four-project
modular-monolith direction. Public request/response records are in
`MiniErp.Contracts`; application commands, validation, source-lineage
revalidation, and approval orchestration are in `MiniErp.App`; SQL/SQLite EF
entities, mappings, queries, and the formal migration are in
`MiniErp.Infrastructure`; and literal REST handlers plus Foundation/OpenAPI
metadata are in `MiniErp.Api`.

The `procurement` schema owns Purchase Orders, lines, confirmations,
confirmation lines, evidence, supplier changes, history, and audit. Each new
entity is Tenant-owned and registered with the stored-owner verifier. Every
read/write receives the server-derived Tenant and Company/Branch context; the
client cannot widen scope or invent source IDs. The official backend evidence
entry point remains `scripts/Test-MiniErpBackend.ps1`, which uses a disposable
LocalDB safety target and leaves the persistent `MESP` connection untouched.

Idempotency replay is validated against a deterministic server-side SHA-256
request fingerprint (`PurchaseOrderAudit.RequestFingerprint`, added by the
additive migration `AddPurchaseOrderAuditRequestFingerprint`), not only the
Tenant/actor/operation/key tuple. An identical retry replays the original
result deterministically; the same key reused against a different payload or
a different target returns HTTP 409 `idempotency_conflict` rather than ever
replaying an unrelated Purchase Order's result.

That replay evidence is durable and is consulted before state-dependent
business validation. `IPurchaseOrderPersistence.ProbeReplayAsync` exposes a
read-only three-way probe (NotFound / Replay / Conflict) over the persisted
Tenant-scoped audit evidence, and `PurchaseOrderService` calls it only after
the trusted Tenant context, the current target, and the caller's authority
over that target have been established — but before lifecycle-state gates,
optimistic-concurrency comparison, approval-stage state, approval-policy and
delegation resolution, and supplier-change/reapproval validation. An identical
retry of a command whose original success already advanced the order therefore
still replays instead of returning `submit_not_allowed`, `decision_not_allowed`,
`issue_not_allowed`, `confirmation_not_allowed`, or
`supplier_change_approval_not_allowed`, and it survives both expiry of the
volatile ten-minute REST-layer idempotency cache and an API process restart.
Replay is never an authorization bypass: it is matched on the exact actor, so
separation of duties, delegation, and Tenant/Company/Branch authority still
have to be satisfied by the current request, and a genuinely new create still
runs full current source-decision validation. The in-transaction
persistence-side replay check remains in place as defense in depth.

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

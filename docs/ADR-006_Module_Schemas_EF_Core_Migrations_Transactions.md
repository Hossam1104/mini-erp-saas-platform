# ADR-006 — Module schemas, EF Core contexts, migrations, and cross-module transactions

| Field | Decision |
|---|---|
| Status | Foundation implementation baseline; production validation remains gated |
| Date | 4 August 2026 |
| Owners | Solution Architecture / Persistence Engineering |
| Related Jira | MESP-61, MESP-64, MESP-48, MESP-50 |
| Supersedes | None |

## MESP-123 B2 local-provider reconciliation — 16 August 2026

The bounded B2 implementation makes the shared SQL Server shape executable for
local Development without changing the production gate. When the explicit
`MESP_SQLSERVER_CONNECTION_STRING` is configured, the four module contexts
use server `.` / database `MESP` and apply formal migrations in this order:
Tenancy, Master Data, Business Parties, then Procurement. Each context has a
distinct `dbo.__EFMigrationsHistory_*` table. `tenancy.TenantOwnedRecords` is a
shared runtime table but has one physical owner: the Tenancy context. The
other module alignment migrations are no-op database migrations whose model
snapshots reflect that shared ownership; they do not create duplicate tables.

The Development startup migrator is exact-environment-only and production
startup never calls it. A bounded inventory-first cutover utility moved the
existing local SQLite rows into the empty `MESP` database with a recoverable
backup, preserved IDs/Tenant IDs and foreign-key lineage, verified source
hashes, and retained the SQLite originals. This is local Development evidence,
not a production migration, deployment, backup/restore, capacity, HA/DR,
residency, retention, or MESP-48/MESP-50 approval.

## MESP-124 Purchase Order persistence reconciliation - 17 August 2026

The bounded Purchase Order and Supplier Confirmation slice preserves this ADR's
module ownership. `ProcurementDbContext` owns the `procurement` tables for
Purchase Orders, lines, confirmations, confirmation lines, evidence, supplier
changes, lifecycle history, and audit. All eight entity types are Tenant-owned
and registered with the stored-owner verifier; Company/Branch scope remains an
application-authorized context inside the already authorized Tenant.

The formal EF migration
`20260817143432_PurchaseOrderAndSupplierConfirmation` adds these tables and
indexes without introducing a second database, a competing migration history,
or a shared-table ownership change. The application revalidates the approved
Purchase Request, Supplier Quotation, Source Decision, supplier, currency,
scope, and selected lines before creating immutable source/commercial snapshots.
The migration is a Development/runtime artifact subject to the existing
production migration, supported-volume, retention, privacy, backup/restore,
and cutover gates. The official disposable SQL safety runner remains the only
accepted full backend safety entry point.

## Context

Release 1 uses a shared SQL Server database with strict application-layer
Tenant isolation and database schemas separated by business module. The
Foundation work needs a bounded persistence seam for durable work, outbox and
inbox records without turning the modular monolith into a collection of
microservices or granting a worker a global business-data query path.

## Decision

1. Each module owns its EF Core model, mappings, repositories and schema
   namespace. A module may reference shared contracts/building blocks only
   through the approved dependency direction; it must not reach another
   module's DbContext or tables directly.
2. The shared SQL Server database remains the Release 1 deployment shape. A
   Tenant-owned row carries an immutable TenantId and the persistence guard
   verifies stored ownership on modified and deleted entities before saving.
3. Cross-module business effects are coordinated in the application layer and
   use one explicit transaction boundary when the owning module requires it.
   A transaction is not an excuse to bypass Tenant checks, query filters or
   authorization evidence.
4. MESP-61 may provide provider-neutral contracts and a deterministic local
   adapter. It does not choose a SQL deployment topology or claim production
   migration readiness.
5. MESP-64 owns disposable SQL Server provider validation, schema/index/
   concurrency probes and the evidence report. B2's local `MESP` migrations
   and data cutover are a separate Development convenience and do not replace
   the disposable safety gate or the separately reviewed production delivery
   step.

## Alternatives considered

- A database per Tenant was rejected because the approved direction is one
  shared database with strict isolation.
- Microservices and a distributed transaction coordinator were rejected for
  the one-developer modular-monolith scope.
- A single unrestricted shared DbContext was rejected because it obscures
  module ownership and makes cross-Tenant access easier to introduce.
- SQLite-only evidence is insufficient for SQL Server-specific semantics and
  is therefore limited to fast local contract tests.

## Consequences and guardrails

- New module entities require an owning module, Tenant ownership decision,
  schema mapping, index/unique-key review and targeted tests.
- `IgnoreQueryFilters`, raw SQL, bulk operations and maintenance paths remain
  restricted to an explicit privileged boundary and are not available to
  ordinary Tenant calls.
- The first production migration must be reviewed against the approved BRDs,
  MESP-48 supported-volume evidence and MESP-50 retention/privacy/legal-hold/
  purge requirements.
- SQL Server Row-Level Security is not selected here. ADR-016 remains the
  production decision record for adoption or formal deferral.

## Explicitly deferred

This ADR does not decide production region, provider/vendor, backup or restore
targets, retention durations, legal hold, purge execution, residency, or RLS.
Those decisions remain owned by the approved MESP-48/MESP-50 gates and the
applicable production ADRs.

## Evidence expected from MESP-64

The final Foundation safety report must identify which assertions are covered
by architecture tests, local provider tests and disposable SQL Server tests.
It must show stored-owner update/delete denial, same-Tenant relationship
integrity, unique/index behavior, rowversion/stale-write behavior and
transaction atomicity without touching a production or shared database.

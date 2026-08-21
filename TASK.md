# MESP-128 — Sol Acceptance Prompt: Inventory Ledger Foundation

Reviewer: GPT-5.6 Sol

Repository: D:\AI Tools\Hossam\mini-erp-saas-platform

Capability: MESP-128 — Inventory Ledger Foundation

Branch: feat/MESP-128-inventory-ledger-foundation

Exact main base SHA: f54b6abe383edd304911eb0a53db43fafdcb3066

Initial implementation commit: 9893b41

SOL P1 remediation commit: 0cd5defd109dceffead9bf4781c7f271275bffed

Jira: MESP-128 is IN PROGRESS under activation comment 11745. Jira,
Confluence, and other Jira tracker writes are prohibited in this acceptance
session. Sol owns the read-only acceptance decision.

Delivery state: the branch must remain open, Draft, and unmerged. Exactly one
Draft PR against main is permitted. Do not merge, rebase, force-push, create a
second PR, or start MESP-129/MESP-130/MESP-131.

## SOL P1 remediation completion / FULL re-acceptance handoff

The required stock-integrity remediation was implemented from the exact
starting head `cec9fee911a4d4ba14867a358f852ebd36a89fba` without rebase, merge,
force-push, Jira writes, or downstream capability work. The remediation adds
durable provenance fingerprints independent of request idempotency, a filtered
unique consumed-source database boundary, fail-closed posting for any
quarantined opening row, cumulative reservation-safe correction serialized by
the existing stock-identity anchors, authoritative active Master Data UOM
codes, and executable coverage for the required duplicate/replay/conflict,
Tenant/source, UOM, tracking, correction, and real independent-context race
cases.

Final implementation evidence: focused Inventory **15/15**; full backend
**865/865 passed, 0 skipped** including disposable LocalDB SQL safety;
Release build **0 warnings / 0 errors**; Angular **241/241 across 32 spec
files**; production initial **499.97 kB** and Inventory lazy chunk **25.83
kB**; focused Chromium **2/2**; full Chromium **26/26**; both npm audits **0
vulnerabilities**; formal migration apply/drop passed; and `git diff --check`
is clean. The migration history includes
`20260821132738_MESP128StockIntegrityRemediation` after the original
`20260821113311_MESP128InventoryLedgerFoundation` migration.

The current Product contract still provides only boolean `TrackingEnabled`, so
enabled identity-required and disabled identity-rejected semantics are
enforced; complete batch/lot/serial/manufacturing/expiry mode enforcement
remains an upstream Product-model limitation. Production percentages remain
unchanged because this is correctness remediation. This file is the FULL Sol
re-acceptance handoff; do not create an Opus prompt yet.

## Acceptance boundary

Accept only the bounded Inventory-owned physical-stock foundation delivered by
MESP-128:

- an append-only stock ledger whose posted movement is the canonical physical
  quantity effect;
- controlled Tenant/Company/Branch/Warehouse opening-balance provenance with
  validation/quarantine, posting, reconciliation totals, and correction by
  linked reversal movements;
- server-derived OnHand, Reserved, and Available projections, with truthful
  zero Expected, Damaged, and InTransit facts because those later workflows
  have not posted Inventory facts;
- explicit Tenant/Warehouse/Product/UOM reservations with full or partial
  allocation, visible unallocated quantity, controlled reduce and release,
  immutable history, audit, idempotency, and concurrency;
- configuration-led tracking validation only where the existing Product
  contract enables it;
- Foundation REST/OpenAPI metadata, antiforgery, mandatory audit, durable
  idempotency replay/conflict, If-Match/ETag concurrency, Tenant query filters,
  a formal Inventory migration, and a bilingual responsive EN/AR RTL Angular
  workspace.

Inventory owns stock truth. Purchase Orders, Supplier Confirmations, Goods
Receipt commercial evidence, Purchase Invoice Handoff, and Procurement
Supplier Return evidence do not create or decrease Inventory stock in this
capability. No direct cross-module persistence may be introduced.

## Repository facts to verify

Confirm the implementation follows the inspected repository rather than
inventing architecture:

- the repository has separate Contracts, App, Infrastructure, and Api
  projects;
- Product exposes configuration-led TrackingEnabled as a boolean; it does not
  expose a tracking-kind enum or authorize GS1/EAN/statutory serialization;
- Product persistence exposes the base UOM identity but the current provider
  does not expose a base-UOM code, so alternate-UOM use fails closed;
- Warehouse is a configured provider seam and is not a newly invented
  persisted Warehouse module;
- Tenant authority and operational scope are server-derived from Foundation;
  client query/body Tenant IDs cannot widen scope;
- unsafe REST operations use the existing Foundation operation catalogue,
  antiforgery, mandatory audit, idempotency, and If-Match conventions;
- the Inventory schema, entities, indexes, ownership verifier, design-time
  factory, SQL migration history, and migration are Inventory-owned.

Any acceptance finding that depends on a missing upstream Product tracking
kind, UOM conversion authority, or persisted Warehouse contract must be
reported as an upstream limitation. Do not authorize a guessed replacement.

## Sol acceptance checklist

### Ledger ownership and immutability

1. Confirm StockMovement is Inventory-owned and append-only.
2. Confirm each posted movement retains Tenant, Company, Branch where
   applicable, Warehouse snapshot, Product/SKU snapshot, UOM, quantity,
   direction, source type/document/line, actor, posted/effective dates,
   correlation, idempotency/evidence, unit cost, currency, and correction
   linkage.
3. Confirm no SetBalance, UpdateOnHand, DirectBalanceAdjustment, mutable
   StockBalance source of truth, or direct posted-movement edit path exists.
4. Confirm opening posting creates exactly one immutable inbound movement per
   valid row and does not create a PO, Goods Receipt, invoice, AP, tax, or GL
   effect.
5. Confirm correction retains the original movement and adds linked outbound
   correction movement(s), with the projection changing exactly once.

### Opening balance

6. Confirm an opening batch is Tenant-bound and requires an authorized
   Company/Branch/Warehouse scope.
7. Confirm Product, active state, Inventory relevance, base-UOM identity,
   tracking requirements, positive quantity, non-negative unit cost, and
   currency evidence are server validated.
8. Confirm invalid rows are quarantined with a deterministic validation code
   and valid rows remain reconcilable.
9. Confirm Draft -> Validated -> Posted -> Corrected transitions require
   current If-Match versions and cannot edit a posted batch.
10. Confirm a newly created batch has a non-empty concurrency version.
11. Confirm durable idempotency with the same key and fingerprint replays the
    original result without a duplicate ledger effect; a different
    fingerprint conflicts deterministically.
12. Confirm opening history/audit preserves actor, session, Tenant, scope,
    correlation, idempotency, reason, timestamps, before/after, and lineage.

### Projections

13. Confirm OnHand is calculated only from posted Inventory movements.
14. Confirm Reserved is calculated only from active Inventory reservations.
15. Confirm Available is max(0, OnHand - Reserved) and never negative.
16. Confirm Expected, Damaged, and InTransit are not inferred from
    Procurement evidence and remain truthful zero/not-applicable values until a
    later Inventory-owned posting capability exists.
17. Confirm reservations do not append stock movements and do not change
    OnHand.

### Reservations and stock integrity

18. Confirm a reservation retains Tenant, Company/Branch, Warehouse, Product,
    UOM, tracking identity when required, bounded source type/reference,
    requested, reserved, and unallocated quantities.
19. Confirm a full reservation allocates available quantity exactly.
20. Confirm a partial reservation is explicit and preserves unallocated
    quantity.
21. Confirm reduce restores the reduced quantity to Available and release
    restores the complete reserved quantity to Available.
22. Confirm released reservations cannot be reduced or released again.
23. Confirm stale reduce/release requests fail without false success.
24. Confirm the same reservation retry is durable and does not duplicate
    reserved quantity.
25. Confirm concurrent reservations against the same stock identity cannot
    over-allocate: two requests for 7 against OnHand 10 may produce 7+3 or a
    deterministic conflict, but never 14.
26. Confirm no negative-stock override, force, warning-only bypass, role
    exception, Warehouse exception, expiry exception, or valuation bypass was
    added.

### Tracking and upstream boundaries

27. Confirm untracked Products do not require a tracking identity.
28. Confirm the existing Product boolean tracking configuration requires a
    tracking identity for opening/reservation/availability operations where it
    is enabled and rejects tracking data when disabled.
29. Confirm tracking identity remains Tenant- and stock-scope-isolated.
30. Confirm no GS1/EAN ownership, statutory serialization, external recall,
    barcode-scanner subsystem, or advanced WMS behavior was invented.

### Tenant, scope, and authorization

31. Confirm all business reads and writes resolve Tenant authority from the
    server-owned Foundation context.
32. Confirm exact permission and authorization-path metadata is checked.
33. Confirm wrong Tenant, Company, Branch, Warehouse, inactive Warehouse,
    inactive Product, non-inventory Product, and unsupported UOM requests fail
    closed.
34. Confirm client-supplied IDs are lookup inputs only and never authorization
    authority.
35. Confirm EF Tenant query filters and ownership verification cover every
    Inventory entity, including history, audit, replay, and concurrency anchor
    records.

### Audit and REST/OpenAPI

36. Confirm every unsafe Inventory operation requires antiforgery, mandatory
    audit, durable idempotency, and the declared concurrency policy.
37. Confirm audit records distinguish successful effects, validation failures,
    authorization denials, persistence outages, replay, and conflict rather
    than misclassifying an outage as a genuine denial.
38. Confirm ledger list/detail, availability, opening list/read/history/audit
    and create/validate/post/correct, and reservation list/read/history/audit
    and create/reduce/release are registered in the Foundation catalogue.
39. Confirm unsafe routes have stable operation IDs, Inventory tags, metadata,
    and OpenAPI/Scalar visibility.
40. Confirm replay returns the original result, sets the established replay
    evidence, and cleans up in-progress idempotency state on failed execution.

### Persistence and migration

41. Confirm migration
    20260821113311_MESP128InventoryLedgerFoundation is formal, non-empty, and
    creates the Inventory-owned tables, indexes, constraints, and concurrency
    structures.
42. Confirm the model snapshot contains all Inventory entities.
43. Confirm SQLite compatibility is preserved for local tests and SQL Server
    design-time/runtime migration registration is present.
44. Confirm no persistent runtime connection was changed and no disposable
    safety database remains after the canonical runner.

### Angular workspace

45. Confirm /app/inventory is lazy-routed behind the authenticated shell and
    has no raw client Tenant selector.
46. Confirm human-readable server Warehouse, Product/SKU, and UOM labels are
    used; raw GUIDs are not primary business labels.
47. Confirm OnHand, Reserved, Available, Expected, Damaged, and InTransit are
    visibly distinct.
48. Confirm opening source/provenance, date, quantity, unit-cost evidence,
    currency, validation, posting, reconciliation, and correction controls
    are visible with safe server errors.
49. Confirm reservation requested/reserved/unallocated, partial allocation,
    reduce, release, and tracking-conditional controls are visible.
50. Confirm EN/AR copy, RTL/LTR direction, keyboard labels, responsive layout,
    reduced motion, accessible status/error presentation, and no raw
    translation-key leakage.
51. Confirm the browser sends no Tenant ID in the availability query and sends
    antiforgery, Idempotency-Key, and If-Match evidence for unsafe actions.

## Explicit non-scope

Do not accept or start any of the following as part of MESP-128:

- MESP-129 Goods Receipt authoritative stock posting, transfer, shipment,
  InTransit lifecycle, transfer receipt, or physical return posting;
- MESP-130 Stock Adjustment, Inventory Count, or Stock Issue;
- MESP-131 complete Moving Weighted Average valuation or reconciliation;
- Sales Orders, Delivery, customer credit control, AP, AR, GL, Finance
  periods, payment/settlement, tax accounting, revaluation, or FX;
- ZATCA/FATOORA, external providers, supplier portal, advanced WMS,
  production cutover, DNS/TLS, or Wafra-specific core behavior.

## Required validation

Run from the repository root:

    dotnet build backend/MiniErp.sln -c Release

    dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj -c Release --filter FullyQualifiedName~InventoryLedgerTests

    scripts/Test-MiniErpBackend.ps1 -NoBuild:$false

The canonical backend result must report the exact total and skipped count,
execute disposable LocalDB SQL safety when available, leave the persistent
MESP_SQLSERVER_CONNECTION_STRING unchanged, and leave no disposable database.

Run from frontend:

    npm test -- --watch=false --no-progress
    npm run build
    npm run test:e2e -- inventory.spec.ts --project=chromium
    npm run test:e2e -- --project=chromium
    npm audit --omit=dev
    npm audit

The initial production bundle must remain below the existing 500 kB budget;
do not raise the budget. Also run git diff --check and git status. Verify
frontend/assets has no changes.

The completing implementation evidence is: Release build 0 warnings/0
errors; focused Inventory 15/15; full backend 865/865 passed, 0 skipped;
Angular 241/241 across 32 spec files; 499.97 kB initial bundle with 25.83 kB
Inventory lazy chunk; focused Chromium 2/2; full Chromium 26/26; both npm
audits 0 vulnerabilities; formal disposable migration apply/drop passed; and
git diff --check clean.

## Sol decision and delivery rules

Inspect the complete task-related diff and the final branch status. Accept
only if the checklist and validation evidence are satisfied. If a bounded
correction is genuinely required, document the exact finding and keep the
change within MESP-128; do not widen scope. Do not write Jira, do not merge,
do not force-push, and do not start the next capability. Do not automatically
request Opus from this handoff; any later independent stock-integrity review
is a separate Owner/Sol routing decision after acceptance.

Final acceptance report must state the base SHA, branch, implementation SHA,
final SHA, single Draft PR number, migration identity, exact validation
counts, any accepted limitation, and explicit confirmation that the branch
remains unmerged with no Jira writes and no downstream implementation.

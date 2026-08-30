# Mini ERP backend foundation

<!-- MESP-144-CURRENT-START -->
> **MESP-144 HOLD 5 merge-safety record - 30 August 2026.**
> MESP-144 reconciliation reached Sol content acceptance at comment `12293` on
> reviewed head `ffe5a8975611dcc85c3a7c40dce0b3737b123aeb`. HOLD 5 authority is
> comment `12296` and exists only to make repository state merge-safe. At the
> HOLD 5 executor handoff, Jira and PR lifecycle had not yet been finalized;
> Jira and GitHub remain authoritative for their respective final states.
> The pre-reconciliation main baseline was `4d6e33189a3835d5d8d2a58736055a837a3f5bc9`.
> MESP-137 is Done/accepted/merged; no implementation capability is active;
> MESP-138/139 remain To Do/inactive; fast-track is `21/26 = 80.8%`; and
> production readiness remains approximately `47%` overall / `41%` P2P.
>
> This is documentation/state reconciliation only. No backend source, tests,
> migrations, assets, Jira state, or later capability changed in this checkpoint.
<!-- MESP-144-CURRENT-END -->

<!-- MESP-131-HISTORICAL-MERGED-START -->
> **MESP-131 Opus P1 financial-correctness remediation - 24 August 2026.** Drifted-average corrections fail closed as Blocked evidence with `correction_would_orphan_residual_value`, stop only the affected valuation scope, and never persist an impossible zero-quantity/non-zero-value state. Physical quantity arithmetic preserves Stock Ledger `decimal(28,8)` precision; `AmountScale` remains monetary-only and reconciliation compares exact stored quantities. No schema migration was required.
>
> **Final validation.** Final P1 correction-quantity commit `64c4f4ea9b917119d07cb26df7ecac8c2239bfac`; focused valuation `44/44`; combined Inventory regression `89/89`; SQL safety `40/40`; canonical disposable-LocalDB backend `963/963` with 0 failed/0 skipped; Release build 0 warnings/0 errors; Angular `254/254`; initial bundle `499.94 kB`; valuation lazy chunk `35.96 kB`; focused/full Chromium `5/5` and `32/32`; both npm audits 0 vulnerabilities; `frontend/assets` untouched. The final correction regression proves `1.005 - 0.001 = 1.004` without monetary quantity rounding. PR #75 is merged into `main`; no Jira writes were performed.
> **Current MESP-131 merged-main overlay â€” 24 August 2026; MESP-131 and MESP-8 are Done in Jira.** Inventory now contains a deterministic Company-scoped `LedgerSequence` for movement ordering, versioned decimal Moving Weighted Average valuation policy/state/evidence, exact MESP-120 Exchange Rate snapshots, Pending/Blocked predecessor handling, append-only correction/reversal history, Warehouse Transfer/In-Transit value lineage, Inventory reconciliation, Finance handoff facts, and bounded valuation/report/export REST surfaces. Finance posting remains downstream: no Journal, GL, AP, AR, tax, payment, fiscal-period, Sales, generic Reporting, statutory, migration/cutover, external-provider, or Wafra-specific reusable core behavior is introduced.
>
> **MESP-131 accepted validation.** Focused valuation `44/44`; combined Inventory regression `89/89`; SQL Server safety `40/40` against disposable LocalDB; canonical disposable-LocalDB backend `963/963` with 0 failed/0 skipped; Release build 0 warnings/0 errors; Angular `254/254` across 35 specs; initial bundle `499.94 kB`; valuation lazy chunk `35.96 kB`; focused Chromium `5/5`; full Chromium `32/32`; both npm audits 0 vulnerabilities; `frontend/assets` untouched. PR #75 is merged into `main`; no Jira writes were performed.
<!-- MESP-131-HISTORICAL-MERGED-END -->

<!-- MESP-134-HISTORICAL-START -->
> **MESP-134 Tax / FX / Reporting Currency / Revaluation HOLD 2 - 26 August 2026.**
> MESP-133 is Done and merged at `3c616dd85b9cebb53990934321f1ae7d0d5410c9`.
> MESP-134 is implemented on the only active Finance branch under MESP-10,
> pending Draft PR #78, which remains Open/Unmerged for GPT-5.6 Sol acceptance. The backend
> reuses MESP-119 Tax, MESP-120 Exchange Rates, MESP-132 posting authority,
> and MESP-133 AP/AR/settlement history for monetary policy, exact Reporting
> evidence, tax effects, realized FX, revaluation, and reconciliation. HOLD 1
> adds immutable journal monetary evidence, source snapshots, posting-rule
> lineage, supplier-declared-tax evidence, reconciliation feeds, and provider-
> realistic SQL concurrency coverage. HOLD 2 corrects one-sided allocation
> monetary evidence, replaces SQL REV03 with the real revaluation/allocation
> race, and adds direct Tax, historical FX, realized FX, revaluation, and EN/AR
> error regressions. Final validation is Release 0/0, focused MESP-134 24/24,
> backend 1052/1052, SQL safety 70/70, REST/OpenAPI/host 55/55, and clean EF
> model-change detection. No Jira writes, Opus review, merge, Ready transition,
> external provider, ZATCA/FATOORA, Sales lifecycle, or MESP-135 work is in
> scope.
<!-- MESP-134-HISTORICAL-END -->

<!-- MESP-133-HISTORICAL-START -->
> **MESP-133 AP / AR / cash settlement verification-only HOLD 4 - 25 August 2026.** The
> backend now consumes trusted MESP-126 Finance-ready evidence with an
> authoritative historical payment-term/version snapshot and reproducible due
> date, reuses `IFinanceSourceApprovalPolicy` for settlement SoD, enforces
> internal manual-only payment methods, and validates settlement direction,
> Company scope, lifecycle, currency, and effective dates at posting time.
> Cash/bank posting must match the selected account's linked GL account through
> the configured Posting Rule. AP/AR reconciliation derives active subledger
> balances and actual posted/reversed journal lines; aging/exposure use
> accounting-date as-of allocation semantics. Rejected settlements return to
> Draft only through the server-side correction path. Realized FX and external
> providers remain fail-closed/deferred to MESP-134 and later scope.
>
> **MESP-133 validation.** HOLD 4 directly instantiates the real
> `ProcurementFinanceSupplierInvoiceSourceProvider` with bounded authoritative
> dependency fakes and adds four provider cases plus one historical recognition
> rule A→B persistence regression. Focused remediation coverage is `16/16`, the
> `54/54` REST/OpenAPI/host contract suite, and 15 named MESP-133 SQL race
> tests; the complete SQL safety class is `61/61` against disposable LocalDB,
> the canonical disposable-LocalDB backend is `1014/1014`
> with 0 failed/0 skipped, and Release build is 0 warnings/0 errors. The
> additive migration remains `20260824220208_MESP133ApArCashSettlement`; no
> migration edit or Owner-managed asset change was made. PR #77 remains Open,
> Draft, and unmerged on `feat/MESP-133-ap-ar-cash-settlement` for Sol HOLD
> `11926`/`11963`/`11967` re-review (with Finance Epic `11927`/`11964`/`11968` and manual-AR
> supplemental finding `11928`); MESP-132 is Done/merged/closed and no Jira writes were
> performed. The focused source/test commit is
> `b9eba368922899165324086aa59298d054fec25d`; HOLD 3 implementation commit is
> `a9c46a27349cb617770277699ad74456262b81c4`; HOLD 4 test commit is
> `7cf177e8eaf694824a91b8b5b0cf3642d0f049f7`.
<!-- MESP-133-HISTORICAL-END -->

> **Historical MESP-129 runtime overlay - 22 August 2026.** The backend now
> consumes authoritative Procurement Goods Receipt lines for one-time accepted
> quantity posting, blocks Goods Receipt cancellation while an active physical
> effect exists, and consumes only the real `AwaitingInventory` Supplier Return
> source for reservation-safe outbound physical movement with cumulative
> same-stock-identity capacity validation and complete
> Supplier Return/Goods Receipt/Purchase Order lineage. Inventory-owned
> Warehouse Transfers support server-authorized same-Company direct and
> two-step flows, derived InTransit, partial receipt, explicit shortage/loss,
> overage rejection, and safe pre-shipment cancellation. Physical MESP-129
> movements are explicitly Pending valuation with nullable cost/currency. The
> customer-return surface is an unavailable authoritative Sales integration
> seam. No MESP-130 Count/Adjustment/Stock Issue, MESP-131 MWA, AP/AR/GL,
> tax/payment, commercial Sales, external/statutory, or Wafra-specific behavior
> is included.

> **MESP-129 validation.** Release build is **0 warnings / 0 errors**;
> focused Inventory coverage is **33/33**; focused Goods Receipt/Supplier
> Return coverage is **23/23**; SQL Server safety coverage is **29/29**; the
> canonical disposable-LocalDB backend runner passes **896/896, 0 skipped**;
> Angular passes **241/241 across 32 spec files**;
> production build is **499.97 kB initial** with a **33.12 kB Inventory lazy
> chunk**; Chromium is **26/26**; both npm audits report **0 vulnerabilities**;
> `git diff --check` is clean; protected `frontend/assets` are untouched; and
> no Jira writes were performed. The bounded P1 source/test commit is
> `a824e8a`; Draft PR **#73** remains open, Draft, and unmerged.

> **Historical MESP-128 runtime overlay - 22 August 2026.** The backend carries
> the bounded Inventory-owned append-only stock ledger foundation plus the SOL
> P1 remediation and Opus delta remediation: deterministic Tenant-safe
> opening-source fingerprints independent of extraction time and request
> idempotency, filtered unique consumed-source race protection, fail-closed
> quarantine posting, reservation-safe cumulative correction using shared
> stock-identity anchors, provider-independent mutable anchor touches, narrow
> SQL Server 1205/1222 contention classification, and authoritative active
> Master Data UOM-code snapshots. No Goods Receipt stock posting, transfer,
> Stock Adjustment/Count/Issue, MWA, Finance/AP/GL, payment,
> external/statutory, or Wafra-specific behavior is included.

> **MESP-128 validation.** Release build is **0 warnings / 0 errors**;
> focused Inventory module/persistence/architecture coverage is **17/17**;
> SQL Server safety coverage is **26/26**; the canonical disposable-LocalDB
> backend runner passes **871/871, 0 skipped**; Angular passes **241/241 across
> 32 spec files**; production build is **499.97 kB initial** with a **25.82 kB
> Inventory lazy chunk**; focused
> Chromium is **2/2**, full Chromium is **26/26**, and both npm audits report
> **0 vulnerabilities**. The persistent runtime connection is unchanged,
> disposable migration apply/rollback/reapply/drop passed, protected
> frontend/assets remain untouched, and no Jira writes were performed.

> **Historical MESP-127 runtime overlay - 21 August 2026.** The backend now
> carries Procurement-owned Supplier Returns from accepted Goods Receipt
> evidence through Draft, Submitted, Approved, rejection/cancellation,
> Inventory-facing handoff evidence, Finance-facing correction/credit
> references, completion, reversal, and forward-linked correction. Eligibility
> is derived from accepted receipt quantity less active non-reversed returns;
> rejected receipt quantity and the MESP-125 non-additive damage overlay never
> become return quantity. Source PO/GR/supplier/product/UOM/warehouse snapshots,
> private-file evidence references, immutable history/audit, durable replay,
> optimistic concurrency, Tenant/Company/Branch/Warehouse authorization, and
> operational report rows are persisted in Procurement. No stock ledger,
> on-hand, Inventory valuation, AP, GL, tax posting, payment, or authoritative
> Finance/Inventory event is fabricated. The formal migration is
> `20260821031935_MESP127SupplierReturnEvidence`.

> **MESP-127 validation baseline.** Release build is **0 warnings / 0
> errors**; the canonical disposable-LocalDB backend runner passes **844/844,
> 0 skipped**; focused Supplier Return architecture coverage passes **3/3**;
> Angular passes **239/239 across 31 spec files**; production build is
> **494.71 kB initial** with a **57.40 kB Supplier Return lazy chunk**;
> focused Supplier Return Chromium coverage is **2/2** and the full Chromium
> suite is **24/24**; both production-only and full npm audits report **0
> vulnerabilities**. Protected `frontend/assets` remain untouched.

> **Historical MESP-126 runtime overlay - 21 August 2026.** The backend now
> carries deterministic three-way matching as Procurement evidence
> orchestration: independent supplier-declared invoice evidence is stored
> separately from the MESP-125 PO-derived handoff preview, exact-safe and
> configured runtime tolerance policies are selected by Tenant/
> Company/Branch scope and snapshotted with evaluations, current partial-handoff
> quantity and cumulative accepted/confirmed source limits are enforced,
> over/under supplier declarations remain truthful evidence, MESP-120
> Exchange Rate references are resolved server-side and snapshotted, and
> authorized exception resolution records reason, history, audit, idempotency,
> optimistic concurrency, and policy-driven SoD. This slice does not post AP,
> GL, tax accounting, stock, payment, FX revaluation, ZATCA/FATOORA, or
> external integrations.

> **Historical MESP-125 runtime overlay - 19 August 2026.** The backend carries
> the complete MESP-125 Goods Receipt and Purchase Invoice Handoff slice alongside
> the merged MESP-124 Purchase Order and Supplier Confirmation slice, MESP-143
> Tenant-aware entry routing, candidate host resolution, exact server-side
> membership authority, operational Company/Branch context switching, generic
> branding, and SAR presentation metadata, as well as the bounded Master Data,
> Business Parties, Purchase Request, and Supplier Quotation/comparison source-decision
> slices.
>
> With a nonblank `MESP_SQLSERVER_CONNECTION_STRING`, exact local `Development`
> uses the formal module-owned SQL Server migrations against server `.` /
> database `MESP`; the SQLite provider remains an explicit fallback when that
> setting is absent. Production startup never auto-migrates.
>
> The SQL Server safety-harness tests are run via a dedicated disposable
> LocalDB connection assigned only to `MESP_SQLSERVER_SAFETY_CONNECTION_STRING`.
> That variable is the exclusive input for the destructive create/drop lifecycle;
> `MESP_SQLSERVER_CONNECTION_STRING` (the persistent runtime variable) is never
> read by the safety harness. Use `scripts/Test-MiniErpBackend.ps1` or
> `scripts/validate-foundation.ps1` to run the full suite safely.
>
> MESP-125 provides warehouse-authorized Goods Receipts from Confirmed POs,
> strict physical partition (`Received = Accepted + Rejected`), descriptive
> condition overlay (`Damaged <= Received`), commercial remainder tracking,
> over-receipt prevention, receipt cancellation, Purchase Invoice Handoff with
> pro-rata tax allocation preview, handoff cancellation, durable idempotent
> replay, history/audit, and EF Core persistence with optimistic concurrency.
> It adds no inventory/warehouse movement postings, general ledger journals,
> AP subledger liabilities, or Finance posting outside FIN-OD-01 / PD-046.
> MESP-48/MESP-50, production topology, deployment migration
> governance, backup/restore, capacity, and specialist gates remain open.

The accepted MESP-126 validation baseline is Release build **0 warnings / 0
errors**; focused Invoice Handoff/matching remediation tests pass **37/37** and
the canonical backend runner passes **841/841 tests, 0 skipped**, including all
**22 SQL safety tests** against a disposable LocalDB `MiniErpFoundation_*`
database. The P1 remediation also verifies the public FX request is identity-
only, server version selection uses immutable supplier-invoice-date evidence,
missing dates fail closed, repeated declared lines aggregate by Purchase Order
line, and repeated allocations aggregate by Goods Receipt line without
double-consuming valid receipt evidence. Exact Company/Branch scope remains
server-derived and explicit; no broader Company-to-Branch inheritance policy
is introduced. The previous repository baseline was **812/812** ArchitectureTests
passed with **0 skipped**, including the disposable SQL Server safety harness.
Focused Goods Receipt tests pass **11/11**, focused Purchase Invoice Handoff
tests pass **8/8**, focused Purchase Order tests pass **14/14**, and the full
suite directly covers physical receipt quantity invariants A through I,
concurrent race prevention (10 -> 7/7), warehouse scoping, and durable replay.

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

- .NET SDK 10.0.400 (the repository pins this SDK in `global.json`)

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

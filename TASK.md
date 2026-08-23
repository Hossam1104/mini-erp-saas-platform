# MESP-131 - Sol Acceptance Handoff

<!-- MESP-131-JIRA-SYNC-START -->
## Jira/documentation synchronization â€” 23 August 2026

Jira traceability has been reconciled without closing MESP-131:

- MESP-131 remains In Progress; implementation handoff comment `11779`.
- MESP-8 Inventory Epic is In Progress; progress comment `11780`.
- MESP-54 FX consumption comment `11781`.
- MESP-53 report-boundary comment `11782`.
- MESP-113 Inventory-policy consumption comment `11783`.
- MESP-120 Exchange Rate consumption comment `11784`.
- MESP-132 downstream Finance handoff comment `11785`; status remains To Do.
- MESP-139 downstream Reporting source comment `11786`; status remains To Do.
- Sol acceptance comment `11788` and delta acceptance comment `11789` remain
  the independent review authority.

Draft PR #75 remains unmerged and Sol acceptance is still required.
<!-- MESP-131-JIRA-SYNC-END -->

Repository: `D:\AI Tools\Hossam\mini-erp-saas-platform`

Capability: MESP-131 - Moving Weighted Average valuation, reconciliation, and
Inventory valuation reporting.

Branch: `feat/MESP-131-mwa-valuation-reconciliation`

Exact required main base: `b470179e1d18ef75c0a9247b2340407da6220dc4`

Starting SHA: `1beca1a02eddcab675a92ae1d0f1915bfca5089f`

Remediation implementation SHA: `958339d395323106e83b59caeb3b64bbcd0758fd`

Final branch SHA: recorded in the final documentation handoff commit and completion response.

Draft PR: `#75` - Open, Draft, Unmerged; base `main`.

Jira is read-only for this bounded session. No Jira writes were performed.
MESP-131 remains In Progress until Sol accepts the exact final branch SHA.
Do not mark the PR Ready, merge, rebase, force-push, create another PR, or
start MESP-132 automatically. Sol owns the independent delta acceptance
handoff; no Opus prompt is created by this session.

## Repository Facts Confirmed

- Implementation started from the exact required main base above.
- `frontend/assets` is an Owner-managed source boundary and has zero changes.
- No Journal, JournalLine, GL, AP, AR, tax, payment, bank, fiscal-period,
  Sales, generic Reporting, external provider, statutory, ZATCA/FATOORA,
  DNS/TLS, migration/cutover, or Wafra-specific core behavior was added.
- MESP-130 physical movements remain upstream inputs; MESP-132+ owns Finance.

## Sol Delta Acceptance Handoff

The remediation implementation is complete for Sol review. The exact bounded
delta includes:

- LedgerSequence-authoritative mutation ordering, with AsOf mutation removal
  and unsafe TrackingIdentity process filters removed;
- policy-pool continuity and monotonic policy versioning, including compatible
  carry-forward and fail-closed incompatible transitions;
- durable missing-policy predecessor evidence and same-pool blocking;
- empty-state positive CurrentMovingAverage guard;
- outbound use of the persisted prior rounded average and configured precision;
- exact full correction reversal and deterministic partial correction;
- conserved InTransit quantity/value for receipt, loss, and return resolution;
- bounded SHA-256 REST/correction fingerprints, durable process/policy replay,
  real conflict semantics, and first-scope concurrency safety;
- Finance Direction, absolute non-negative BaseAmount, SignedBaseAmount, and
  `inventory-valuation-finance.v1` contract semantics;
- dedicated multi-Product Warehouse summary, explicit Partial/Pending/Blocked
  truthfulness, safe current-state reconciliation filters, and complete pending
  counts;
- Angular aggregate-summary, pending-state, Finance-handoff, EN/AR/RTL, and
  lazy-route truthfulness.

The additive migration, focused MESP-131 tests, SQL Server/LocalDB safety tests,
full backend suite, Angular suite, focused and full Chromium suites, bundle
budget, npm audits, runtime restart/HTTP evidence, and protected asset check
are the acceptance evidence for the exact final branch tip. Sol owns the next
independent delta acceptance; do not start downstream implementation.

## Ledger Ordering

### Company Ledger Sequence

Every Inventory movement-producing path receives the next durable `long`
LedgerSequence from a Tenant + Company anchor. Valuation orders by
LedgerSequence and movement ID, never by PostedAt or EffectiveDate.

### Existing Movement Bootstrap

Migration `20260823124304_MESP131MovingWeightedAverageValuation` adds the
sequence and anchor, deterministically backfills existing rows by Tenant,
Company, PostedAt, and Id, then initializes each anchor to MAX + 1.

### Movement-Producing Paths

Opening Balance, Goods Receipt, Stock Adjustment, Inventory Count Variance,
Stock Issue, Supplier Return, Customer Return seam, Transfer Shipment,
Receipt, Loss, Return, and physical corrections use the same sequence path.

## Valuation Policy

### Scope

Tenant-owned, Company-specific, effective-dated, versioned policy. Scope is
Warehouse/Product/UOM or Warehouse/Product/UOM/TrackingIdentity.

### Functional Currency

Active Master Data currency identity and normalized code are server-validated;
policy/version/currency are snapshotted into state, evidence, handoff, report,
and export output.

### Precision / Rounding

Decimal-only calculations use bounded quantity/unit-cost/amount scales and
ToEven or AwayFromZero rounding. Negative state and over-issue are rejected.

### Goods Receipt Cost Basis

Authoritative Purchase Order line unit price and source currency are resolved
through Procurement; exact active effective-dated MESP-120 FX is required when
currencies differ. Client base cost is never authority.

### Return / Adjustment Policies

Supplier Return uses configured CurrentMovingAverage or LinkedReceiptValuation.
Positive Stock Adjustment and Inventory Count Variance use configured current
MWA. Customer Return without original delivery valuation remains Pending.

## MWA Engine

### Formula

Inbound: `Qnew=Qprior+Q`, `Vnew=Vprior+(Q*baseUnitCost)`. Outbound:
`Qnew=Qprior-Q`, `Vnew=Vprior-(Q*priorAverage)`. Average is `Vnew/Qnew`,
rounded by policy.

### Valuation State

Durable state is per Tenant/Company/Branch/Warehouse/Product/UOM/(tracking)/
physical valuation pool, never per policy version. It stores quantity, value,
average, last valued LedgerSequence, current policy metadata, currency,
timestamps, and concurrency token. Scope anchors use the same pool identity.

### Append-Only Evidence

Immutable events store source document/line/lineage, policy/currency/precision,
transaction cost, exchange-rate identity/version/scale/provenance, prior/new
state, movement value, status, correlation, actor, and occurrence time.

### Pending Predecessor

Missing policy/cost/FX, unresolved source, transfer shipment, or original
delivery evidence produces explicit Pending/Blocked evidence and later events
in the same scope do not leap over the stopped predecessor.

### Backdated Movements

Backdated EffectiveDate does not reorder the physical ledger. LedgerSequence
processing applies the event and records `backdated_applied` when appropriate.

## Source Valuation

- **Opening Balance:** source unit cost/currency with exact FX snapshot.
- **Goods Receipt:** Purchase Order line price and source currency.
- **Exchange Rate:** active exact source/target/effective MESP-120 version only;
  no inversion, default, or ambiguous rate.
- **Stock Adjustment:** configured current-MWA positive basis; outbound current
  average with non-negative state protection.
- **Inventory Count Variance:** MESP-130 physical movement, valued by the
  configured positive-adjustment/current-MWA rule; no accounting effect.
- **Stock Issue:** outbound current-MWA cost with source lineage.
- **Supplier Return:** current MWA or linked receipt evidence; missing link is
  Pending rather than fabricated cost.
- **Customer Return Boundary:** Sales-owned original delivery valuation is
  required; no Sales or AR implementation was added.

## Corrections

### Physical Correction Valuation

Corrections append a new linked movement. Full reversal uses original movement
value exactly; partial reversal is deterministic quantity pro-rata. The event
stores a signed reversal movement value.

### Valuation Source Revision

The correction API requires authoritative source-revision ID, reason,
antiforgery, idempotency, correlation, and audit. Persistence truthfully
returns `authoritative_source_revision_provider_required` until that provider
exists; prior evidence is never edited and cost is never invented.

### Immutable History

`CorrectionOfMovementId` and `CorrectionOfValuationEventId` link correction
history. History/export expose the chain without mutating the original event.

## Warehouse Transfer Valuation

Shipment is outbound source-warehouse MWA. In-transit is shipped less received
quantity and inherited shipment value. Receipt, loss, and return preserve
Transfer lineage and inherit shipment evidence; missing shipment valuation is
Pending.

## Concurrency / Idempotency

Serializable process transaction, durable scope anchors, optimistic tokens, and
actor + idempotency-key + request-fingerprint replay are implemented. A
concurrent valuation loser maps to a safe conflict. Mutations require
antiforgery, Idempotency-Key, correlation, server Tenant context, authorization,
audit, and safe errors. Client amounts, MWA values, base amounts, and FX are
not authority.

## Tenant / Company / Warehouse Authorization

Tenant is resolved from trusted server context. Policy, movement, state,
evidence, report, reconciliation, export, and handoff queries are
Tenant-filtered. Company/Branch/Warehouse access is server-authorized and
foreign scope fails closed without leakage; client Tenant IDs never authorize.

## Audit / History

Policy creation, process coordination, correction request, and CSV export
carry actor, correlation, idempotency, authorization path, and audit evidence.
History is immutable; export is bounded to 10,000 rows and records filters.

## Finance Handoff Boundary

Applied evidence emits `ReadyForFinance` facts under
`inventory-valuation-finance.v1`, with source/policy/currency/cost/amount/FX,
evidence ID/version, sequence, and correlation. Pending/NotConfigured/
ReadyForFinance are explicit. Inventory creates no journal, GL, AP, AR, tax,
payment, period, or financial reversal effect.

## Reconciliation

Inventory-owned reconciliation compares physical on-hand with valued quantity
and exposes valued amount, MWA cost, eligible/applied/pending/blocked counts,
latest physical and valued sequences, oldest pending sequence, in-transit
quantity/value, Finance handoff state, policy/currency, as-of, and freshness.
Statuses include Reconciled, PendingValuation, Blocked, QuantityMismatch, and
FinanceHandoffPending. No balancing plug is returned.

## Inventory Valuation Reports

The bounded views are Summary, MWA Cost History, Pending/Blocked, Inventory
Reconciliation, In-Transit Valuation, Finance Handoff, and correction history
through the immutable history chain. Filters cover Company, Branch, Warehouse,
Product, UOM, Tracking, source type, status, policy, currency, effective date,
and LedgerSequence.

## Export

`GET /api/v1/inventory/valuation/export` is Tenant-authorized and returns a
bounded CSV containing filters, as-of, freshness, functional currency,
policy/version, generated actor/correlation, and immutable event evidence. It
creates an `inventory.valuation.export` audit row and no public link or
external-storage artifact.

## EN / AR / RTL

Lazy `/app/inventory/valuation` extends Inventory with summary, history,
pending/blocked, reconciliation, in-transit, handoff, blocked/as-of/freshness,
and export controls. It uses server-provided warehouse context, supports EN/AR
labels and RTL, and has no raw GUID entry.

## API / OpenAPI

Catalogue-backed operation IDs cover policy, process, state, history,
summary, pending, reconciliation, in-transit, export, Finance handoff, and
correction seams. Safe problem responses and Tenant scope are documented; the
export is a file response.

## Migration / Legacy Bootstrap / SQL Safety

Formal migrations: `20260823124304_MESP131MovingWeightedAverageValuation` and
the additive remediation `20260823180537_MESP131SolFinancialIntegrityRemediation`.
Legacy sequence bootstrap is deterministic and evidence is preserved. The
remediation migration is separate and the original MESP-131 migration remains
unchanged. The disposable SQL Server LocalDB safety harness passed the final
source with schema, ownership, migration, sequence, concurrency, and valuation
checks. No production SQL/provider/cutover decision was made.

## Validation Totals

- Focused MESP-131 valuation: `27/27`.
- Prior Inventory regression (ledger + stock control + valuation): `52/52`.
- SQL Server safety harness: `38/38` against disposable LocalDB (previous
  baseline `32`).
- Full backend LocalDB harness: `944/944`, `0` failed, `0` skipped.
- Release solution build: `0` warnings, `0` errors.
- Angular: `254/254` across 35 spec files.
- Production bundle: initial `499.94 kB`; valuation lazy `35.96 kB`; no
  initial-budget warning.
- Focused Playwright: `5/5`; full Playwright: `32/32`.
- Both npm audit modes: `0` vulnerabilities.
- `git diff --check`: checked before the documentation handoff commit.

## Runtime Verification

Backend `http://localhost:5300`, PID `36540`; frontend
`http://localhost:4300`, PID `21248`. Backend health, frontend root, and
`main.js` each returned HTTP 200 after the official `Start-MiniErpDevelopment.ps1
-Restart` launcher run. Both launcher-owned processes remain running for Owner
inspection. Loopback-only Development auth bypass was used without printing or
persisting credentials.

## Known Limitations / Deferred Finance Policy

- Authoritative revised-source persistence is a provider-required seam and is
  intentionally unavailable; correction never fabricates valuation.
- Finance owns GL mapping, account/period validation, balanced journals,
  subledger reconciliation, AP/AR, corrections, and reversals; MESP-132+ owns
  that work.
- Formal migration is source evidence only; production SQL/provider,
  backup/restore, retention, legal, capacity, DNS/TLS, and cutover gates stay
  open.
- No generic Reporting platform, Sales integration, external/statutory
  submission, automated FX, supplier portal, or Wafra-specific core behavior.

## Exact Next Action

Sol performs acceptance against the exact final branch SHA and Draft PR #75.
The PR remains Draft and unmerged. The Owner decides whether to merge after
acceptance. No Jira writes were performed, and no next implementation task is
started automatically.

# MESP-130 - FINAL LEDGER-FENCE REMEDIATION: GPT-5.6 Sol Acceptance Handoff

Reviewer: GPT-5.6 Sol

Repository: `D:\AI Tools\Hossam\mini-erp-saas-platform`

Capability: MESP-130 — Stock Adjustment, Inventory Count, Stock Issue, and
eligible stock-movement corrections.

Branch: `feat/MESP-130-stock-control-corrections`

Exact bounded-session start SHA: `9f5950848217bb992df7770baf93a91fa67b24ca`

Exact main base: `6f6d204726cc4baf9979961ea6936c0d03e93e32`

Prior Sol remediation SHA: `3320cf284d64a58be7fb0f00ac654ee7a11d7b00`

Ledger-fence remediation SHA: `e63bcb3736138d3b3fb57ccd06646b6caf943e75`

Final branch SHA: recorded after the final documentation/runtime handoff
commit and reported in the completion response.

Draft PR: `#74` — Open, Draft, Unmerged; base `main`.

Jira is read-only for this session. No Jira writes were performed. MESP-130
remains In Progress until Sol accepts the exact final branch SHA. Do not mark
the PR Ready, merge, rebase, force-push, create another PR, or start MESP-131,
Finance, Sales, Reporting, migration/cutover, or other downstream work.

## Bounded final delta

- Full Count now establishes a durable warehouse movement-cardinality fence
  inside the Serializable persistence transaction before it reads the
  authoritative ledger identity universe. The identity universe, explicitly
  requested identities, anchor acquisition, expected quantities, cutoff, and
  count lines are resolved in the same transaction. A post-fence movement that
  would introduce a new warehouse identity is therefore blocked until the
  snapshot boundary is complete and cannot be silently omitted.
- Cycle Count remains selected-identity scoped. It records a movement
  cardinality for each selected `Company/Branch/Warehouse/Product/UOM/
  TrackingIdentity`; movement on an unrelated identity remains irrelevant.
- Full Count and Cycle Count movement-cardinality values are persisted as
  `long`/SQL Server `bigint`. Each count generation has an append-only
  `inventory.CountSnapshots` evidence row, and each current count line carries
  its identity cardinality. Recount and resnapshot create new generation rows
  and preserve prior snapshot evidence; they do not overwrite old fence data.
- Posting no longer treats `PostedAt > SnapshotCutoff` as the stale-detection
  authority. It compares the current durable generation fence with the live
  warehouse or selected-identity ledger cardinality and fails closed when the
  generation evidence is absent or changed, returning `ResnapshotRequired`
  without creating a variance movement.
- The formal additive Inventory EF migration is
  `20260823104702_MESP130InventoryCountLedgerFence`, after all existing
  MESP-130 migrations. It adds the fence columns and `CountSnapshots` only;
  it does not alter unrelated model columns or ownership boundaries.
- Deterministic SQL Server regressions pause after the real authoritative
  reader has executed, then prove the concurrent insert is blocked while the
  count transaction holds the fence. Full Count explicitly proves Product B
  has `PostedAt` earlier than the eventual cutoff, is not in the snapshot, and
  still forces `ResnapshotRequired`. Cycle Count proves the same selected-
  identity behavior while unrelated identities remain irrelevant.

## Required acceptance evidence — completed

- Focused Inventory Stock Control tests: `12/12` passed.
- SQL Server safety suite: `32/32` passed through a disposable LocalDB
  `MiniErpFoundation_*` catalog; no persistent runtime database connection was
  used by the safety harness.
- Full backend suite: `911/911` passed, `0` failed, `0` skipped.
- Release solution build: `0` warnings, `0` errors.
- Angular unit tests: `246/246` across `33` spec files.
- Focused MESP-130 Chromium journey: `1/1` passed.
- Full Chromium suite: `27/27` passed.
- Production bundle: initial `499.81 kB`; Inventory lazy chunk `90.11 kB`;
  Supplier Quotation lazy chunk `91.94 kB`; no initial-budget warning.
- `npm audit --omit=dev --audit-level=high`: `0` vulnerabilities.
- `npm audit --audit-level=high`: `0` vulnerabilities.
- `git diff --check`: clean for the source/test/migration delta; final
  documentation diff is checked before the handoff commit.
- `frontend/assets`: zero changes; Owner-managed source assets were not
  deleted, renamed, replaced, regenerated, optimized, recolored, moved, or
  restored.

## Runtime left for Owner inspection

The official `scripts/Start-MiniErpDevelopment.ps1 -Restart` launcher was used
after the final Release build. It selected the safe fallback API port because
the generic port 5000 was occupied:

- Backend: `http://localhost:5300`, PID `31576`; `GET /health` returned HTTP
  `200`.
- Frontend: `http://localhost:4300`, PID `40296`; `GET /` and `GET /main.js`
  returned HTTP `200`.
- Both repository-owned processes were verified alive after the checks and are
  left running.
- The explicit loopback-only Development auth bypass was used. No password or
  other credential was printed or persisted.

## Preserved boundaries

MESP-130 remains Pending-valuation for new physical effects and creates no
Finance, GL, AP, AR, tax, payment, Sales, Reporting, MWA, external, statutory,
ZATCA/FATOORA, DNS/TLS, production-provider, migration/cutover, supplier
portal, or Wafra-specific core behavior. MESP-131 owns MWA valuation.
Unsupported physical sources remain uncorrectable. Return-for-change is not
exposed because this bounded UI has no edit/resubmit contract.

## Exact next action

Sol performs final acceptance against the exact final branch tip and Draft PR
`#74`, then the Owner decides whether to merge. Do not start another
implementation task automatically. No Opus review prompt is created by this
handoff.

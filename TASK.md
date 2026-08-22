# MESP-130 — Sol Acceptance Handoff: Stock Control, Counts, Issues, and Corrections

Reviewer: GPT-5.6 Sol

Repository: `D:\AI Tools\Hossam\mini-erp-saas-platform`

Capability: MESP-130 — Stock Adjustment, Inventory Count, Stock Issue, and
eligible stock-movement corrections.

Branch: `feat/MESP-130-stock-control-corrections`

Exact starting main SHA: `6f6d204726cc4baf9979961ea6936c0d03e93e32`

Implementation SHA: `dc8ea88b035a2b05b7ab560bf00bf87165ff35fb`

Final branch SHA: the final documentation/runtime handoff tip is reported in
the completion response because Git cannot embed a commit's own SHA in that
commit's content.

Draft PR: `#74` — https://github.com/Hossam1104/mini-erp-saas-platform/pull/74
(`MESP-130: Stock Control, Counts, Issues, and Corrections`), base `main`;
it remains Open, Draft, and unmerged.

Jira: read-only. No Jira writes were performed.

MESP-129 is Done. MESP-130 remains In Progress until Sol acceptance. Do not
claim MESP-130 Done from implementation or test activity alone.

## Scope delivered

- Tenant-scoped Inventory reason/purpose catalogue with bilingual names,
  category, active state, durable uniqueness, and lifecycle history.
- Stock Adjustment draft, submit, configurable approval stages with
  delegation seam, reject/return-for-change, post, and correction workflow.
- Full and cycle Inventory Counts with authoritative server snapshots,
  reviewer/counter separation, blind counter view, cutoff, post-cutoff
  movement detection, recount, resnapshot, variance reason, approval, and
  variance posting.
- Stock Issue draft, submit, configurable approval/delegation seam,
  rejection/return-for-change, reservation-safe posting, and correction.
- Corrections limited to MESP-130 Stock Adjustment, Inventory Count Variance,
  and Stock Issue movement sources, with original-movement linkage and durable
  double-correction protection.
- Formal Inventory EF Core migration, REST/OpenAPI metadata, antiforgery,
  ETag/If-Match, idempotency, audit/history, Tenant and operational-context
  authorization, and bilingual Angular EN/AR RTL workflow.

## Non-scope and explicit boundaries

- MESP-128 immutable movement and concurrency-anchor rules remain authoritative.
- MESP-129 physical movements remain authoritative; no rewrite or replacement
  of existing physical history was made.
- MESP-131 owns authoritative Moving Weighted Average valuation. New MESP-130
  movements remain `Pending` valuation and do not create Finance, GL, AP, AR,
  tax, payment, or accounting effects.
- No MESP-131, Finance, Sales, Reporting, MESP-139, MESP-141, migration/cutover,
  external integration, statutory/ZATCA/FATOORA, production DNS/TLS, supplier
  portal, or Wafra-specific core implementation was started.
- Owner-managed source assets under `frontend/assets` were not changed.

## Reason / Purpose catalogue

`InventoryReasonCodeEntity` is Tenant-owned and category-bound to Adjustment,
Count Variance, or Stock Issue. Codes are normalized, unique within their
Tenant/category scope, bilingual, active/inactive, server-validated on every
mutation, and snapshotted onto source lines and correction evidence. An
inactive or cross-Tenant code cannot be used.

## Stock Adjustment

### Lifecycle

Draft → Submitted → PendingApproval or Approved → Posted. Reject and
ReturnForChange are explicit terminal/return transitions with optimistic
concurrency, immutable posted movement evidence, and lifecycle history.

### Positive Adjustment

Creates an immutable inbound `StockAdjustment` movement only after all lines,
reason codes, product/UOM, Tenant/company/branch/warehouse scope, and approval
requirements pass.

### Negative Adjustment

Creates an immutable outbound `StockAdjustment` movement only when resulting
OnHand remains at or above active Reserved quantity. It fails closed on
negative stock or reservation over-consumption and leaves no partial movement.

### Approval / SoD

The application resolves a server-configured approval policy and stage,
rejects self-approval, enforces eligible approver/delegation rules, and
persists approval snapshots/evidence. The default provider is an explicit
no-policy seam for this bounded capability; configured multi-stage providers
are supported without inventing a second authorization hierarchy.

### Reservation / Negative Stock

Outbound capacity is validated from authoritative ledger OnHand and active
reservations under the existing Serializable transaction and deterministic
MESP-128 anchors. A failed line blocks the complete posting transaction.

## Inventory Count

### Full Count

Full counts derive the server-side set of stock identities represented by the
warehouse's immutable movement history. Client-supplied expected quantities are
never authoritative.

### Cycle Count

Cycle counts accept an explicitly selected, deduplicated set of server-validated
product/UOM/tracking identities and require at least one line.

### Blind Count

The dedicated counter view suppresses expected quantities and is authorized
separately from reviewer count reads.

### Snapshot / Cutoff

Creation acquires the relevant MESP-128 anchors and stores authoritative
expected quantities, a cutoff timestamp, warehouse/context snapshots, and
round generation. SQL Server computes the cutoff server-side; SQLite test
execution uses an equivalent materialized cutoff filter for provider-safe
regression coverage.

### Post-Cutoff Movement Handling

Posting detects movements after the count cutoff. It returns an explicit
ResnapshotRequired state when the count can no longer be safely posted against
its snapshot; it does not silently merge later movement into counted values.

### Recount

The reviewer can request a new immutable count round for a variance line. The
recount links to the prior line and increments round generation, preserving
the original observation and snapshot evidence.

### Resnapshot

The reviewer can resnapshot a stale count under the same server-authorized
scope. The new expected quantity is derived from the ledger, not from client
input, and the prior snapshot remains in history.

### Variance

Submitted observed quantities are normalized and validated against the
authoritative snapshot. Non-zero variance requires a Tenant-scoped active
Count Variance reason code. Variance lines preserve product/UOM/tracking,
expected, observed, and variance quantities.

### Approval / SoD

The assigned counter cannot approve their own count, and the designated
reviewer is also excluded from the approval actor. Approval is version-checked
and stage-aware.

### Variance Posting

Approved variance creates immutable inbound or outbound
`InventoryCountVariance` movements. Outbound variance remains reservation-safe;
the physical effect is `Pending` valuation and has no accounting effect.

## Stock Issue

### Purpose

Stock Issue is the controlled outbound physical effect for an authorized
operational issue, separate from Sales, AP, AR, and payment workflows.

### Destination / Use

Every issue requires a server-persisted destination/use description and
Tenant-scoped reason/purpose evidence. No arbitrary external destination
authority or commercial Sales document is fabricated.

### Posting

Draft → Submitted → PendingApproval or Approved → Posted, with one immutable
outbound `StockIssue` movement per line and durable source uniqueness/replay.

### Approval / Authority

Submit, approve, reject/return, and post operations require exact server-derived
Tenant/company/branch/warehouse authorization. Approval and delegation use the
shared configurable policy seam and separation-of-duties checks.

### Capacity / Reservations

Posting validates OnHand minus cumulative issue quantity remains at or above
active Reserved quantity for every affected identity, under Serializable
transaction/anchor protection. Negative stock and reservation consumption fail
closed atomically.

## Tracking / UOM

Product identity, base UOM, allowed tracking configuration, and warehouse
scope are resolved server-side through existing Master Data providers. Count,
adjustment, issue, and correction lines preserve tracking identity and reject
missing/invalid tracking where the product requires it. No variant, SKU, EAN,
or unrelated Item behavior was invented.

## Corrections

### Adjustment Correction

Only a posted `StockAdjustment` movement can be corrected. The correction
creates the exact reversal movement with immutable linkage to the original.

### Count Correction

Only a posted `InventoryCountVariance` movement can be corrected. The reversal
preserves the count source lineage and remains Pending valuation.

### Stock Issue Correction

Only a posted `StockIssue` movement can be corrected. Reservation-safe capacity
is checked for the reversal direction and the original issue linkage remains
auditable.

### Unsupported Source Protection

Goods Receipt, Supplier Return, Warehouse Transfer, opening balance, and every
other non-MESP-130 source type are rejected by the correction service and
persistence layer. A movement can have at most one durable correction through
the unique original-movement correction constraint.

## Valuation Boundary

MESP-130 records physical quantity and immutable source evidence only. MWA,
cost, currency, Finance mapping, balanced journals, Inventory valuation
handoff, reconciliation, reversals, and controlled accounting corrections are
deferred to the approved MESP-131/Finance contract. MESP-130 does not ask for
or infer arbitrary user cost.

## Concurrency

Mutations use optimistic document versions, Serializable transactions, durable
Tenant-scoped uniqueness, and deterministic MESP-128 stock-identity anchor
acquisition before balance/capacity validation and movement creation.

## Idempotency / Source Uniqueness

Public mutations carry bounded idempotency keys and request fingerprints.
Durable replay returns the original effect for the same request and rejects a
same-key different-payload conflict. Source/document and correction linkage
constraints prevent duplicate physical effects.

## Audit / History

Created, submitted, approved, rejected, returned, posted, blocked, corrected,
snapshot, recount, resnapshot, variance, authorization, idempotency, and
concurrency decisions are recorded with actor, correlation, reason, and
before/after status or source evidence. Posted movements remain immutable.

## Migration / SQL safety

Formal migration: `20260822194250_MESP130StockControlAndCorrections`. It adds
eight Inventory control tables and preserves Tenant-owned schema/table/index
ownership. SQL safety coverage was updated for the new migration and expected
Inventory topology.

## Validation evidence

- MESP-130 focused Inventory tests: 3/3 passed.
- REST/OpenAPI structural tests: 33/33 passed.
- Full ArchitectureTests backend suite: 899/899 passed, 0 failed, 0 skipped.
- Angular unit tests: 242/242 passed across 32 spec files.
- Focused Inventory Playwright: 2/2 passed; full Chromium Playwright: 26/26
  passed.
- `npm audit --omit=dev`: 0 vulnerabilities; `npm audit`: 0 vulnerabilities.
- Angular production build: succeeded; initial bundle 500.06 kB, which is
  65 bytes over the 500.00 kB warning budget; Inventory lazy chunk 54.98 kB
  and Supplier Quotation lazy chunk 91.94 kB.
- Release Contracts/App/Infrastructure/test builds: 0 warnings/errors in the
  validated project builds. The normal API output was locked by the running
  API process; an equivalent API Release compilation with current project
  references succeeded with 0 errors.
- `git diff --check`: clean before documentation changes.

## Runtime verification

- Backend URL: `http://localhost:5300`.
- Frontend URL: `http://localhost:4300`.
- Backend PID: `43244` (`MiniErp.Api`, committed Release output).
- Frontend PID: `3728` (Node/Angular development server).
- Backend health: `GET /health` returned HTTP 200.
- Frontend status: `/` and `/main.js` returned HTTP 200.
- Both processes were restarted after the final source build and remain running
  for Owner inspection.

## Review and delivery controls

- Do not mark the Draft PR Ready.
- Do not merge, rebase, or force-push after validation.
- Do not create another PR or write Jira.
- Sol controls acceptance/review routing; no Opus prompt is placed in this
  handoff.
- Do not start MESP-131, Finance, Sales, Reporting, MESP-139, MESP-141, or
  downstream implementation from this task.

The next exact action is Sol acceptance of the exact final branch SHA and
bounded MESP-130 evidence. MESP-130 remains In Progress until that acceptance.

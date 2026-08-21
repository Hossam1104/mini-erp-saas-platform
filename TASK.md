# MESP-127 — Sol Acceptance Prompt: Supplier Return Corrections

Reviewer: GPT-5.6 Sol (bounded acceptance; read-only unless acceptance requires
an explicitly authorized correction)

Repository: `D:\AI Tools\Hossam\mini-erp-saas-platform`

Branch: `feat/MESP-127-supplier-return-corrections`

Exact main base SHA: `e5568c1ea186995dcc4f0cb0075b2f6b20a15064`

Implementation SHA: `f8f6dd1d850a00a94955d69c8ebb1c2b4c6697a5`

Final branch handoff SHA (implementation plus tracker baseline):
`ce39ce82121dd9484f06ce65ac3451b259854491`

Draft PR: Create or reuse exactly one Draft PR against `main`; keep it open,
Draft, and unmerged. Record its number here after creation. Do not merge.

Jira: MESP-127 remains **IN PROGRESS** under activation comment `11684`. No
Jira, Confluence, or other tracker writes are permitted in this acceptance
session.

## Acceptance boundary

Accept only the bounded MESP-127 Procurement-owned commercial Supplier Return
capability. Do not start MESP-128/MESP-129 Inventory, Finance/AP, payment,
external integration, statutory/ZATCA/FATOORA, DNS/TLS, supplier portal, or
Wafra-specific work. `frontend/assets` is Owner-managed source and must remain
untouched.

The delivered chain is:

`Accepted Goods Receipt -> Supplier Return commercial record -> authorized
decision -> Inventory-facing handoff evidence -> Finance-facing
credit/correction reference -> commercial history/reporting`.

## Delivered implementation to verify

- Public Procurement contracts cover Supplier Return status, reason,
  condition, commercial outcome, create/action/handoff/finance/correction,
  eligible-source, list/detail, history, audit, and report responses.
- Supplier Return persistence owns immutable Tenant-scoped source snapshots for
  PO/PO line, Supplier Confirmation where available, Goods Receipt/receipt
  line, Supplier, Warehouse, Product/UOM, accepted quantity, return quantity,
  reason/condition, commercial outcome, private evidence-reference metadata,
  downstream references, timestamps, actor/correlation, version, history, and
  replay/audit evidence.
- The additive formal migration is
  `20260821031935_MESP127SupplierReturnEvidence`; it must contain the actual
  Supplier Return tables, indexes, constraints, and relationships, not an empty
  placeholder. Procurement must not own Inventory ledger, on-hand, valuation,
  AP, GL, tax-posting, payment, or supplier-balance tables.
- Server-derived eligible quantity is recorded Goods Receipt accepted quantity
  less active non-reversed Supplier Return quantities for that receipt line.
  Rejected quantity is never eligible. MESP-125 remains truthful:
  `Received = Accepted + Rejected`, and damaged quantity is a non-additive
  condition overlay bounded by `Damaged <= Received`.
- Creation validates the recorded receipt, exact Tenant and Company/Branch
  scope, source PO/line, Supplier, Warehouse, accepted line, and remaining
  quantity. Source version touching/optimistic concurrency prevents overlapping
  requests from double-consuming the same remainder.
- Lifecycle behavior is truthful: Draft, Submitted, Approved,
  AwaitingInventory, InventoryHandoffRecorded/AwaitingFinance,
  FinanceReferenceRecorded/Completed, Rejected, Cancelled, Reversed, and
  CorrectionLinked only where the state permits. Downstream evidence never
  claims Stock Posted or Credit Posted without an authoritative downstream
  record.
- Corrections are forward-linked successor records. Original posted/source
  facts remain queryable and immutable; correction/reversal retains actor,
  timestamp, reason, affected source, prior/new linkage, scope, correlation,
  idempotency, authorization, history, and audit. Cancellation/reversal cannot
  silently restore quantity after downstream evidence exists.
- All mutations use the existing antiforgery, mandatory audit, ETag/If-Match,
  server authorization, durable idempotency replay/conflict, and
  Tenant/Company/Branch/Warehouse boundary patterns. Same key and fingerprint
  replays; same key with different fingerprint conflicts.
- Attachment support is intentionally reference-only because the existing
  platform seam does not authorize a new blob provider. Evidence references
  are Tenant-scoped, linked to the return, immutable metadata, and audited;
  no secret/private binary is placed in `frontend/assets`.
- REST routes are Foundation-catalogued and OpenAPI/Scalar discoverable for
  eligible sources, list/detail, create, lifecycle, Inventory handoff,
  Finance reference, correction, history, audit, and operational report.
- Reporting is Procurement operational evidence only: open returns, quantity,
  reason/status, source lineage, Supplier/Warehouse/Product lineage, pending
  handoffs/corrections, and correction/reversal state. It must not expose AP
  aging, supplier balances, GL/tax figures, stock balances, or valuation.
- Angular routes are lazy-loaded at `/app/procurement/supplier-returns`,
  `/new`, and `/:id`. Verify the accepted-only source selector, remaining
  quantity, human-readable PO/GR/SR lineage, evidence reference, actions,
  handoff/reference status, history/audit, visible safe errors, EN/AR copy,
  RTL/LTR direction, labelled keyboard controls, responsive layout, and
  reduced-motion behavior.

## Required acceptance checks

1. Verify branch, clean state after delivery, exact base ancestry, and that no
   source under `frontend/assets` changed.
2. Inspect the complete diff against
   `e5568c1ea186995dcc4f0cb0075b2f6b20a15064`, especially the persistence
   mappings, ownership-verifier registry, migration Up/Down, operation
   catalogue, authorization, idempotency ordering, and endpoint bindings.
3. Verify the migration contains non-empty create/drop operations and the
   snapshot contains all five Supplier Return entity types.
4. Verify accepted-only eligibility, rejected exclusion, partial return
   remainder, zero remainder, over-return conflict, cancellation/reversal
   restoration only before downstream evidence, and cross-Tenant/
   Company/Branch/Warehouse denial.
5. Verify lifecycle reason requirements, immutable original plus linked
   correction, downstream-consequence blocking, Inventory/Finance evidence-only
   references, history/audit snapshots, and no stock/AP/GL mutation.
6. Verify stale If-Match, racing overlapping returns, exact replay after state
   advancement/cache expiry, and idempotency fingerprint conflict.
7. Verify Foundation catalog and REST structural protection cover every unsafe
   endpoint with antiforgery, mandatory audit, idempotency, and concurrency;
   verify report permission filtering and server scope.
8. Verify the Angular workspace and deterministic Playwright flow do not expose
   raw developer IDs as the primary business labels or invent downstream facts.

## Required validation evidence

Run and report actual results, without hiding warnings or skips:

- `dotnet build backend/MiniErp.sln -c Release`: 0 warnings / 0 errors.
- Focused Supplier Return backend/architecture tests: 3/3 passed.
- `scripts/Test-MiniErpBackend.ps1`: 844/844 passed, 0 skipped, including all
  22 disposable LocalDB safety cases; verify the persistent runtime connection
  was unchanged and no `MiniErpFoundation_*` database remains.
- `npm test -- --watch=false --no-progress`: 239/239 across 31 spec files.
- `npm run build`: 494.71 kB initial, under the 500 kB budget, with a 57.40 kB
  Supplier Return lazy chunk.
- Focused Supplier Return Chromium Playwright: 2/2 passed.
- Full Chromium Playwright: 24/24 passed.
- `npm audit --omit=dev` and full `npm audit`: 0 vulnerabilities.
- `git diff --check`: clean.

## Sol decision and handoff rules

Return exactly one acceptance disposition: `ACCEPT FOR OWNER REVIEW`,
`REQUEST CHANGES`, or `BLOCK`, with P0/P1/P2/P3 findings, exact
file/line evidence, reproduction commands, regression evidence, and remaining
production/provider/legal/specialist/cutover gates. Acceptance is read-only;
do not merge, close MESP-127, write Jira, or start MESP-128. If a correction is
required, stop and describe the bounded authorization before editing.

The repository tracker and current state were updated for this implementation;
Hossam/ChatGPT should inspect the tracked GitHub versions directly. The
implementation branch and Draft PR remain unmerged pending this acceptance.

# MESP-126 — Independent Claude Opus 5 Delta Re-Review Prompt

Reviewer: Claude Opus 5 (independent, read-only)

Repository: `D:\AI Tools\Hossam\mini-erp-saas-platform`

Branch: `feat/MESP-126-three-way-matching-tolerances`

Base SHA: `42e51b673de5d076b56426180d914f7e3d07c54c`

Previous SOL review anchor: `178a49fca9dab6ba55f71871bf3bfcc0e709606a`

New code remediation SHA: `d2a107e427df335a0067c77c30d07562608ab743`

Final branch handoff SHA for this bounded implementation state:
`d2a107e427df335a0067c77c30d07562608ab743`

Draft PR: `#70` — must remain open, Draft, and unmerged.

Jira: MESP-126 remains **IN PROGRESS**. No Jira writes are permitted.

## Review rules

This is a bounded independent delta review of the P1 remediation and completed
cross-currency UX. It is read-only. Do not edit files, commit, push, merge,
close, retarget, or comment on PR #70. Do not write Jira, Confluence, or any
other external tracker. Do not start MESP-127 or Finance, AP, GL, Inventory,
payment, external integration, production, or FX-override work.

First verify the branch, clean/dirty state, base ancestry, previous review
anchor, new code remediation SHA, and final branch handoff SHA. Read
`AGENTS.md`, `CLAUDE.md`, `.ai/CURRENT_STATE.md`, this prompt, the attached
MESP-126 brief, and the relevant Procurement, currency, tax, authorization,
audit, REST, and ADR documents. Inspect the complete branch diff against the
base, then concentrate the re-review on the delta below while checking for
regressions in the already accepted MESP-126 capability.

## P1 remediation delta to verify

### 1. FX authority and request contract

Verify the public `PurchaseInvoiceExchangeRateReferenceRequest` exposes exactly
one property: `ExchangeRateId`. `EffectiveOn` and any other caller-supplied
historical date must not be accepted. Raw rate, scale, currency-pair, version,
effective-date, provenance, or source facts must not be matching inputs.

Verify the narrow MESP-120 provider selects the effective version only from the
immutable supplier invoice date captured in supplier-declared invoice evidence.
The only allowed fallback is the existing immutable handoff date field because
the current contract calls it `SupplierInvoiceDate`. A missing date fails
closed. The provider must continue to enforce Tenant ownership, active identity,
source/target currency pair, effective-dated version, positive rate and scale,
and server-owned snapshot metadata. Later master-data edits must not rewrite an
immutable match evaluation.

Required evidence includes provider/service tests for date authority and
missing-date failure, DTO-shape/reflection or equivalent contract coverage, and
snapshot assertions for version ID/number, pair, rate, scale, effective date
and window, provenance, and source notes.

### 2. Aggregate quantity and allocation semantics

Verify repeated supplier-declared lines for one `PurchaseOrderLineId` are
aggregated before quantity comparison so exactly one quantity variance is
recorded per PO line. The comparison remains against the current partial
Handoff/source quantity, not the original PO quantity. Under- and
over-declarations remain truthful evidence and use the configured absolute plus
percentage tolerance formula, including exact-boundary behavior.

Verify allocations are aggregated by `GoodsReceiptLineId`. The total declared
allocation must not exceed the handoff-represented quantity or active accepted
quantity for that receipt line. Duplicate allocations must be preserved as
supplier evidence and classified as an aggregate mismatch; they must not be
silently double-consumed or rejected at evidence intake merely because two
invoice lines refer to one physical receipt. Invalid foreign Tenant, wrong PO,
wrong line/receipt, negative, malformed, cancelled, rejected, and cross-scope
data must still fail closed. Valid allocations across multiple receipt lines
must remain valid without double-consumption.

Verify individual price, discount, tax code/rate/amount, net/gross/line amount,
and header subtotal/discount/tax/gross comparisons remain intact and are not
replaced by the aggregation logic.

### 3. Scope and policy boundary

Verify exact Tenant and Company/Branch scope remains server-derived and
explicit. Do not infer or invent Company-to-Branch policy inheritance. Verify
the existing tolerance, resolution, SoD, authorization, audit, concurrency,
idempotency, source snapshot, and legacy no-evidence `NotMatchReady` behavior
remain unchanged except for the bounded remediation above.

### 4. Cross-currency Angular UX

Verify same-currency matching renders no Exchange Rate selector and submits no
FX reference. For different currencies, verify the workspace loads the existing
MESP-120 Exchange Rate list and shows a human-readable selector only for active
identities whose source/target pair matches supplier invoice currency to PO
currency and whose version window covers the immutable supplier invoice date.

The browser must expose no raw GUID text and no editable rate, scale, version,
effective date, pair, provenance, or source fields. The request must send only
`ExchangeRateId`. No eligible identity or missing invoice date must remain
fail-closed and visibly Not match-ready. After evaluation, the UI must display
the applied server snapshot including pair, version, effective date, and
provenance/source metadata. Verify English/Arabic copy, RTL/LTR direction,
keyboard operation, label/description relationships, alert/status semantics,
responsive layout, and reduced-motion behavior. Verify focused and full
Chromium Playwright coverage, including request-body inspection.

## Required regression review

Recheck independent invoice evidence remains separate from the MESP-125
PO-derived handoff preview and preserves reference/date/currency/totals, line
quantity/unit price/discount/tax/amount/net/gross/description, Purchase Order
lineage, legitimate receipt allocations, immutable evidence versioning,
history/audit/replay, supersession/current evaluation, optimistic concurrency,
Tenant and Company/Branch scope, migrations, provider portability, REST/
OpenAPI/Foundation registration, antiforgery, `If-Match`, and idempotency.

This capability remains Procurement evidence orchestration only. It must not
post AP, GL, tax accounting, stock/on-hand, Inventory valuation, payment,
realized/unrealized FX, revaluation, statutory data, supplier portal data,
external invoice/FX integration, or Wafra-specific core behavior.

## Validation evidence to verify

- `dotnet build backend/MiniErp.sln -c Release`: 0 warnings, 0 errors.
- Focused Invoice Handoff/matching remediation tests: 37/37 passed.
- Full backend runner: 841/841 passed, 0 skipped, including 22 SQL safety
  tests against a disposable LocalDB `MiniErpFoundation_*` database; verify the
  persistent runtime connection was unchanged and zero disposable databases
  remained.
- Angular unit tests: 238/238 across 31 spec files.
- Angular production build: 494.00 kB initial and 38.05 kB matching lazy
  chunk, below the 500 kB initial budget.
- Focused matching Playwright: 3/3; full Chromium suite: 22/22.
- `npm audit --omit=dev` and full `npm audit`: 0 vulnerabilities.
- `git diff --check` and complete branch diff review.

## Required result

Return exactly one of `APPROVE FOR MERGE`, `REQUEST CHANGES`, or `BLOCK`, with
P0/P1/P2/P3 findings, exact file/line evidence, reproduction commands,
regression evidence, remaining production/provider/legal/specialist/cutover
gates, and explicit confirmation that the review was read-only. Preserve
branch `feat/MESP-126-three-way-matching-tolerances` and Draft PR #70 for
Owner/Sol decision. Do not merge MESP-126. Do not write Jira.

# MESP-126 — Independent Pre-Merge Review Prompt

Reviewer: Claude Opus 5 (independent, read-only)

Repository: `D:\AI Tools\Hossam\mini-erp-saas-platform`

Feature branch: `feat/MESP-126-three-way-matching-tolerances`

Required base SHA: `42e51b673de5d076b56426180d914f7e3d07c54c`

Final feature HEAD SHA: `TO_BE_FILLED_AFTER_IMPLEMENTATION_COMMIT`

Draft PR: the single Draft PR for this branch, if present

## Review rules

This is a read-only independent review. Do not edit files, commit, push, merge,
close, or retarget the Draft PR. Do not write Jira, Confluence, or any other
external tracker. GPT-5.6 Sol owns Jira. Do not start MESP-127 or any other
capability. Report findings with severity, file/line evidence, reproduction
commands, and a merge recommendation.

Start by verifying the branch, clean/dirty state, base ancestry, and final HEAD
against the values above. Read `AGENTS.md`, `CLAUDE.md`, `.ai/CURRENT_STATE.md`,
this prompt, the MESP-126 task brief, and the relevant Procurement, Inventory,
Finance, currency, tax, authorization, audit, REST, and ADR documents. Inspect
the complete feature diff against the required base; do not rely only on the
summary below.

## Capability under review

MESP-126 is Procurement evidence/orchestration only. It compares:

1. Purchase Order commercial commitment and lineage;
2. active accepted Goods Receipt physical evidence; and
3. independent supplier-declared invoice evidence attached to a Purchase
   Invoice Handoff.

The slice must not post AP, GL, tax accounting, payment, stock/on-hand,
Inventory valuation, statutory submissions, supplier portal data, or external
FX/invoice integrations.

## Review 1 — Independent Side-3 evidence

Verify that the MESP-125 PO-derived handoff preview fields retain their old
meaning and are never treated as supplier invoice truth. Confirm that a legacy
handoff without declared evidence is valid historical data but evaluates as
`NotMatchReady` / invoice evidence incomplete.

Verify the additive supplier-declared evidence contract and persistence:

- header supplier invoice reference/date, declared currency, subtotal,
  discount, tax, and gross totals;
- independent line quantity, unit price, discount, tax code/rate/amount, net,
  gross, description, and Purchase Order line lineage;
- explicit allocations to one or more eligible Goods Receipt lines;
- no requirement that one PO line equals one receipt or one invoice line;
- immutable prior versions, current pointer, optimistic concurrency, bounded
  correction reason, durable history/audit, and no silent overwrite;
- independent declared unit price/tax/currency/totals remain visible in API and
  Angular evidence views.

Check Tenant, Company, Branch, Purchase Order, Goods Receipt, and line lineage
validation. Cross-Tenant or wrong-scope IDs must not be accepted merely because
the caller supplied a GUID.

## Review 2 — Three-way evaluation and partials

Verify deterministic evaluation from genuine source evidence, not from the
PO-derived handoff preview. Check the result/lifecycle separation:

- `NotMatchReady` for missing or non-comparable required evidence;
- `ExactMatch` for exact evidence under the selected policy;
- `WithinTolerance` only for an applicable configured policy;
- `ExceptionHold` for out-of-tolerance or blocked evidence;
- `ResolvedException` only after a separately authorized decision;
- `Current` versus `Superseded` historical lineage.

Verify exact-safe defaults use zero tolerance. Both higher and lower supplier
prices are variances. No favorable-price shortcut is allowed. Check quantity,
price, amount, discount, tax code/rate/amount, header totals, currency, and
structured variance classifications.

Verify all of these load-bearing semantics:

- one PO line may have multiple receipts;
- partial receipts and replacement deliveries work;
- one receipt may support multiple partial invoices;
- one invoice may allocate across multiple receipt lines;
- rejected quantity never satisfies invoice eligibility;
- cancelled receipts and cancelled handoffs contribute zero active quantity;
- cumulative active handoff/invoice quantity cannot exceed active accepted and
  confirmed quantity;
- a PO 100 / active accepted receipt 100 / invoice 40 case can be exact while
  60 remains available.

Review the source snapshot and fingerprint for all relevant IDs, versions,
active receipt/accepted quantities, allocations, declared evidence, policy,
currency, calculated variances, actor, time, correlation, and supersession
lineage. Confirm historical evaluations remain reproducible.

## Review 3 — Tolerance, FX, and tax boundaries

Verify tolerance selection is Tenant-isolated, deterministic, effective/versioned
where applicable, configuration-led, and snapshotted with each evaluation. No
hardcoded Wafra/customer values or invented numerical defaults are allowed.
Check exact boundaries, within-boundary behavior, and just-outside behavior.

For same currency, verify nominal comparison from immutable source evidence. For
different currencies, verify that only a retained immutable applied-rate
snapshot with matching source/target currencies, rate, scale, provenance, and
version is accepted. Missing or mismatched rate evidence must fail closed with
an explicit currency variance. There must be no live FX fetch, realized or
unrealized FX, revaluation, or Finance entry.

Tax matching is source-evidence comparison only. Verify PO tax snapshots,
supplier-declared tax code/rate/amount, pro-rata line basis, and the existing
approved rounding rule. Tax variance must hold. Verify there is no recoverable
VAT, tax liability, GL, statutory-return, ZATCA, or FATOORA behavior.

Verify that both tolerance policy evidence and the configured resolution/SoD
policy identity/version are retained and visible where the response/history
supports them.

## Review 4 — Exception resolution, authorization, and Tenant isolation

Verify server-side authorization precedes resource disclosure and durable replay.
Cross-Tenant, unauthorized Company/Branch, wrong Warehouse lineage, inactive
source, or forged cross-module IDs must fail closed. Platform Administrator or
hostname knowledge must not grant Tenant ERP authority.

Exception resolution must require the exact matching resolve permission, current
Tenant/scope authority, non-empty bounded reason, antiforgery, mandatory audit,
idempotency, and optimistic `If-Match` concurrency. Resolution must not mutate
PO, GR, handoff, inventory, AP, or Finance source documents.

Verify Separation of Duties is policy/configuration-driven. Do not accept a
universal hardcoded “creator can never resolve” rule, but enforce a configured
different-actor rule when the applicable policy requires it. Verify the policy
snapshot and resolution reason survive reload and are shown in history/audit.

## Review 5 — Concurrency, staleness, and idempotency

Inspect transaction isolation, unique constraints, row/version handling, and
source re-read behavior. Verify at minimum:

- concurrent evaluations cannot create competing current evaluations;
- evaluation versus receipt cancellation cannot produce a successful invalid
  current decision;
- evidence/handoff changes invalidate the old source version;
- two resolvers cannot both transition the same hold;
- stale resolution refuses to proceed and requires a new evaluation;
- re-evaluation supersedes prior history without deleting it.

Verify the strong idempotency contract for evidence capture/correction,
evaluation, and resolution: same Tenant + actor + operation + target + key +
semantic request replays the original response; same key with changed payload or
target returns `409 idempotency_conflict`; no duplicate declaration, evaluation,
history, audit, or resolution is created. Retry after the state transition must
still replay the durable original response. Authorization must precede replay
disclosure.

## Review 6 — Persistence and migration

Verify Procurement module ownership, Tenant query filters, composite Tenant
foreign keys, unique indexes, concurrency/version columns, safe deletes, and
provider portability. Inspect the MESP-126 migrations and model snapshot,
including independent evidence, lines, allocations, match evaluations,
history/audit, fingerprints, source/policy snapshots, and resolution-policy
snapshot. Confirm no mutable Product/Supplier/UOM authority is duplicated.

Check SQL Server formal migration shape and SQLite test-provider behavior. Do
not claim SQL safety validation passed unless the disposable LocalDB gate is
actually supplied and exercised.

## Review 7 — REST, OpenAPI, and Angular UX

Verify the backend is the source of truth and every matching operation is
registered consistently in the Foundation REST catalogue, authorization
catalogue, OpenAPI/Scalar metadata, route handlers, frontend service, and E2E
mocks. Review list, detail, evaluate/re-evaluate, resolve, history, audit, and
declared-evidence capture/read use cases. Unsafe operations must retain
antiforgery, mandatory audit, idempotency, and `If-Match` behavior.

Review the Angular workspace for a real business workflow, not a debug panel:

- clear exact/tolerance/hold/resolved/stale states;
- PO, Goods Receipt, supplier invoice, supplier, product SKU/name, UOM,
  warehouse, quantities, amounts, currencies, policy, and variances shown with
  human-readable labels rather than a raw GUID workflow;
- partial allocations and evidence lineage understandable side by side;
- authorized reasoned exception resolution and safe conflict/stale errors;
- English and Arabic copy, RTL/LTR direction, keyboard/ARIA/focus behavior,
  responsive layout, and reduced-motion handling;
- SAR remains presentation-only through generic currency presentation; no
  `if tenant == Wafra`, Wafra-specific behavior, or asset mutation.

## Independent evidence already produced by the implementation session

Treat these as claims to verify against files and command output, not as a
substitute for review:

- Release backend build: 0 warnings, 0 errors.
- Focused Procurement handoff/matching tests: 13/13 passed.
- Full backend: 795 non-SQL tests passed; 22 SQL safety tests were
  connection-gated because `MESP_SQLSERVER_SAFETY_CONNECTION_STRING` was not
  available. No SQL assertion is claimed.
- EF migration listing included
  `20260820094805_ThreeWayMatchingAndDeclaredInvoiceEvidence` and
  `20260820102459_MESP126ResolutionPolicyEvidence` using a disposable-named
  LocalDB design-time connection.
- Angular unit tests: 235/235 across 30 spec files.
- Production Angular build: 494.00 kB initial; 29.75 kB matching lazy chunk;
  initial budget remains 500 kB.
- Chromium Playwright suite: 21/21 passed, including exact-result/Arabic RTL
  and ExceptionHold resolution browser scenarios. These are fixture-backed
  browser tests, not production/provider sign-off.
- `npm audit --omit=dev` and full `npm audit`: 0 vulnerabilities.
- API runtime smoke exercised only the repository API `/health` endpoint on
  port 5300 and returned `{ "status": "ok" }`. Do not infer live matching,
  SQL-provider, or production deployment validation from that smoke.

## Required review result

Return one of `APPROVE FOR MERGE`, `REQUEST CHANGES`, or `BLOCK`. Include exact
P0/P1/P2/P3 findings, affected files/lines, test evidence, and any remaining
production/provider/legal/cutover gates. Preserve the Draft PR and branch for
Owner/Sol decision. MESP-126 must not be merged by this review and no Jira
writes are permitted.

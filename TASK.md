# MESP-126 — Independent Claude Opus 5 Pre-Merge Review Prompt

Reviewer: Claude Opus 5 (independent, read-only)

Repository: `D:\AI Tools\Hossam\mini-erp-saas-platform`

Branch: `feat/MESP-126-three-way-matching-tolerances`

Base SHA: `42e51b673de5d076b56426180d914f7e3d07c54c`

Previous SOL review anchor: `178a49fca9dab6ba55f71871bf3bfcc0e709606a`

Exact new feature implementation SHA: `02e99f4ff2d962adc72efc46b5ebb8986df4d2f1`

Draft PR: `#70` — must remain open, Draft, and unmerged.

Jira: MESP-126 remains **IN PROGRESS**. No Jira writes are permitted.

## Review rules

This is a complete, independent, read-only review. Do not edit files, commit,
push, merge, close, retarget, or comment on PR #70. Do not write Jira,
Confluence, or any other external tracker. Do not start MESP-127 or Finance,
AP, GL, Inventory, payment, external integration, or production work. Report
P0/P1/P2/P3 findings with file/line evidence, reproduction commands, and one
of `APPROVE FOR MERGE`, `REQUEST CHANGES`, or `BLOCK`.

First verify branch, clean/dirty state, base ancestry, the previous review
anchor, and the exact implementation SHA. Read `AGENTS.md`, `CLAUDE.md`,
`.ai/CURRENT_STATE.md`, this prompt, the MESP-126 brief, relevant Procurement,
Inventory, Finance, currency, tax, authorization, audit, REST, and ADR
documents, and the complete branch diff against `main`.

## Required capability review

MESP-126 is Procurement evidence orchestration only. It compares the Purchase
Order commercial snapshot, active accepted Goods Receipt evidence, and
independent supplier-declared invoice evidence. It must not post AP, GL, tax
accounting, payment, stock/on-hand, Inventory valuation, statutory data,
supplier portal data, or external FX/invoice integrations.

Verify independent invoice evidence remains separate from the PO-derived
handoff preview and preserves header reference/date/currency/totals, line
quantity/unit price/discount/tax code/rate/amount/net/gross/description,
Purchase Order lineage, legitimate receipt allocations, immutable corrections,
history, audit, Tenant and Company/Branch scope, optimistic concurrency,
idempotency, and legacy handoff `NotMatchReady` behavior.

### Quantity tolerance and evidence truth

Verify quantity matching uses the current partial Handoff/source quantity,
never the entire Purchase Order:

`Variance = declared supplier quantity - current Handoff/source quantity`

`AllowedTolerance = absolute + abs(expected) * percentage / 100`

Zero configured policy means zero tolerance; both under- and over-declarations
remain explicit variances. Verify exact partial 100/100/40/40, zero-tolerance
39 and 41 holds, configured tolerance within and exactly on the boundary gives
`WithinTolerance`, and just outside gives `ExceptionHold`.

Verify supplier-declared over/under quantity remains recordable evidence rather
than being rejected as an intake error or being fabricated into receipt
allocation. Hard-invalid foreign Tenant, wrong PO, wrong line/receipt,
negative, malformed, and cross-scope data must still fail closed. Rejected
receipt quantity must not expand eligibility. Cancelled receipts and cancelled
handoffs must contribute zero current quantity. Cumulative active declared
invoice quantity must be explicitly blocked when it exceeds applicable active
accepted Goods Receipt or confirmed commercial quantity; tolerance must never
create physical stock or supplier entitlement.

### Runtime configuration and SoD

Verify normal application composition uses a live generic .NET configuration /
`IOptions` tolerance provider, with Tenant-isolated exact Company/Branch scope,
effective/versioned deterministic selection, no Wafra/customer values, and
exact-safe zero-tolerance fallback. The immutable selected policy must remain
in the evaluation snapshot. Verify configured non-zero tolerance is exercised
through the provider path.

Verify resolution fallback requires server-side permission, Tenant/scope
authorization, bounded non-empty reason, audit/history, idempotency, and
concurrency, but does not invent a universal different-actor rule. A
configured `RequireDifferentActor=true` policy must deny the same actor and
allow an authorized different actor; unauthorized actors remain denied
regardless of SoD configuration. Resolution must not mutate source documents.

### Server-authoritative MESP-120 FX

Verify the matching request accepts only a stable server-owned Exchange Rate
reference (`ExchangeRateId` and any existing effective-date input). Raw client
`Rate`, `Scale`, `Source`, `Version`, or currency-pair facts must not be
authoritative or accepted as matching inputs. Verify the narrow provider backed
by MESP-120 server persistence enforces:

- Tenant ownership;
- active Exchange Rate identity;
- effective-dated version existence;
- supplier declared currency equals source and PO currency equals target;
- positive rate/scale and server-owned version, provenance, and source notes.

Same-currency matching requires no FX reference. Different currency without a
valid reference is `CurrencyNotComparable` / `NotMatchReady`. Foreign Tenant,
wrong pair, inactive identity, and missing effective version all fail closed.
When valid, the exact `ExchangeRateId`, version ID/number, pair, rate, scale,
effective date/window, provenance, and source metadata are persisted in the
immutable match snapshot. Later MESP-120 edits or versions must not rewrite a
historical evaluation. There is no external FX feed, realized/unrealized FX,
revaluation, or Finance journal.

### Regression and boundaries

Recheck price, discount, tax code/rate/amount, line/header amount, currency,
source fingerprint, immutable evidence versioning, history/audit, replay,
supersession, current/non-stale evaluation, optimistic concurrency, durable
idempotency, Tenant isolation, Company/Branch scope, migration shape and
provider portability. Verify REST/Foundation catalogue, route handlers,
OpenAPI/Scalar metadata, antiforgery, mandatory audit, `If-Match`, and
idempotency are consistent.

Review Angular models/service/E2E for the corrected request and response. No
raw FX authority or raw-GUID workflow may be exposed. Human-readable evidence,
quantity, amount, policy, variance, and server-owned FX snapshot information
must remain understandable with English/Arabic copy, RTL/LTR behavior,
keyboard/ARIA/focus accessibility, responsive/reduced-motion behavior, and
protected `frontend/assets` untouched. No Wafra-specific core behavior.

## Validation evidence to verify

- Release backend build: 0 warnings, 0 errors.
- Focused handoff/matching remediation tests: 30/30 passed.
- Canonical backend runner: 834/834 passed, 0 skipped, including all 22 SQL
  safety tests against disposable LocalDB `MiniErpFoundation_*`; verify the
  persistent runtime connection was not used or changed.
- Angular unit tests: 235/235 across 30 spec files.
- Angular production build: 494.00 kB initial and 29.75 kB matching lazy
  chunk, below the 500 kB initial budget.
- Focused matching Playwright: 2/2; full Chromium suite: 21/21. These are
  fixture-backed browser checks, not production/provider sign-off.
- `npm audit --omit=dev`: 0 vulnerabilities; full `npm audit`: 0
  vulnerabilities.
- `git diff --check` and complete branch diff review.

## Required result

Return `APPROVE FOR MERGE`, `REQUEST CHANGES`, or `BLOCK`, with exact severity,
file/line evidence, test commands, remaining production/provider/legal/
specialist/cutover gates, and explicit confirmation that the review is
read-only. Preserve branch `feat/MESP-126-three-way-matching-tolerances` and
Draft PR #70 for Owner/Sol decision. Do not merge MESP-126. Do not write Jira.

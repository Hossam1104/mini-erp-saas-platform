# MESP-133 - AP / AR / Cash / Payment / Receipt / Settlement Architecture

## Bounded capability

MESP-133 adds the reusable Finance settlement spine for Accounts Payable,
Accounts Receivable, internal payment methods, cash and bank accounts,
payments, receipts, allocations, reversals, aging, exposure, and bounded
reconciliation. It is implemented as a Company-scoped Finance capability
inside the already authorized Tenant boundary. It does not create a separate
Tenant, workspace, or operational authorization hierarchy.

The capability starts from the exact merged-main baseline
`9ace42c7a830b5ef155a26b18d4a888676b8c188` and is delivered on
`feat/MESP-133-ap-ar-cash-settlement` as one focused Draft PR. It remains
unmerged pending GPT-5.6 Sol review and owner-controlled merge.

## Scope and non-scope

The implementation covers:

- AP open-item recognition from the existing MESP-126 Finance-ready supplier
  invoice handoff, with source evidence and supplier/company ownership;
- manual AR invoice/open-item creation as a bounded receivable source, without
  implementing the Sales lifecycle;
- Company-scoped internal payment methods and cash/bank accounts with
  lifecycle, direction, and uniqueness controls;
- payment and receipt lifecycle through submit, approve, reject, post, and
  explicit reversal;
- partial and multiple allocations, on-account/unapplied balances, allocation
  reversal, aging, customer exposure, and bounded reconciliation;
- source-to-subledger-to-GL lineage using the MESP-132 journal and Posting
  Rule contracts; and
- Angular EN/AR/RTL lazy workspaces for AP, AR, and settlements.

The following remain explicitly outside this capability: MESP-134 FX and
exchange-rate setup, tax/VAT/ZATCA/FATOORA, the Sales lifecycle, external bank
feeds or payment gateways, supplier/customer portals, statements, fixed
assets, payroll, treasury, generic Reporting, production provider setup,
migration/cutover, and Wafra-specific core behavior.

## Authorization and ownership

Every Finance settlement read and mutation resolves Tenant and Company scope
server-side from the authenticated context. Hostnames and client-supplied
Company identifiers are routing or request inputs only; they are not
authority. Resource routes re-resolve the Company and reject cross-Tenant or
cross-Company access. Mutations require the existing Finance permissions,
anti-forgery protection, idempotency where applicable, and optimistic
concurrency through `If-Match` on mutable lifecycle/configuration resources.

Payment methods, cash accounts, open items, settlement documents, and
allocations are module-owned Finance persistence. Tenant ownership verification
and global query filters are registered for all five new Finance entity types.

## AP and AR source contracts

AP recognition consumes the existing Procurement-to-Finance supplier invoice
handoff. Only the MESP-126 Finance-ready, exact/within-tolerance/resolved
source state is eligible. Held, unresolved, non-comparable, rejected, or
pending source evidence is not recognized as an AP open item. The adapter
preserves source document and match evidence rather than fabricating an
invoice.

Payment terms and the due-date basis are snapshots on the recognized open
item. No Net-30 default is invented. The current upstream handoff does not
provide a trusted term snapshot, so AP recognition fails closed with
`payment_terms_not_configured` until valid term evidence is present. This
preserves due-date reproducibility and prevents hidden aging drift.

AR manual creation is deliberately distinct from Sales. The request records
server-owned source identity and the same explicit term/due-date snapshot
rules. A missing term fails closed; no Sales invoice or customer credit
workflow is implied by this slice.

## Settlement model

Payment methods and cash/bank accounts are internal configurable resources.
Their direction, active lifecycle, and Company ownership are validated before
use. Account selection and posting are resolved through versioned Finance
Posting Rules; the API never accepts a browser-selected GL account as
authority.

Cash movement posts first as an unapplied/on-account settlement document.
Allocations are a separate, auditable subledger action that reduces the
remaining open-item balance. Multiple open items and partial amounts are
supported, with exact decimal validation and no over-allocation. Posted
documents and allocations are immutable. Corrections use explicit reversal
documents and reversed allocations; an allocation cannot be silently edited
after posting.

Realized FX is bounded and fails closed with
`fx_settlement_not_configured` when a non-functional settlement currency would
require MESP-134 configuration. No exchange rate, conversion, tax, or stored
amount is invented by MESP-133.

## Persistence and transactions

The additive Finance migration is
`20260824220208_MESP133ApArCashSettlement`. It adds the five module-owned
tables for payment methods, cash accounts, open items, settlement documents,
and allocations. Existing MESP-132 Finance migrations remain unchanged.

Lifecycle transitions, posting, reversal, allocation, and configuration
mutations use the Finance persistence boundary with serializable transaction
semantics where competing identity, balance, period, or allocation decisions
could race. Durable idempotency keys, version checks, source-to-GL uniqueness,
audit records, and explicit conflict classification protect retry and
concurrent-request behavior. SQL Server LocalDB tests exercise five provider-
realistic races for payment-method uniqueness/lifecycle, cash-account
uniqueness/lifecycle, and same-version payment-method editing.

## API and UI surface

The REST/OpenAPI surface is grouped under Finance and includes AP source-ready
and recognition routes, AR open-item routes, payment-method and cash-account
configuration routes, payment/receipt lifecycle routes, allocation and
allocation-reversal routes, and reconciliation/aging/exposure routes. Source
identity, settlement direction, posting authority, and evidence fields are
server-owned.

Angular adds lazy Finance routes:

- `/app/finance/ap`
- `/app/finance/ar`
- `/app/finance/settlements`

The shared standalone workspace exposes Company context, AP/AR open items,
settlement documents, aging/exposure summaries, and bilingual EN/AR labels
with RTL presentation. It is an operational Finance surface, not a new
Tenant/workspace chooser.

## Verification evidence

The bounded implementation was validated from the exact required main base:

- Release backend build: 0 warnings, 0 errors;
- disposable SQL Server LocalDB backend suite: 987/987 passed, 0 failed,
  0 skipped;
- SQL Server safety coverage: 51/51 passed, including five new MESP-133
  races;
- Angular unit tests: 261/261 passed across 38 spec files;
- Playwright Chromium: 36/36 passed, including two MESP-133 flows;
- production build: 0 warnings/errors, 496.43 kB initial bundle, 34.31 kB
  existing Finance/GL lazy chunk, and 23.95 kB settlement lazy chunk;
- both npm audits: 0 vulnerabilities;
- runtime health, root, `main.js`, `/app/finance`, `/app/finance/ap`,
  `/app/finance/ar`, and `/app/finance/settlements` probes returned HTTP 200;
  and
- `frontend/assets` was inspected and remains untouched.

The overall production-ready headline remains approximately 47% and
Procurement/P2P remains approximately 41%. A Draft PR is not merged
capability credit, so the accepted fast-track headline remains 16/26 = 61.5%
until the bounded review and merge decision is complete.

## Deferred handoff

GPT-5.6 Sol must review the complete focused diff, rerun or verify the
validation evidence, inspect the AP term fail-closed and MESP-126 source
boundary, inspect Posting Rule/GL lineage and reversal invariants, confirm the
SQL migration is additive, and decide whether the Draft PR can be merged.
MESP-133 remains In Progress / activated in Jira; no Jira writes were made by
this implementation session and no Opus review or Opus prompt was requested.

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
item. The adapter resolves the exact upstream Purchase Order payment-term
identity, code, and version through trusted Procurement persistence seams,
verifies the historical active term version, and derives the due date from the
invoice document date and that version. No Net-30 default is invented.
Missing, untrusted, cancelled, or unsupported term evidence fails closed with
`payment_term_not_configured`.

AR manual creation is deliberately distinct from Sales. The request records
server-owned source identity and the same explicit term/due-date snapshot
rules. A missing term fails closed; no Sales invoice or customer credit
workflow is implied by this slice.

## Settlement model

Payment methods and cash/bank accounts are internal configurable resources;
non-manual/provider-style methods fail closed. Their direction, active
lifecycle, and Company ownership are validated before use. Account selection
and posting are resolved through versioned Finance Posting Rules, and the
cash/bank side must equal the selected account's Company-owned
`LinkedAccountId`; the API never accepts a browser-selected GL account as
authority. MESP-132's `IFinanceSourceApprovalPolicy` supplies Required,
NotRequired, and NotConfigured settlement approval behavior, including
server-side SoD/self-approval enforcement.

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
concurrent-request behavior. SQL Server LocalDB tests retain the five earlier
MESP-133 configuration races and add nine financial races for AP recognition,
open-item/cash-document over-allocation, settlement post/lifecycle ordering,
submit-version ordering, posted-settlement reversal, allocation versus
reversal, same-payment post, same-receipt post, and allocation reversal.

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

The remediation was validated from the exact required main base and original
Sol-reviewed head. The focused source/test remediation commit is
`b9eba368922899165324086aa59298d054fec25d`:

- Release backend build: 0 warnings, 0 errors;
- disposable SQL Server LocalDB backend suite: 1002/1002 passed, 0 failed,
  0 skipped;
- SQL Server safety coverage: 60/60 passed, with all seven required financial
  races, the additional settlement-post/lifecycle and submit-version races,
  and the five retained MESP-133 configuration races;
- Angular unit tests: 261/261 passed across 38 spec files;
- focused Finance Playwright: 4/4; full Playwright Chromium: 36/36;
- production build: 0 warnings/errors, 496.43 kB initial bundle, 34.31 kB
  existing Finance/GL lazy chunk, and 23.95 kB settlement lazy chunk;
- both npm audits: 0 vulnerabilities;
- runtime health, frontend root, `main.js`, `/app/finance`,
  `/app/finance/ap`, `/app/finance/ar`, and `/app/finance/settlements` probes
  returned HTTP 200; a Development-authenticated request to
  `/api/v1/finance/companies` returned HTTP 200;
  and
- `frontend/assets` was inspected and remains untouched.

The overall production-ready headline remains approximately 47% and
Procurement/P2P remains approximately 41%. A Draft PR is not merged
capability credit, so the accepted fast-track headline remains 16/26 = 61.5%
until the bounded review and merge decision is complete.

## Deferred handoff

GPT-5.6 Sol must independently review the complete remediation diff, rerun or
verify the validation evidence, inspect the AP term/source boundary, approval
policy reuse, Posting Rule/GL lineage, reconciliation/as-of semantics,
route/document integrity, reversal invariants, and additive migration safety.
MESP-133 remains In Progress / activated in Jira; HOLD comment `11892` and
MESP-10 progress comment `11893` already exist. No Jira writes, merge, Ready
transition, MESP-134 activation, or Opus review/prompt were performed.

# MESP-134 — Tax Accounting, Multi-Currency, FX, Reporting Currency, and Revaluation

## Bounded capability

MESP-134 adds the next Finance capability on top of the merged MESP-132 GL
foundation and MESP-133 AP/AR settlement spine. It is Company-scoped inside
the already-authorized Tenant and does not create a parallel Tenant or
workspace authority.

The implementation owns durable monetary-policy, tax-accounting-effect, and
revaluation-batch evidence. It consumes the existing authorities for Company
functional currency, MESP-119 tax identities/rate versions, MESP-120 exact
exchange-rate versions, Finance posting rules, fiscal periods, journal facts,
AP/AR open items, settlements, and allocation lineage.

## Monetary policy and evidence

Each Company may have non-overlapping effective-dated policy versions. The
Company remains the authority for functional currency. Reporting currency is
optional and is resolved through an active Master Data currency reference.
Rounding scale and mode are persisted with the policy version and are applied
only at the Finance presentation/evidence boundary.

Every non-functional-currency calculation requires one exact active MESP-120
source/target pair and one effective version on the business date. The
evidence contains the pair, version identity, effective date, rate, scale,
provenance, source notes, and a stable reference value. Triangulation and
silent latest-rate selection are not permitted. Functional-currency
transactions carry no fabricated FX evidence.

Reporting values are derived from functional values through the configured
functional-to-reporting pair. A missing or ambiguous reporting rate blocks
the operation and is represented as an explicit evidence status; it does not
change the persisted transaction or functional amount.

## Tax accounting

Tax preview and posting call the existing `MasterDataTaxService` with the
server-owned open-item date, AP/AR direction, tax identity, taxable base,
transaction currency, and source lineage. The returned tax code, exact rate
version, effective date, rate percentage, and amount are persisted with the
Finance effect.

Posting creates a balanced Finance-owned reclassification journal using the
actual recognition journal account as the source-side control and the
effective `finance-tax.v1` input/output rule for the tax side. It validates
the source account and period before posting, records the rule version and
journal lineage, and supports an exact journal reversal with a required
reason. Supplier-declared tax and future statutory return/submission behavior
remain upstream or separately gated contracts; MESP-134 does not invent a
supplier declaration or ZATCA/FATOORA submission.

## Realized FX on allocation

MESP-133 allocation posting now records historical functional carrying value,
settlement functional value, realized difference, direction, exact
`finance-fx.v1` realized rule, and the realized-FX journal. The allocation
journal uses the actual historical AP/AR control account and the actual linked
cash/bank account from the posted settlement journal. It adds the configured
gain/loss account only when the rounded functional difference is non-zero.

The four sign cases are derived from the open-item kind and the difference
between settlement and historical functional value:

| Source | Settlement above historical | Settlement below historical |
|---|---|---|
| Payable | loss | gain |
| Receivable | gain | loss |

Functional-currency allocations have zero realized FX and no FX rule
evidence. Allocation reversal reverses the complete original journal,
including any gain/loss line, rather than recomputing with a current rule.

## Unrealized revaluation

The revaluation workflow is an explicit `Draft → Calculated → Posted →
Reversed` batch. Calculation snapshots recognized, still-outstanding AP/AR
items and posted, unallocated payment/receipt documents as of the requested
date. It derives historical functional carrying value from the original
source evidence and revalues the outstanding transaction amount with one exact
MESP-120 pair on the as-of date.

Posting requires `finance-fx.v1/unrealized`, an open fiscal period, active
posting accounts, and the original source journal lineage. Payable and
receivable signs determine gain/loss direction; every posted line retains the
rate and source evidence snapshot. A new revaluation for the same source is
blocked while an earlier posted batch is active. The earlier batch must be
reversed exactly before another active revaluation can be posted.

## API, security, and UX

The REST catalogue exposes Company-scoped policy, tax preview/post/reverse,
tax reconciliation, revaluation list/detail/create/calculate/post/reverse,
and realized/unrealized FX reconciliation operations. Unsafe mutations require
the existing Tenant context, Finance permission, antiforgery validation,
idempotency key, and audit evidence. Reversal and revaluation actions use
`If-Match` version concurrency. Cross-Company resources resolve as denied or
not found and never become a browser-selected Tenant/workspace filter.

The lazy Angular Finance workspace at `/app/finance/tax-fx` provides EN/AR and
RTL-safe policy, tax preview/posting, and revaluation controls. It displays
server-returned rate-version and journal/evidence state, exposes loading,
empty, unavailable, and blocked-by-evidence states, and keeps the existing
Finance/GL and settlement workspaces intact.

## Explicit boundaries

This bounded capability does not add external bank feeds, payment gateways,
automated provider FX, statutory VAT/ZATCA/FATOORA returns or submissions,
financial statements, generic Reporting, Sales lifecycle, payroll, fixed
assets, treasury, production DNS/TLS, migration/cutover, or Wafra-specific
core behavior. Production/provider, capacity, backup/restore, legal,
specialist, and external/statutory gates remain open.

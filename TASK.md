# Next session - MESP-33 Inventory and Warehouse Management BRD only

MESP-32 is **Done** as the approved Release 1 B2B Procurement and
Purchase-to-Pay business baseline. Its canonical artifact is
`docs/21_Procurement_and_Purchase_to_Pay_BRD.md`; focused PR #45 merged to
`main` at `6dec81f3520decdf7d50ef40a44186988ba516d5`. Jira activation,
validation, Owner approval, and closure evidence are comments `10736`,
`10738`, `10739`, and `10740`. MESP-33 remains **To Do** under MESP-8 and
must not be activated or executed until a fresh session verifies the live
baseline and begins this exact bounded task.

## Exact objective

Execute only **MESP-33 - Produce Inventory and Warehouse Management BRD**.
Produce the Release 1 B2B business-requirements baseline for Inventory and
Warehouse Management; obtain and record any genuinely blocking named-Owner
decisions through the normal process; publish the bounded documentation/Jira
handoff; then stop. Do not begin implementation, a Lean Implementation
Specification, MESP-34 Finance, or any later domain.

## Required evidence

Read `AGENTS.md`, `.ai/CURRENT_STATE.md`, this `TASK.md`,
`docs/staticts.md`, the canonical approved PRD `docs/MESP_PRD_v1.2.docx`, the
approved glossary, approved upstream BRDs and ADR/index evidence, the Product
Decision Register, MESP-23, `docs/21_Procurement_and_Purchase_to_Pay_BRD.md`,
and the Product Delivery Master Plan before changing scope.

Use PRD anchors `INV-001` through `INV-008`, `BR-006`, and `BR-007` as the
primary Inventory baseline. Trace procurement receipt/return handoffs,
Product/UOM/tracking boundaries, Organization/Warehouse scope, Finance
valuation/posting, Saudi/localization, reporting, migration, and production
gates explicitly.

Verify live Jira before activation, including MESP-25, MESP-26, MESP-32,
MESP-33, MESP-23, MESP-41, MESP-45, and every other open MESP-41--MESP-56
decision that can affect Inventory. Preserve approved answers at their exact
scope and keep recommended defaults visibly unapproved.

## BRD coverage

The BRD must define, without inventing unresolved policy:

- Opening Balance, Goods Receipt, Warehouse Transfer, Stock Adjustment,
  Inventory Count, Supplier Return, Customer Return, and Stock Issue flows;
- Inventory ownership, the immutable stock ledger, projected balances,
  availability, reservations only if approved, tracking attributes, and
  Release 1 Moving Weighted Average valuation;
- partial receipt/return/transfer/count/issue, cancellation, rejection,
  correction, reopening, negative-stock and other exception behavior only
  where supported by approved evidence;
- permissions, approval boundaries, separation of duties, delegation,
  concurrency, audit evidence, immutable historical references, failure and
  reconciliation handling;
- Product/UOM/tracking master reuse, Procurement receipt/return ownership,
  Finance valuation/accounting/AP impact, multi-currency/exchange boundaries,
  and integration contracts without redefining those domains;
- Saudi launch implications and external-validation gates without legal, tax,
  ZATCA, banking, or statutory conclusions;
- reports, KPIs, availability and ledger reconciliation, notifications,
  imports/exports, migration, observability, retention/privacy, supported
  volume, recovery, and operational-readiness requirements at the business
  level; and
- traceable Given/When/Then scenarios for happy paths, partials, exceptions,
  denial, Tenant isolation, immutable ledger/audit, concurrency, valuation,
  reconciliation, and downstream handoffs.

Release 1 remains B2B ERP only. Retail POS and Wafra-specific core behavior
are prohibited; Wafra may be used only as explicitly labelled validation
evidence. Suppliers, Customers, and other external business parties are not
Users and receive no login, credential, Tenant membership, or session semantics
from this BRD.

## Decision discipline

Do not infer an unresolved business rule from existing code, common inventory
practice, a recommended default, or model judgement. Consolidate decisions
that truly block a coherent Inventory BRD into a small Owner decision bundle
that states recommendation, alternatives, consequences, scope, and due point.
A decision is approved only with named human evidence in Jira or the immutable
decision record. Keep MESP-41 tracking, MESP-45 negative stock, MESP-48 volume,
MESP-49 Saudi production validation, MESP-50 retention/privacy, MESP-51
migration, MESP-53 reports/reconciliation, MESP-54 exchange rates, MESP-55
delegation, and every other affected open row open unless qualifying evidence
explicitly resolves it.

## Required boundary

- Documentation, Jira, and governance only.
- No application source, EF entity, table, migration, endpoint, API contract,
  UI, provider, database, infrastructure, or automated-test behavior change.
- Do not execute a migration or provision production/external infrastructure.
- Preserve the Inventory rules that the stock ledger is immutable, balances
  are projections that reconcile to movements, Moving Weighted Average is the
  Release 1 valuation baseline, and opening balances do not bypass the ledger.
- Do not resolve MESP-48, MESP-49, MESP-50, ADR-011, or another domain's
  policy by implication.
- Do not activate or execute MESP-34, another Jira issue, or an Inventory
  implementation slice automatically.

## Required completion and handoff

Run the checks relevant to documentation-only work, inspect the complete
task-related diff, update every genuinely affected state/plan file, review and
conservatively update `docs/staticts.md`, and record exact Jira evidence.
Commit and push through a focused review PR; merge only when clean and
unblocked. Leave the repository synchronized and `TASK.md` pointing to the
next exact separately authorized session, then stop for ChatGPT review. Never
execute that next task in this chat.

## Stop conditions

Stop and report a blocker for an unresolved Owner decision required to make the
BRD coherent; accounting/data-integrity, Tenant-isolation, authorization,
legal/privacy/external-validation, destructive migration/data-loss,
credential/production-infrastructure, or material scope/architecture risk.

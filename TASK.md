# Next session - MESP-35 B2B Sales and Order-to-Cash BRD only

MESP-34 is **Done** as the approved, documentation-only Release 1 B2B
Finance and Accounting business baseline. Its canonical artifact is
`docs/23_Finance_and_Accounting_BRD.md`. Focused PR #47 merged cleanly to
`main` at `a6f1960e9ae748c9809b6addbfd7e8d7ea510a1b` from final branch head
`72aa210d462f783671f1b3b33fcdea4955567b9c`; the approved requirements head
was `7d9de5d1556114d443b95db9547d6c083dcd804d`. Jira activation, validation,
Owner approval, final validation, and MESP-23 handoff evidence are comments
`10746`, `10747`, `10748`, `10749`, and `10750`. MESP-34 is Done in live Jira.

MESP-35 remains **To Do** under MESP-9 with the separately controlled BRD
sequence. It must be activated only in a fresh session after the live MESP-34
closure, Sales entry gate, and Finance posting-foundation handoff are
reverified. Currency and all later tasks remain unstarted.

## Exact objective

Execute only MESP-35 — Produce the Release 1 B2B Sales and Order-to-Cash
business-requirements baseline. Cover the B2B quotation, sales order,
reservation, delivery, sales invoice, customer receipt, customer return,
credit-control, AR, Inventory, Finance, reporting, Saudi, migration, and
integration handoffs. Obtain and record genuinely blocking named-Owner or
qualified external decisions through the normal process; publish the bounded
documentation/Jira handoff; then stop.

Do not begin implementation, a Lean Implementation Specification, Currency,
Reporting, Saudi, or any later domain. Do not execute any next task
automatically.

## Required evidence

Read `AGENTS.md`, `.ai/CURRENT_STATE.md`, this `TASK.md`, `docs/staticts.md`,
the canonical approved PRD `docs/MESP_PRD_v1.2.docx`, the approved glossary,
approved Procurement BRD `docs/21_Procurement_and_Purchase_to_Pay_BRD.md`,
approved Inventory BRD `docs/22_Inventory_and_Warehouse_Management_BRD.md`,
approved Finance BRD `docs/23_Finance_and_Accounting_BRD.md`, the Product
Decision Register, MESP-23, ADR/index evidence, and the Product Delivery Master
Plan before changing scope.

Use PRD anchors SAL-001 through SAL-008, BR-005, and BR-009 as the primary
Sales baseline. Reverify the corrected sequence: Finance is analysed before
Sales. Preserve the Finance posting foundation and do not redefine AP, AR, GL,
tax, cash/bank, period, currency, reversal, reconciliation, or consolidation
policy.

Verify live Jira before activation, including MESP-25, MESP-26, MESP-34,
MESP-35, MESP-23, MESP-41 through MESP-56, and every Sales-affecting issue.
Preserve exact approved MESP-52/PD-020 and MESP-56/PD-021 scopes. Keep every
other open row open unless qualifying named evidence resolves it.

## BRD coverage

The Sales BRD must define, without inventing unresolved policy:

- B2B quotation, sales order, approval, reservation, delivery, partial
  delivery, sales invoice, customer receipt, allocation, customer return,
  credit note, and reconciliation workflows;
- triggers, preconditions, actors, permissions, approval boundaries, credit
  control, separation of duties, delegation, lifecycle/status transitions,
  validation, business numbering, cancellation, immutable posted history, and
  reversal/correction;
- source handoffs to the Finance GL/AR/revenue/tax/cash foundation and the
  Inventory immutable stock ledger/valuation boundary;
- Product/Item, Category/UOM, customer, Organization, Procurement,
  Inventory, Finance, Reporting, Migration, Saudi/localization, and external
  integration boundaries without redefining those domains;
- partials, exceptions, denial, credit holds, stale/concurrent edits,
  idempotent retries, unknown payment outcomes, audit, notifications,
  reconciliation, reports/KPIs, imports/exports, observability, and recovery;
- Saudi/localization implications and external validation without legal, tax,
  ZATCA, banking, privacy, or statutory conclusions; and
- traceable Given/When/Then scenarios for happy paths, partial delivery and
  settlement, denial, Tenant isolation, immutable financial history,
  Inventory/Finance handoffs, valuation, reconciliation, and downstream
  reporting.

Release 1 remains B2B ERP only. Retail POS, cashier sessions, retail checkout,
store cash drawers, loyalty, promotions, and Wafra-specific core behavior are
prohibited. Customers and suppliers are external business parties, not Users;
this BRD must not grant them credentials, Tenant membership, or sessions.

## Decision discipline

Do not infer unresolved Sales, credit, pricing, tax, currency, approval,
reservation, delivery, return, payment, Saudi, migration, reporting, or
integration rules from existing code, common practice, recommended defaults, or
model judgment. Consolidate decisions that truly block a coherent Sales BRD
into a small Owner/external bundle stating recommendation, alternatives,
consequences, scope, and due point. A decision is approved only with named
human or qualified external evidence in Jira or the immutable decision record.

Do not close Inventory or Finance decisions merely because Sales depends on
them. Preserve the Finance BRD's open FIN-OD rows and the exact PD-020/PD-021
boundaries.

## Required boundary

- Documentation, Jira, and governance only.
- No application source, EF entity, table, migration, endpoint, API contract,
  UI, provider, database, infrastructure, or automated-test behavior change.
- Do not execute a migration or provision production/external infrastructure.
- Do not resolve MESP-48, MESP-49, MESP-50, ADR-011, Finance policy,
  Inventory policy, or another domain's decision by implication.
- Do not activate or execute Currency, MESP-36, MESP-37, or any later task
  automatically.

## Required completion and handoff

Run focused documentation checks, inspect the complete task-related diff,
update every genuinely affected state/plan file, review and conservatively
update `docs/staticts.md`, and record exact Jira activation, validation, Owner
approval, MESP-23 handoff, and closure evidence. Commit and push through a
focused review PR; merge only when clean and unblocked. Leave the repository
synchronized and `TASK.md` pointing to the next exact separately authorized
session, then stop for ChatGPT review.

## Stop conditions

Stop and report a blocker for an unresolved Owner or qualified external
decision required to make the Sales BRD coherent; accounting/data-integrity,
Tenant-isolation, authorization, legal/privacy/external-validation,
destructive migration/data-loss, credential/production-infrastructure, or
material scope/architecture risk.

This handoff is the end of the MESP-34 session. Do not execute MESP-35,
Currency, or any next task after updating this file in the current session.

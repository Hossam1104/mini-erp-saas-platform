# Next session - MESP-34 Finance and Accounting BRD only

MESP-33 is **Done** as the approved, documentation-only Release 1 B2B
Inventory and Warehouse Management business baseline. Its canonical artifact
is docs/22_Inventory_and_Warehouse_Management_BRD.md. Focused PR #46 merged
cleanly to main at cd6f57de329b7d193c5d75e2e4268ae87c8aac67 from final branch
head 94f3b7e. Jira activation, validation, Owner approval, and closure
evidence are comments 10741, 10742, 10743, and 10745. The MESP-23
Inventory-linked register handoff is comment 10744.

MESP-34 remains **To Do** under MESP-10 with labels brd-deliverable,
brd-seq-09, and phase-brd. It must be activated only in a fresh session after
the live MESP-33 closure and the Finance BRD entry gate are reverified.

## Exact objective

Execute only MESP-34 - Produce Finance and Accounting BRD. Produce the
Release 1 B2B Finance and Accounting business-requirements baseline, including
the posting foundation on which Procurement, Inventory, and B2B Sales depend;
obtain and record any genuinely blocking named-Owner or qualified external
decisions through the normal process; publish the bounded documentation/Jira
handoff; then stop. Do not begin implementation, a Lean Implementation
Specification, MESP-35 B2B Sales, or any later domain.

## Required evidence

Read AGENTS.md, .ai/CURRENT_STATE.md, this TASK.md, docs/staticts.md, the
canonical approved PRD docs/MESP_PRD_v1.2.docx, the approved glossary,
approved upstream BRDs including Inventory docs/22_Inventory_and_Warehouse_Management_BRD.md
and Procurement docs/21_Procurement_and_Purchase_to_Pay_BRD.md, the Product
Decision Register, MESP-23, ADR/index evidence, and the Product Delivery Master
Plan before changing scope.

Use PRD anchors FIN-001 through FIN-011, BR-008, and BR-009 as the primary
Finance baseline. Trace Accounts Payable, Accounts Receivable, General Ledger,
journals, posting rules, tax, cash/bank, accounting periods, reconciliation,
multi-currency, financial statements, Inventory valuation, Procurement
invoices/payments, B2B Sales invoices/receipts, Saudi/localization,
reporting, migration, and production gates explicitly.

Verify live Jira before activation, including MESP-25, MESP-26, MESP-33,
MESP-34, MESP-23, MESP-41 through MESP-56, and any Finance-affecting issue.
Preserve approved answers at their exact scope and keep recommended defaults
visibly unapproved. Do not close an Inventory decision merely because Finance
depends on it.

## BRD coverage

The Finance BRD must define, without inventing unresolved policy:

- AP, AR, GL, journals, posting rules, tax, cash/bank, accounting periods,
  reconciliation, multi-currency, financial statements, and the posting
  foundation required by Procurement, Inventory, and B2B Sales;
- source-document and subledger-to-GL lineage, immutable posted history,
  reversal/correction, period controls, valuation handoffs, and no silent
  edit/delete behavior;
- actors, permissions, approval boundaries, separation of duties, delegation,
  concurrency, idempotency, audit, failure, unknown outcomes, and
  reconciliation;
- Product/UOM, Organization, Procurement, Inventory, B2B Sales, Reporting,
  Migration, Saudi/localization, and external integration boundaries without
  redefining those domains;
- Saudi launch implications and external validation without legal, tax,
  ZATCA, banking, or statutory conclusions;
- reports, KPIs, notifications, imports/exports, migration, observability,
  retention/privacy, supported volume, recovery, and operational-readiness
  requirements at the business level; and
- traceable Given/When/Then scenarios for happy paths, partials, exceptions,
  denial, Tenant isolation, immutable financial history, concurrency,
  valuation, reconciliation, and downstream handoffs.

Release 1 remains B2B ERP only. Retail POS and Wafra-specific core behavior
are prohibited; Wafra may be used only as explicitly labelled validation
evidence. Suppliers, Customers, and other external business parties are not
Users and receive no login, credential, Tenant membership, or session semantics
from this BRD.

## Decision discipline

Do not infer unresolved accounting, tax, currency, approval, period, banking,
consolidation, Saudi, migration, or reporting rules from existing code, common
practice, a recommended default, or model judgement. Consolidate decisions
that truly block a coherent Finance BRD into a small Owner/external decision
bundle stating recommendation, alternatives, consequences, scope, and due
point. A decision is approved only with named human or qualified external
evidence in Jira or the immutable decision record.

Keep MESP-41 through MESP-55 and every other affected open row open unless
qualifying evidence explicitly resolves it. Preserve the approved exact
scope of MESP-52 / PD-020 and MESP-56 / PD-021. Preserve the Inventory
baseline's immutable ledger, projected-balance, Moving Weighted Average,
tracking, negative-stock, migration, reporting, exchange-rate, delegation,
Saudi, and production gates.

## Required boundary

- Documentation, Jira, and governance only.
- No application source, EF entity, table, migration, endpoint, API contract,
  UI, provider, database, infrastructure, or automated-test behavior change.
- Do not execute a migration or provision production/external infrastructure.
- Do not resolve MESP-48, MESP-49, MESP-50, ADR-011, an Inventory policy, or
  another domain's decision by implication.
- Do not activate or execute MESP-35, another Jira issue, or a Finance
  implementation slice automatically.

## Required completion and handoff

Run the checks relevant to documentation-only work, inspect the complete
task-related diff, update every genuinely affected state/plan file, review and
conservatively update docs/staticts.md, and record exact Jira evidence.
Commit and push through a focused review PR; merge only when clean and
unblocked. Leave the repository synchronized and TASK.md pointing to the next
exact separately authorized session, then stop for ChatGPT review. Never
execute that next task in the same chat.

## Stop conditions

Stop and report a blocker for an unresolved Owner or qualified external
decision required to make the BRD coherent; accounting/data-integrity,
Tenant-isolation, authorization, legal/privacy/external-validation,
destructive migration/data-loss, credential/production-infrastructure, or
material scope/architecture risk.

This handoff is the end of the MESP-33 session. Do not execute MESP-34 or any
next task after updating this file in the current session.

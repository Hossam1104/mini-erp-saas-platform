# Next session - MESP-32 Procurement and Purchase-to-Pay BRD only

MESP-108 has accepted and reconciled the non-blocking Independent Opus 5
checkpoint findings in
`docs/98_Independent_Opus_5_Checkpoint_Reconciliation.md`. MESP-25 and MESP-26
are Done. MESP-32 remains **To Do** under MESP-7 and must not be activated or
executed until a fresh session verifies the live baseline and begins this exact
bounded task.

## Exact objective

Execute only **MESP-32 - Create Procurement and Purchase-to-Pay BRD**. Produce
the Release-1 business-requirements baseline for Procurement/Purchase-to-Pay;
obtain and record any genuinely blocking named-Owner decisions through the
normal decision process; publish the bounded documentation/Jira handoff; then
stop. Do not begin implementation, a Lean Implementation Specification, or the
next domain.

## Required evidence

Read `AGENTS.md`, `.ai/CURRENT_STATE.md`, this `TASK.md`, `docs/staticts.md`,
the canonical approved PRD `docs/MESP_PRD_v1.2.docx`, the approved glossary,
the approved upstream BRDs and ADR/index evidence, the Product Decision
Register, MESP-23, and the Product Delivery Master Plan before changing scope.
Use PRD anchors PROC-001 through PROC-008 and BR-005 as the primary Procurement
baseline and trace supporting cross-domain requirements explicitly.

Verify live Jira before activation, including MESP-25, MESP-26, MESP-32,
MESP-23, MESP-42, MESP-43, MESP-44, and every other open MESP-41--MESP-56
decision that can affect Procurement. Preserve approved answers at their exact
scope and keep recommended defaults visibly unapproved.

## BRD coverage

The BRD must define, without inventing unresolved policy:

- Purchase Request, Purchase Order, Supplier Confirmation, Goods Receipt,
  Purchase Invoice, Supplier Payment, and supplier-return business flows;
- partial ordering, confirmation, receipt, invoicing, payment, cancellation,
  rejection, return, reopening, and exception behavior where Release 1 needs
  it;
- matching and exception ownership across order, receipt, invoice, and payment,
  including tolerances only when supported by approved evidence;
- permissions, approval boundaries, separation of duties, delegation,
  concurrency, audit evidence, immutable historical references, and failure
  handling;
- Supplier/master-data reuse, Inventory receipt/return ownership, Finance AP,
  posting/payment ownership, tax, multi-currency/exchange-rate boundaries, and
  integration contracts without redefining those domains;
- Saudi launch implications and external-validation gates without making legal,
  tax, ZATCA, banking, or statutory conclusions;
- reporting, notifications, imports/exports, migration, observability,
  retention/privacy, supported volume, recovery, and operational-readiness
  requirements at the business level; and
- traceable Given/When/Then acceptance scenarios for happy paths, partials,
  exceptions, denial, Tenant isolation, audit, concurrency, and downstream
  handoffs.

Suppliers are external business parties. They are never application Users and
receive no login, credential, Tenant membership, or user-session semantics from
this BRD. Release 1 remains B2B ERP only; Retail POS and Wafra-specific core
behavior are prohibited. Wafra may be used only as explicitly labelled
validation evidence.

## Decision discipline

Do not infer an unresolved business rule from existing code, common ERP
practice, a recommended default, or model judgement. Consolidate any decisions
that truly block a coherent Procurement BRD into a small Owner decision bundle
that states recommendation, alternatives, consequences, scope, and due point.
A decision is approved only with named human evidence in Jira or the immutable
decision record. Keep MESP-42, MESP-43, MESP-44 and other Procurement-affecting
open decisions open unless qualifying evidence explicitly resolves them.

## Required boundary

- Documentation, Jira, and governance only.
- No application source, EF entity, table, migration, endpoint, API contract,
  UI, provider, database, infrastructure, or automated-test behavior change.
- Do not execute a migration or provision production/external infrastructure.
- Do not resolve MESP-48, MESP-49, MESP-50, ADR-011, or another domain's
  policy by implication.
- Do not activate or execute MESP-33, MESP-34, another Jira issue, or a
  Procurement implementation slice automatically.

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

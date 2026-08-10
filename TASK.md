# Next session - MESP-23 / Create Open Questions Register only

MESP-106 / Master Data authorization and duplicate-audit classification
hardening is **Done**. Its bounded Product/Supplier correction merged through
PR #42 at `0f712edcf58119057d614000721fe41227383bc1` from reviewed head
`678a5598877f55f1b32b012de692ebdf28408acd`. Jira activation and validation
evidence are comments `10728` and `10729`; the closure evidence is recorded on
MESP-106 after the final state reconciliation.

MESP-107 / M95-SL-05 Business Customer master-data implementation is **Done**
through PR #41 at `fb632982d06fd4f6bf965fb15dff7701a0bddcec`; its activation,
validation, and closure evidence are comments `10692`, `10726`, and `10727`.
MESP-106 changed only Product/Supplier authorization dependency and denial
classification, Supplier deterministic duplicate classification, failure
audit-evidence preservation, and focused regression coverage. Customer source
behavior was not changed. No new fields, tables, migrations, routes, UI,
provider, production, downstream, or cross-Tenant scope behavior was added.

The repository is synchronized on `main` at the MESP-106 merge baseline. Live
Jira counts must be re-checked at session start. At handoff, MESP-23 is the
existing non-Epic **In Progress** governance/open-questions register; it is
not a new implementation activation. MESP-48, MESP-49, and MESP-50 remain
open production and external-decision gates. Do not activate or execute any
other Jira item automatically.

## Exact objective

Execute only the bounded continuation of **MESP-23 - Create Open Questions
Register** as a governance artifact. Reconcile the living register with the
approved PRD v1.2 clarification backlog, the Product Decision Register, the
glossary, the current Decisions register, and live Jira. Preserve every
unresolved question as unresolved unless a named Owner has explicitly recorded
an approved answer or deferral with evidence.

The session must:

- verify the register structure and the sixteen PRD clarification questions;
- reconcile timing classifications, affected BRD tasks, owners, evidence,
  status, and links to MESP-41 through MESP-56;
- preserve the recorded closure/evidence for MESP-52 and MESP-56 without
  treating recommendations or historical defaults as new approvals;
- keep MESP-48, MESP-49, MESP-50 and all other still-open decisions visibly
  open, including their external legal/privacy/production gates; and
- produce only the smallest documentation/Jira update needed to keep the
  living register accurate and traceable.

Do not infer an answer from code, Wafra evidence, general knowledge, or an
assistant recommendation. If the current register is already accurate, record
the evidence and add no duplicate artifact.

## Entry gates

- Read `AGENTS.md`, `.ai/CURRENT_STATE.md`, this `TASK.md`, and the current
  `docs/staticts.md` before changing scope.
- Read the approved `docs/MESP_PRD_v1.2.docx`, `docs/90_MVP_Founder_Decision_Pack.md`,
  `docs/91_Jira_Simplification_Update.md`, `docs/Decisions.md`,
  `docs/00_ERP_Business_Glossary.md`, the relevant BRD/plan traceability
  sections, and the current Product Decision Register evidence.
- Verify live Jira status and comments for MESP-23, MESP-22, MESP-19,
  MESP-18, and MESP-41 through MESP-56. Confirm MESP-106 and MESP-107 remain
  Done and MESP-48/MESP-49/MESP-50 remain open.
- Confirm this is documentation/Jira governance work only. The backend
  topology remains `MiniErp.Api -> MiniErp.Infrastructure -> MiniErp.App ->
  MiniErp.Contracts`; no source, persistence, provider, or production change
  is authorized by this handoff.

## Required boundary

- Keep the register decision-neutral: open is a valid state, and an approved
  answer requires named human Owner evidence on the applicable decision record.
- Keep MESP-23 as the living governance register; do not create a second
  shadow register or duplicate BRD/LIS artifact.
- Preserve the distinction between business decisions, external legal/privacy
  validation, architecture gates, production gates, and implementation work.
- Preserve Release 1 B2B ERP scope and the explicit Retail POS/Wafra-core
  exclusions.

## Hard exclusions

- No application source, EF entity, table, migration, endpoint, API contract,
  UI, provider, database, production provisioning, or test behavior changes.
- No Product, Supplier, Customer, Inventory, Procurement, Sales, AR/AP,
  Finance, Tax, payment, banking, settlement, Price List, Payment Terms,
  Currency, Exchange Rate, statutory, Saudi legal, privacy, retention, purge,
  residency, or downstream implementation work.
- No decision may be marked approved from a recommended default, Wafra-only
  observation, assistant analysis, or unvalidated external assumption.
- Do not activate or execute another Jira item automatically after MESP-23.

## Required validation and handoff

- Review the complete documentation/Jira diff for silent decisions, dropped
  questions, incorrect task mappings, missing owners/evidence, duplicated
  artifacts, scope expansion, or stale current-state claims.
- Run documentation link/reference checks and the repository checks relevant
  to Markdown-only changes. Do not claim source, SQL Server, or production
  validation from a governance update.
- Update genuinely affected Markdown state/plan files, `.ai/CURRENT_STATE.md`,
  `docs/staticts.md`, and this `TASK.md`. Update MESP-23 with bounded evidence
  only; do not close it without the required Owner approval and register
  acceptance evidence.
- Commit and push the bounded documentation/Jira work. Use a focused review
  PR when repository content changes, inspect review threads, address valid
  findings, synchronize local `main`, and record the final handoff.
- Stop after this single MESP-23 session for ChatGPT review. Do not execute the
  next root `TASK.md` automatically in the same chat.

## Stop conditions

Stop and report a blocker on an invented or disputed business answer,
unresolved Owner or legal/external validation, Tenant/security or accounting
integrity risk, destructive data change, credential/production-infrastructure
requirement, or material scope/architecture change.

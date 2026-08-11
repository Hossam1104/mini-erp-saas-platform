# Next session — qualified Saudi external-validation and owner-decision handoff

MESP-111 — Prepare Saudi regulatory evidence and external-validation readiness
is complete at its bounded documentation/research/governance scope. The
canonical artifact is
docs/26_Saudi_Regulatory_Evidence_and_External_Validation_Readiness.md.

The recorded verdict is:

> READY FOR MESP-37 DRAFT ONLY — EXTERNAL VALIDATION OUTSTANDING

The artifact contains the dated official ZATCA and SDAIA/NDMO source register,
KSA-001–KSA-008 / BR-002 traceability, evidence classifications,
ZATCA/FATOORA and VAT boundaries, PDPL/privacy/residency boundaries,
MESP-49/MESP-50 gap records, and qualified-adviser/owner question packs. It
contains no software source, database, migration, API, UI, infrastructure,
credentials, FATOORA implementation, legal advice, tax advice, certification,
or production-readiness claim.

## Exact next objective

In one fresh bounded session, obtain, record, and reconcile qualified Saudi
external validation and owner decisions needed to determine whether MESP-37
can later be activated. This is a readiness handoff, not MESP-37 activation.

Do not activate or execute MESP-37 automatically. MESP-37 remains **To Do**.

## Required entry evidence

Before any MESP-37 activation decision, read and reverify:

- AGENTS.md, .ai/CURRENT_STATE.md, this TASK.md, docs/staticts.md, and the
  canonical readiness artifact;
- the approved PRD, glossary, approved Procurement/Inventory/Finance/Sales/
  Reporting BRDs, Decisions.md, applicable ADRs, and the Product Delivery
  Master Plan;
- live Jira MESP-111 closure evidence and the current statuses of MESP-37,
  MESP-49, MESP-50, MESP-23, MESP-53, MESP-54, and MESP-110; and
- current official ZATCA and SDAIA/NDMO sources, recording any changed or
  superseded source instead of rewriting the approved PRD silently.

## Required external evidence

Obtain dated, source-cited written answers from:

1. A qualified Saudi tax/compliance adviser for TAX-01 through TAX-10,
   including Release 1 invoice/note scope, Phase 1/Phase 2 and taxpayer-wave
   applicability, clearance/reporting, fields/identifiers/Arabic/timestamps/
   QR/XML/security, correction/failure handling, archive/retention, VAT-safe
   BRD wording, country-pack versus taxpayer configuration, certification
   evidence, and permitted product claims.
2. A qualified Saudi privacy/legal adviser for PRIV-01 through PRIV-12,
   including controller/processor roles, legal bases/notices, rights, breach
   duties/times, DPO, controller registration, data flows, cross-border
   hosting/backups/support/subprocessors/observability/integrations, TIA,
   SCC/BCR/safeguards, sensitive data, retention/destruction/legal hold, and
   legal versus commercial residency.
3. Product Owner, Finance Controller, and relevant Platform/Privacy owners for
   OWN-01 through OWN-05. Owner approval must not substitute for missing
   qualified-adviser validation.

Preserve all unresolved items in MESP-23. MESP-49 and MESP-50 remain open until
their exact evidence gates are genuinely satisfied. MESP-53, MESP-54, and
MESP-110 remain open and are not implied to be resolved by this handoff.

## Allowed scope

Documentation, official-source evidence, Jira, traceability, and governance
only. No source, test, EF entity, table, migration, endpoint, API contract,
UI, provider, database, infrastructure, credential, FATOORA integration,
production configuration, Retail POS behavior, or Wafra-specific core
behavior.

Only if the required external and owner evidence is complete may a later
session decide whether to activate MESP-37. If evidence is still incomplete,
record the remaining gaps and keep MESP-37 To Do. Do not create or execute a
different next task automatically.

## Completion requirements

Validate the full task-related diff, update every genuinely affected Markdown
state/plan file, conservatively update docs/staticts.md, synchronize Jira
comments/statuses without closing MESP-37/MESP-49/MESP-50 by inference, publish
only the bounded evidence change through a focused PR, merge only when clean
and unblocked, update this TASK.md with the next exact separately authorized
handoff, and stop for ChatGPT review.

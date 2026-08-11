# Next session - MESP-38 - Security, Audit, and Data Governance BRD only

## Session boundary

This is the exact next executable session after the completed MESP-37 Saudi
Localization/Core ERP BRD. Execute only the MESP-38 documentation-only BRD
boundary below. Do not start another task automatically.

MESP-37 is Done. Its approved bounded product-only baseline is published at
`docs/28_Release_1_Saudi_Localization_BRD.md`; PR #55 was reviewed at final
content head `ff8eb5901d68a2cc366ed61722c08a7be53f50a1` and merged to `main` at
`7d03fa5b19226b8c6368012ec90c8a09eefd4aaf`. MESP-38 - Produce Security,
Audit, and Data Governance BRD - is the next exact Jira item, currently To Do,
and was not activated by the MESP-37 session.

## Objective

Produce a bounded Release 1 B2B ERP Security, Audit, and Data Governance BRD.
Define business requirements and acceptance scenarios for Tenant isolation,
object-level authorization, audit history, retention/deletion/legal-hold
policy boundaries, attachments and private exports, privacy/security
monitoring, support access, permissions, and separation of duties. Keep the
artifact documentation-only: it must not implement or prescribe source code,
database schema, migrations, endpoints, UI, providers, credentials, or
production infrastructure.

## Required entry reading and verification

Before changing anything, read completely and verify the live state of:

1. `AGENTS.md`;
2. `.ai/CURRENT_STATE.md`;
3. this `TASK.md`;
4. `docs/28_Release_1_Saudi_Localization_BRD.md`;
5. `docs/27_Release_1_Saudi_Localization_Scope_Rebaseline.md`;
6. `docs/26_Saudi_Regulatory_Evidence_and_External_Validation_Readiness.md`;
7. `docs/94_Product_Delivery_Master_Plan.md`;
8. `docs/staticts.md`;
9. canonical `docs/MESP_PRD_v1.2.docx` structurally, with visual QA attempted
   under the documents-skill workflow when the local renderer is available;
10. `docs/Decisions.md`, the Product Decision Register Jira item MESP-22, and
    the applicable ADRs, especially ADR-002, ADR-004, ADR-006, ADR-007,
    ADR-008, ADR-009, ADR-011, and ADR-018;
11. approved relevant BRDs for Platform/Foundation, Inventory, Procurement,
    Finance, Sales, Reporting, Master Data, and Saudi localization; and
12. live Jira items MESP-23, MESP-37, MESP-38, MESP-48, MESP-49, MESP-50,
    MESP-53, MESP-54, MESP-110, MESP-111, and MESP-112, plus the current
    branch, worktree, and `main` baseline.

Record the fresh MESP-38 entry evidence before drafting. Verify MESP-37 is
Done, MESP-38 is the single next To Do item, MESP-23 remains the living Open
Questions Register, and the production gates remain open. Use the existing
MESP-38 Jira issue; do not create a duplicate issue.

## In scope

The BRD may define, at business-requirement level only:

- Tenant ownership, isolation, and no-cross-Tenant visibility;
- company/branch/object access boundaries and server-derived authorization;
- role, permission, delegation, support-access, and separation-of-duties
  requirements without implementing an approval catalogue;
- audit events, actor/tenant/object context, before/after or decision context,
  integrity expectations, search/export controls, and evidence requirements;
- policy-level requirements for retention, deletion, legal hold, privacy,
  security monitoring, incident evidence, and support access, with unresolved
  operational values explicitly gated;
- attachment, download, and export authorization/classification boundaries;
- cross-module control ownership and impacts across the approved Release 1
  B2B ERP domains;
- fallback, failure, mixed-content, denial, and auditability behavior;
- Given/When/Then acceptance scenarios that remain business-testable and do
  not imply implementation authorization; and
- future implementation and external-validation handoff boundaries.

## Explicit exclusions and gates

Do not implement or claim completion of any of the following in this session:

- application source, tests, EF entities, tables, migrations, APIs, UI, or
  provider/infrastructure configuration;
- legal advice, PDPL compliance, privacy certification, DPO/controller status,
  data-subject rights workflows, transfer-impact assessments, SCCs/BCRs,
  regulator approval, certification, or external validation;
- chosen retention periods, purge schedules, legal-hold durations, residency
  decisions, backup/restore/DR behavior, or production deletion mechanics;
- closure of MESP-48 supported-volume, MESP-49 Release 1 scope, MESP-50
  retention/privacy/legal-hold/purge/residency/backup/restoration gates,
  MESP-53 security decision, MESP-54 exchange-rate decision, MESP-110,
  MESP-111 external-validation status, or any MESP-23 open question;
- tax, ZATCA, FATOORA, e-invoicing, statutory reporting, payment-provider or
  other external production integration behavior;
- Currency or Finance decisions owned by MESP-54, Retail POS, or Wafra-
  specific core behavior; or
- automatic activation of MESP-39 or any later item.

## Documentation and Jira discipline

Use a focused documentation branch and one canonical BRD file under `docs/`.
Preserve the living decision register and existing Product Decision IDs;
append traceability only when justified and do not silently close or promote
an unresolved decision. Keep product language bounded to a Saudi-localized
Core ERP Release 1 B2B baseline; do not use statutory, legal, privacy,
certified, or government-integrated claims.

Use the standing Owner approval for normal bounded BRD work. Record activation,
validation, approval, traceability, handoff, and closure evidence in Jira.
Keep MESP-23, MESP-48, MESP-50, MESP-53, MESP-54, and MESP-110 open unless a
separate authorized decision explicitly changes them. Do not create parallel
implementation work or child coding items.

## Validation and handoff

Before finishing this session:

1. Validate the BRD against the exact scope, exclusions, traceability, and
   business acceptance scenarios.
2. Run `git diff --check` and targeted documentation checks; verify the task
   diff contains no source, schema, migration, endpoint, UI, provider,
   credential, or production configuration changes.
3. Review the complete diff against the correct base and preserve unrelated
   user work.
4. Update `.ai/CURRENT_STATE.md`, `docs/94_Product_Delivery_Master_Plan.md`,
   every genuinely affected Markdown state/plan file, and `docs/staticts.md`
   conservatively. Update this file with the next exact session only after
   MESP-38 is genuinely complete.
5. Update live Jira with evidence and the next exact handoff; do not activate
   the next item automatically.
6. Commit and push the bounded documentation/state changes, merge the focused
   PR only when clean and unblocked, verify `main` and `origin/main` agree,
   and verify the worktree is clean.

Stop after handing off the completed MESP-38 session for independent ChatGPT
review. Do not execute the next `TASK.md` in the same chat.

# Next session — MESP-116 — Release 1 Consolidated Owner Decision Approval and Implementation-Unblock Reconciliation

## Session boundary

This is the exact next bounded session after MESP-115. Execute only this
decision-approval and implementation-unblock reconciliation in a fresh chat.
Do not execute this prompt in the current MESP-115 completion session. Do not
start the first capability automatically after completing it.

Release 1 remains the full-feature reusable B2B ERP. **31 August 2026 —
Release 1 Integrated Preview** is an integrated preview of the real codebase,
not an MVP, throwaway/demo UI, Wafra fork, or scope cut. Unfinished capability
remains required after the preview.

MESP-38 is Done. MESP-39 remains To Do, unactivated, and not executed as a
future-release Integrations and External Services BRD. MESP-40 remains To Do,
unactivated, and required for Release 1 in the migration wave. MESP-23 remains
In Progress as the living Open Questions Register. MESP-117–MESP-142 are the
not-activated capability backlog under existing module Epics.

## Objective

Read the canonical Consolidated Owner Decision Pack and obtain explicit
Hossam/Owner approval, rejection, or deferral for every applicable A/B row.
Apply only the decisions explicitly approved in this session; do not convert
recommendations into requirements by inference. Append the next Product
Decision entries only for explicit Owner decisions, reconcile the existing
MESP-23 and Jira owner records without duplicates, publish the final
dependency map, and hand off the first capability task for a separate later
session.

This session is governance/readiness only. It must not implement source, tests,
persistence, schema, migrations, APIs, UI, providers, credentials,
infrastructure, production configuration, MESP-39, or migration execution.

## Required entry reading and live verification

Read and verify, in this order:

1. `AGENTS.md` and `CLAUDE.md`;
2. `.ai/CURRENT_STATE.md` and this `TASK.md`;
3. `docs/30_Release_1_Full_Feature_Fast_Track_Delivery_Plan.md`;
4. `docs/31_Release_1_Consolidated_Owner_Decision_Pack.md` completely;
5. `docs/32_Release_1_Tax_VAT_Scope_Clarification.md` completely;
6. `docs/staticts.md`;
7. `docs/Decisions.md`, the glossary, `docs/94_Product_Delivery_Master_Plan.md`,
   and the affected ADRs/contracts only;
8. the owning and adjacent approved BRDs for any row being approved,
   especially Master Data, Procurement, Inventory, Finance, Sales, Reporting,
   Localization, and Security/Audit/Governance;
9. live Jira MESP-115, MESP-116, MESP-22, MESP-23, MESP-39, MESP-40,
   MESP-41–MESP-55, MESP-110, MESP-113, MESP-117–MESP-142, and the named
   MESP-48/MESP-50/production gates; and
10. current branch, worktree, `main`, and `origin/main`.

Do not reread every BRD or the entire PRD unless an approval row creates a
real cross-module or architecture question. Use the owning BRD and the exact
decision row as the source of truth.

## Explicit approval protocol

The Owner Decision Pack contains 31 rows: 16 A Owner-decidable rows, 6 B
specialist/input-dependent rows, and 9 C production/external/legal gates.

- Present/record the exact row ID, current issue owner, recommended position,
  alternatives, dependencies, specialist input, and the Owner decision.
- Treat every recommendation as **NOT APPROVED UNTIL OWNER SIGNS**.
- A “yes” applies only to the exact bounded position stated. It does not
  approve statutory compliance, an external integration, production
  credentials, a legal conclusion, a volume number, or a wider module scope.
- A “no”, “defer”, or “needs input” remains open in MESP-23 and the original
  Jira issue with an explicit next owner/action; do not close it ceremonially.
- Class C rows remain gates. Do not turn them into guessed product behavior.
- Keep MESP-39 future-release and unactivated. Keep MESP-40 unactivated while
  retaining it in the Release 1 sequence.

The Owner directions already recorded in PD-024 are not reopened as Pack rows:
full Release 1/preview intent, essential cycles, sequential one-person
delivery, Luna execution, reserved Opus checkpoints A/B/C, external
integration deferral, and internal configuration-led Tax/VAT restoration
without statutory/external scope. PD-023 remains immutable.

## Required reconciliation work

1. Verify that MESP-117–MESP-142 are still To Do/not activated and remain
   attached to the existing module Epics. Do not create duplicate readiness
   or capability tickets.
2. Update MESP-23 with row-level decision evidence, preserving its In Progress
   status until the living register itself has a valid closure basis.
3. Update the original MESP-41–MESP-55, MESP-110, MESP-113, and other affected
   Jira issues with traceability/comments only as justified. Preserve the
   durable MESP-113 owner for INV-OD-004. Do not close MESP-48/MESP-50 or
   production gates through this session.
4. If an approved row materially changes the plan, update the canonical plan,
   Tax/VAT clarification, current state, and any directly affected traceability
   file. Do not rewrite approved historical BRDs to erase history.
5. Append Product Decision entries after PD-024 only for explicit Owner
   decisions. Include scope, rationale, alternatives, dependencies, owner,
   status, and exact Jira evidence. Never append a recommendation as if it was
   approved.
6. Publish a final dependency map showing capability task, owning Epic,
   prerequisite decision/BRD/ADR, source-of-truth module, backend/API/DB/UI
   surfaces, auth/audit/localization gates, validation, and Preview/full-R1
   acceptance. Select exactly one first capability task; do not activate it
   automatically.
7. Update `docs/staticts.md` conservatively. Planning/Jira/decision activity
   does not increase production percentages. Update raw Jira counts, forecast,
   milestone, blockers, Tax/VAT classification, and Progress History only when
   materially applicable.

## Safety and scope gates

Stop and report a real blocker if an Owner decision would require:

- Tenant leakage, client-supplied authority, authentication/authorization
  weakness, or unsafe support access;
- accounting imbalance, source-to-GL ambiguity, tax-posting corruption,
  untraceable correction, or unreconciled FX/valuation;
- stock ledger, reservation, tracking, negative-stock, count, valuation, or
  return/data-integrity risk;
- destructive migration, data loss, purge, rollback, or irreversible cutover;
- a legal/statutory/external-validation conclusion that cannot be safely
  deferred;
- credentials, external providers, production infrastructure, or deployment;
  or
- material scope or architecture expansion beyond the approved Release 1
  reusable B2B baseline.

Safe local contract and capability work is not blocked merely because a later
production gate is open. This session itself remains documentation/Jira only.

## Validation and delivery

Before finishing:

- verify MESP-38 Done and MESP-39 not executed;
- verify MESP-40 remains an unactivated Release 1 requirement;
- verify MESP-23 remains the living register and no recommendation was silently
  approved;
- verify internal Tax/VAT is classified R1 required/Not Started without
  statutory scope;
- verify full-feature scope, no Retail POS, no Wafra-specific core behavior,
  and no implementation/source/test/schema/API/UI/provider/credential/
  infrastructure change outside the later capability session;
- run relevant documentation/Jira checks, `git diff --check`, and a complete
  task diff/allowlist review;
- use one focused branch/PR, review it, merge only when clean, synchronize
  `main` and `origin/main`, and record the reviewed head and final merge SHA;
- update MESP-116, MESP-23, current state, tracker, and the exact next TASK;
  and
- stop. Do not execute the first capability task in this chat.

## Completion report required

Report the approved/deferred/rejected counts by class, row-level Jira keys,
any resulting PD number/status, MESP-23 status, MESP-39/MESP-40 disposition,
Tax/VAT boundary, unchanged production percentages, final dependency-map
selection, first capability handoff, PR/reviewed head/merge SHA, validation,
and exact changed files. Explicitly state that no external integration and no
next implementation task was executed.

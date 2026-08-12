# Next session - MESP-120 - Implement Exchange Rate and multi-currency master-data capability

## Session boundary

MESP-119 is complete at its bounded internal configuration-led Tax/VAT
implementation scope. Focused PR #62 was reviewed at
`ec280a552f328416a52adbda212170a9c1c059fa` and merged to `main` at
`fd34dadb7fb96a680f61765ad3c67d3ec1a26572`. Jira MESP-119 is **Done** with
activation comment `10987`, validation/review comment `10988`, and closure
comment `10989`. The implementation and its state/tracker synchronization
are the completed preceding session.

The exact next capability is **MESP-120 - Implement Exchange Rate and
multi-currency master-data capability**. It is the only fresh-session scope.
Verify its live Jira status, Definition of Ready, current decision evidence,
and affected repository state before activation. Execute only MESP-120 and
stop after its bounded completion or a real blocker. Do not start MESP-121 or
any other capability automatically.

Release 1 remains the full-feature reusable B2B ERP. **31 August 2026 -
Release 1 Integrated Preview** is a running preview of the real codebase, not
an MVP, throwaway/demo UI, Wafra fork, or scope cut. Unfinished functionality
remains required after the preview.

## Objective

Implement one usable internal, reusable, manually/configured Exchange Rate
and multi-currency master-data vertical slice. It must be Tenant safe,
server-authorized, auditable, bilingual, effective-dated, historically
reproducible, and consumable by later Finance, Inventory, Procurement, Sales,
and Reporting capabilities.

Master Data owns configured currency/rate identity, effective history, safe
reference selection, source/provenance notes, and reproducible applied-rate
evidence. Finance owns the applied accounting result, realized/unrealized FX,
revaluation, posting, period, rounding/accounting consequences, reconciliation,
and irreversible downstream decisions. Consume PD-043 only at its approved
contract-bound position; Finance/Reporting specialist validation remains
mandatory.

## Required entry reading and live verification

Read and verify, in this order:

1. `AGENTS.md`, `CLAUDE.md`, `.ai/CURRENT_STATE.md`, and this
   `TASK.md`;
2. `docs/30_Release_1_Full_Feature_Fast_Track_Delivery_Plan.md`,
   `docs/31_Release_1_Consolidated_Owner_Decision_Pack.md`,
   `docs/32_Release_1_Tax_VAT_Scope_Clarification.md`, and
   `docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md`;
3. the live Jira items MESP-120, MESP-23, MESP-54, MESP-53, and MESP-110,
   reconciling stale Jira wording with current approved PD-043/PD-044 evidence;
4. the approved Master Data, Finance, and Reporting BRDs/specifications that
   directly define Currency and Exchange Rate ownership;
5. PD-033, PD-035, PD-036, PD-037, PD-043, PD-044, and PD-046, each only at
   its exact recorded boundary;
6. ADR-002, ADR-005, ADR-006, ADR-011, the shared authorization/audit/
   localization contracts, the existing Currency/Payment Terms implementation,
   and affected tests; and
7. current branch/worktree, `main`, `origin/main`, current diff, and the
   actual backend/Angular topology before changing files.

Do not reread every BRD or the entire PRD routinely. Use the approved
Currency/FX contract and exact decision evidence as the source of truth. If a
Finance/Reporting decision required for safe local rate identity, effective
selection, or reproducible evidence is genuinely unresolved, record the
concrete blocker in MESP-23 and stop rather than inventing a rule.

## Approved boundaries and scope

The implementation may cover:

- Tenant-owned Currency and configured Exchange Rate identities, source notes,
  provenance, precision inputs, and bilingual labels where the existing
  Currency contract requires them;
- manually/configured effective-dated rates with deterministic selection,
  non-overlapping history, historical applied-rate evidence, permissions,
  audit, optimistic concurrency, and safe unknown outcomes;
- reusable transaction/functional/Reporting Currency reference contracts
  without performing Finance posting, revaluation, or settlement;
- Active/Inactive lifecycle where approved by the existing Master Data
  contract, with no invented Draft/delete path;
- safe API/contracts, module-owned persistence/schema mappings according to
  ADR-002/ADR-006, and connected Angular English/Arabic/RTL maintenance,
  history, pending/blocked, denied, conflict, loading, and empty states; and
- focused backend, persistence/provider, API, architecture, and Angular tests
  required by the changed capability, with SQL/provider/production validation
  reported honestly.

## Explicit exclusions and gates

This session must not:

- add automated external FX feeds, bank feeds, external providers,
  credentials, webhooks, integrations, or production infrastructure;
- promote the unapproved recommended Finance behaviors into requirements;
  do not invent realized/unrealized FX, revaluation, rounding/accounting,
  reconciliation, period/year-end, approval/override, correction, or
  migration rules beyond the exact approved contract and existing local
  seams;
- implement Finance posting, source-to-GL, valuation, AP/AR settlement,
  period close, revaluation, or irreversible accounting consequences;
- activate or execute MESP-39, activate MESP-40, perform migration/cutover,
  or close SQL/provider/production, Finance, Reporting, legal, privacy, or
  specialist validation gates;
- implement Price Lists, Procurement, Inventory, Sales, Returns, Reporting
  catalogue, Retail POS, or Wafra-specific core behavior; or
- create a parallel Currency/FX model or widen PD-043/PD-044/PD-046 by
  assumption.

Preserve G-SEC, G-AUD, G-LOC, G-DATA, G-PROD, MESP-48, MESP-49, MESP-50,
Finance/Reporting specialist validation, SQL/provider, privacy/legal,
migration, and production gates. No external production integration is
authorized.

## Definition of Done and validation

MESP-120 is complete only when the real repository demonstrates the approved
internal Currency/Exchange Rate master and reference contract end to end for
authorized Tenant users, including persistence/schema as allowed by the
repository topology, API contracts, server-derived authorization, audit,
effective-date/history, idempotency/concurrency, deterministic validation,
unknown outcomes, historical applied-rate evidence, bilingual/RTL Angular
journeys, and focused tests. Do not claim completion for placeholder data or a
disconnected demo.

Before handoff:

- run the narrowest relevant domain, backend, contract, persistence/provider,
  and Angular tests/builds, including affected regressions;
- inspect the complete diff for Tenant isolation, Finance ownership, audit,
  concurrency, localization, effective-date/history integrity, no silent FX
  assumptions, and source-scope boundaries;
- update MESP-120 with activation, validation, review, and closure evidence;
- update MESP-23 only for a genuinely discovered open decision or blocker;
- update `.ai/CURRENT_STATE.md`, `docs/staticts.md`, and relevant plan/state
  documents conservatively; percentages reflect verified usable capability,
  never Jira or documentation activity alone;
- use one focused branch and PR, review the complete diff, merge only when
  clean, synchronize `main` and `origin/main`, and record the reviewed head
  and final merge SHA; and
- replace this file with the exact next bounded task and stop. Do not execute
  that next task in the same chat.

## Completion report required

Report MESP-120's activated scope, decision rows used, validation results,
security/audit/localization/concurrency/effective-date evidence, Jira
status/comments, any MESP-23 additions, production-capability percentage
changes or unchanged status, PR/reviewed head/merge SHA, synchronized branch
state, and the exact next TASK handoff. Explicitly state that MESP-39,
MESP-40 activation, automated/external FX, Finance posting/revaluation,
production gates, migration/cutover, and all other capabilities were not
executed unless separately authorized.

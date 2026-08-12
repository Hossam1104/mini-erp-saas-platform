# Next session - MESP-118 - Implement Currency and Payment Terms complete Master Data capability

## Session boundary

MESP-117 is complete at its bounded implementation scope: shared Angular
Master Data UX for Category, Unit of Measure, Product, Supplier, and Business
Customer, plus only the missing Category/UOM public REST seam over the existing
application and module-owned persistence contracts. The focused review is PR
#60 from `feature/mesp-117-master-data-angular-ux`; the implementation head is
recorded in the current state and Jira evidence. Supplier work remained limited
to master-data identity, contacts, lifecycle, audit, and concurrency. Procurement
Supplier Confirmation remains downstream MESP-124 and was not implemented.

MESP-116 is **Done** at its bounded Owner decision and implementation-unblock
reconciliation scope. PD-025 through PD-046 in MESP-22 comment `10958` and the
approved dependency map in `docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md`
remain authoritative. PD-033, PD-035, PD-036, and PD-037 apply at their exact
approved global Master Data boundaries wherever MESP-118 consumes them; do not
reopen or generalize those decisions.

The exact next capability is **MESP-118 - Implement Currency and Payment Terms
complete Master Data capability**. It remains **To Do, not activated, and not
executed** at handoff. Verify its live Jira status, Definition of Ready, and
decision gates in a fresh session before activation. Execute only MESP-118 and
stop after its bounded completion or a real blocker. Do not start MESP-119 or
any other capability automatically.

Release 1 remains the full-feature reusable B2B ERP. **31 August 2026 -
Release 1 Integrated Preview** is a running preview of the real codebase, not
an MVP, throwaway/demo UI, Wafra fork, or scope cut. Unfinished functionality
remains required after the preview.

## Objective

Implement Currency and Payment Terms as one usable, Tenant-safe Master Data
vertical slice with its applicable domain, application, persistence, API,
authorization, audit, concurrency/idempotency, and Angular EN/AR/RTL behavior.
Master Data owns reusable identity, lifecycle, effective-dated configuration,
and deterministic read references. Finance owns transaction and accounting
meaning; do not silently implement Finance posting, valuation, aging, or
settlement behavior in this capability.

This is one capability implementation session. It may add or correct only the
minimum necessary Currency/Payment Terms contracts, entities, persistence,
migrations, endpoints, Angular screens, validation, and focused tests required
by the approved bounded contract. Do not create a parallel configuration model
or invent an unresolved Finance decision.

## Required entry reading and live verification

Read and verify, in this order:

1. `AGENTS.md`, `CLAUDE.md`, `.ai/CURRENT_STATE.md`, and this `TASK.md`;
2. `docs/30_Release_1_Full_Feature_Fast_Track_Delivery_Plan.md`;
3. `docs/31_Release_1_Consolidated_Owner_Decision_Pack.md`;
4. `docs/32_Release_1_Tax_VAT_Scope_Clarification.md` and
   `docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md`;
5. the approved Master Data/Product Catalog, Finance, and Reporting
   BRDs/specifications, especially `docs/16` through `docs/20`, and the
   Currency/Payment Terms source contracts;
6. PD-033, PD-035, PD-036, and PD-037 as the approved global Master Data
   boundaries, plus the directly applicable PD-031, PD-043, and PD-044 rows;
7. ADR-002, ADR-005, ADR-006, ADR-011, shared auth/audit/localization,
   existing Master Data models, and directly affected tests;
8. live Jira MESP-118, MESP-110, MESP-54, MESP-53, MESP-23, and the named
   security/audit/SQL/provider/production gates; and
9. current branch, worktree, `main`, `origin/main`, current diff, and actual
   backend/Angular topology before changing files.

Do not reread every BRD or the entire PRD routinely. Use the owning contract
and exact approved decision as the source of truth. If MESP-110/FIN-OD-09 or
MESP-54/FIN-OD-04 leaves a material Payment Term or currency/rate rule
unresolved, record the concrete blocker in MESP-23 and stop rather than
choosing a silent default.

## Approved boundaries and scope

The implementation may cover:

- Tenant-owned Currency identity and lifecycle, with no cross-Tenant sharing
  and no client-supplied authority or scope;
- Payment Terms identity, deterministic configuration, effective dates/history,
  validation, and bounded downstream read references only after the applicable
  Finance contract is verified;
- Active/Inactive guarded lifecycle with no invented Draft/delete path, server-
  derived permission authority, optimistic concurrency, mandatory business
  audit, and no history rewrite;
- bilingual English/Arabic forms and lists, RTL/LTR layout, accessibility,
  loading/empty/error/denied/unknown/conflict states, and responsive Angular UX;
- safe references and effective-date behavior that are deterministic and
  historical, without external rate sourcing or accounting calculations; and
- focused backend, API, persistence, migration, and Angular tests required by
  the changed capability, with SQL/provider validation reported honestly.

## Explicit exclusions and gates

This session must not:

- activate or execute MESP-39, activate MESP-40, or perform migration/cutover
  without the separately required migration readiness and production gates;
- implement external FX providers, automated external rates, bank feeds,
  payment gateways, webhooks, external SSO, credentials, or infrastructure;
- implement Finance posting, valuation, realized/unrealized FX, revaluation,
  aging, settlement, cash/bank, or Reporting catalogue behavior;
- implement Tax/VAT, ZATCA/FATOORA, statutory/legal/certification/submission
  behavior, Procurement, Supplier Confirmation, Inventory, Sales, or returns;
- add Retail POS or Wafra-specific core behavior; or
- widen PD-033/035/036/037 or resolve MESP-110/MESP-54 by assumption.

Preserve G-SEC, G-AUD, G-LOC, G-DATA, G-PROD, MESP-48, MESP-50, SQL/provider,
privacy/legal, and specialist Finance/Reporting validation gates. No external
production integration is authorized.

## Definition of Done and validation

MESP-118 is complete only when the real repository demonstrates the approved
Currency and Payment Terms capability end to end for authorized Tenant users,
including applicable persistence/migration, API contracts, server-derived
authorization, audit, effective-date/history, idempotency/concurrency,
validation and unknown outcomes, bilingual/RTL Angular journeys, and focused
tests. Do not claim completion for placeholder data or a disconnected demo.

Before handoff:

- run the narrowest relevant domain, backend, contract, SQL/provider, and
  Angular tests/builds, including affected regressions;
- inspect the complete diff for Tenant isolation, Finance ownership, audit,
  concurrency, localization, no-history-rewrite, and source-scope boundaries;
- update MESP-118 with activation, validation, review, and closure evidence;
- update MESP-23 only for genuinely discovered open decisions or blockers;
- update `.ai/CURRENT_STATE.md`, `docs/staticts.md`, and relevant plan/state
  documents conservatively; percentages reflect verified usable capability,
  never Jira or documentation activity alone;
- use one focused branch and PR, review the complete diff, merge only when
  clean, synchronize `main` and `origin/main`, and record reviewed head and
  final merge SHA; and
- replace this file with the exact next bounded task and stop. Do not execute
  that next task in the same chat.

## Completion report required

Report MESP-118's activated scope, decision rows used, validation results,
security/audit/localization/concurrency/effective-date evidence, Jira
status/comments, any MESP-23 additions, production-capability percentage
changes or unchanged status, PR/reviewed head/merge SHA, synchronized branch
state, and the exact next TASK handoff. Explicitly state that MESP-39,
MESP-40 activation, external providers/integrations, production gates, and all
other capabilities were not executed unless separately authorized.

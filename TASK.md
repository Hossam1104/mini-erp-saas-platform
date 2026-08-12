# Next session - MESP-119 - Implement internal configuration-led Tax/VAT master and engine contract

## Session boundary

MESP-118 is complete at its bounded Currency and Payment Terms Master Data
scope. The implementation owns reusable Tenant-safe Currency identity and
lifecycle, deterministic functional/transaction/Reporting Currency references,
and reusable Payment Term identity, effective-dated versions, base-date and
schedule configuration, exact installment validation, discount configuration,
historical read references, and deterministic due-date preview. The reviewed
PR, merge, Jira closure, validation, and synchronized repository evidence are
recorded in `.ai/CURRENT_STATE.md` and `docs/staticts.md`.

MESP-110 and MESP-54 are **Done** at their approved bounded scopes through
PD-044 and PD-043. Their older Jira/task wording must not be treated as an
implementation blocker. Finance/Reporting specialist validation remains
mandatory before production or irreversible accounting, valuation, close,
revaluation, migration, or cutover decisions. MESP-23 remains **In Progress**
as the living register; do not add a new question unless this session finds a
genuinely concrete unresolved decision that blocks safe local Tax/VAT work.

The exact next capability is **MESP-119 - Implement internal configuration-led
Tax/VAT master and engine contract**. It is the single fresh-session scope.
Verify its live Jira status, Definition of Ready, current decision evidence,
and affected repository state before activation. Execute only MESP-119 and
stop after its bounded completion or a real blocker. Do not start MESP-120 or
any other capability automatically.

Release 1 remains the full-feature reusable B2B ERP. **31 August 2026 -
Release 1 Integrated Preview** is a running preview of the real codebase, not
an MVP, throwaway/demo UI, Wafra fork, or scope cut. Unfinished functionality
remains required after the preview.

## Objective

Implement one usable internal, reusable, configuration-led Tax/VAT Master Data
and deterministic engine-contract vertical slice. The slice must be Tenant
safe, server-authorized, auditable, bilingual, effective-dated, historically
reproducible, and consumable by later Procurement, Sales, Finance, Returns,
and Reporting capabilities. Master Data owns the reusable tax identity and
configuration contract; Finance owns accounting posting, valuation, period,
reversal, reconciliation, and irreversible downstream consequences.

PD-024 restores internal configuration-led Tax/VAT as a Release 1 requirement.
It does not authorize statutory interpretation, legal/tax advice,
ZATCA/FATOORA behavior, government submission, signing, clearance,
certification, or an external provider. The detailed Tax/VAT decisions in
`docs/32_Release_1_Tax_VAT_Scope_Clarification.md` remain authoritative and
must not be silently filled with defaults.

## Required entry reading and live verification

Read and verify, in this order:

1. `AGENTS.md`, `CLAUDE.md`, `.ai/CURRENT_STATE.md`, and this `TASK.md`;
2. `docs/30_Release_1_Full_Feature_Fast_Track_Delivery_Plan.md`;
3. `docs/31_Release_1_Consolidated_Owner_Decision_Pack.md`;
4. `docs/32_Release_1_Tax_VAT_Scope_Clarification.md` and
   `docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md`;
5. the approved Master Data, Finance, Procurement, Sales, Returns, and
   Reporting BRDs/specifications that directly define Tax/VAT ownership;
6. PD-024 and the directly applicable PD-033, PD-035, PD-036, PD-037, PD-040,
   PD-043, and PD-046 rows, at their exact recorded boundaries;
7. ADR-002, ADR-005, ADR-006, ADR-011, shared authorization/audit/
   localization contracts, current Master Data models, and affected tests;
8. live Jira MESP-119, MESP-23, MESP-49, MESP-53, MESP-110, and MESP-54; and
9. current branch/worktree, `main`, `origin/main`, current diff, and actual
   backend/Angular topology before changing files.

Do not reread every BRD or the entire PRD routinely. Use the owning Tax/VAT
contract and exact approved decisions as the source of truth. If the exact
taxable-base, inclusive/exclusive, exemption, rounding, account-mapping,
period/correction, reporting, or migration behavior required for safe local
implementation is not decided by the applicable evidence, record the
concrete blocker in MESP-23 and stop rather than inventing a rule.

## Approved boundaries and scope

The implementation may cover:

- Tenant-owned Tax/VAT identities, categories, codes, bilingual labels,
  direction/applicability, and the approved internal configuration shape;
- versioned, effective-dated rates with no overlap, deterministic selection,
  permission checks, audit, concurrency, and no rewrite of historical applied
  evidence;
- a server-authoritative deterministic calculation/engine contract only for
  the explicitly approved taxable-base, rate, applicability, and rounding
  rules, including reproducible applied-rate evidence;
- safe read/application references that preserve tax identity, category/code,
  rate version, base, amount, currency, rounding inputs, and source lineage
  where the owning document contract requires them;
- Active/Inactive lifecycle with no invented Draft/delete path, server-derived
  authorization, mandatory business audit, idempotency/concurrency, unknown
  outcomes, and Tenant isolation;
- bilingual English/Arabic forms and lists, RTL/LTR layout, accessibility,
  loading/empty/error/denied/unknown/conflict states, and responsive Angular
  UX; and
- focused backend, API, persistence, and Angular tests required by the
  changed capability, with SQL/provider/production validation reported
  honestly.

## Explicit exclusions and gates

This session must not:

- implement ZATCA/FATOORA, statutory/legal compliance conclusions, government
  submission, clearance, signing, certification, e-invoicing adapters, or
  external tax providers;
- invent inclusive/exclusive calculation, taxable-base treatment for
  discounts/charges/freight, exemption behavior, rounding, account mapping,
  period/correction/reversal policy, report catalogue, scheduling, or
  migration quarantine rules that are not explicitly approved;
- implement Finance tax posting, source-to-GL, period close, valuation,
  reconciliation, realized/unrealized FX, revaluation, AP/AR settlement, or
  irreversible accounting consequences;
- activate or execute MESP-39, activate MESP-40, perform migration/cutover,
  add providers/credentials/infrastructure, or close production gates;
- implement Exchange Rates, Pricing, Procurement, Inventory, Sales, Returns,
  Reporting catalogue, Retail POS, or Wafra-specific core behavior; or
- create a parallel Tax/VAT configuration model or widen PD-024/PD-033/
  PD-035/PD-036/PD-037/PD-040/PD-043/PD-046 by assumption.

Preserve G-SEC, G-AUD, G-LOC, G-DATA, G-PROD, MESP-48, MESP-49, MESP-50,
SQL/provider, privacy/legal, migration, and Finance/Tax/Reporting specialist
validation gates. No external production integration is authorized.

## Definition of Done and validation

MESP-119 is complete only when the real repository demonstrates the approved
internal Tax/VAT master and engine contract end to end for authorized Tenant
users, including persistence/schema as allowed by the repository topology,
API contracts, server-derived authorization, audit, effective-date/history,
idempotency/concurrency, deterministic validation and unknown outcomes,
historical applied evidence, bilingual/RTL Angular journeys, and focused
tests. Do not claim completion for placeholder data or a disconnected demo.

Before handoff:

- run the narrowest relevant domain, backend, contract, persistence/provider,
  and Angular tests/builds, including affected regressions;
- inspect the complete diff for Tenant isolation, Finance ownership, audit,
  concurrency, localization, effective-date/history integrity, no silent tax
  assumptions, and source-scope boundaries;
- update MESP-119 with activation, validation, review, and closure evidence;
- update MESP-23 only for a genuinely discovered open decision or blocker;
- update `.ai/CURRENT_STATE.md`, `docs/staticts.md`, and relevant plan/state
  documents conservatively; percentages reflect verified usable capability,
  never Jira or documentation activity alone;
- use one focused branch and PR, review the complete diff, merge only when
  clean, synchronize `main` and `origin/main`, and record reviewed head and
  final merge SHA; and
- replace this file with the exact next bounded task and stop. Do not execute
  that next task in the same chat.

## Completion report required

Report MESP-119's activated scope, decision rows used, validation results,
security/audit/localization/concurrency/effective-date evidence, Jira
status/comments, any MESP-23 additions, production-capability percentage
changes or unchanged status, PR/reviewed head/merge SHA, synchronized branch
state, and the exact next TASK handoff. Explicitly state that MESP-39,
MESP-40 activation, statutory/external Tax/VAT behavior, external providers/
integrations, production gates, and all other capabilities were not executed
unless separately authorized.

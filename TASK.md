# Next session - MESP-117 - Complete Master Data shared Angular UX for existing Category/UOM/Product/Supplier/Customer slices

## Session boundary

This is the exact next bounded session after MESP-116. MESP-116 is **Done** at
its documentation/Jira/governance-only scope. Hossam approved A1-A16 and B1-B6
at their exact bounded positions, subject to the amendments and clarifications
recorded in `docs/31_Release_1_Consolidated_Owner_Decision_Pack.md`, Jira
comment `10957` on MESP-116, and PD-025 through PD-046 in MESP-22 comment
`10958`. Class B is the Release 1 product/implementation contract; Finance,
Inventory, Reporting, Migration, and other named specialist validation remains
mandatory before production or irreversible accounting, destructive migration,
or cutover decisions. C1-C9 remain open and are not approved or closed.

MESP-117 is the approved first capability handoff, but at the start of this
fresh session it remains **To Do, not activated, and not implemented**. Verify
its live status and Definition of Ready before activating it. Execute only this
capability and stop after its bounded completion or a real blocker. Do not
start MESP-118 or any other next capability automatically.

Release 1 remains the full-feature reusable B2B ERP. **31 August 2026 -
Release 1 Integrated Preview** is a running preview of the real codebase, not
an MVP, throwaway/demo UI, Wafra fork, or scope cut. Unfinished functionality
remains required after the preview.

## Objective

Complete the shared Angular user experience for the existing Master Data
Category, Unit of Measure, Product, Supplier, and Business Customer slices,
using their approved contracts and existing platform/module boundaries. Make
the existing bounded capability usable in the real codebase for authorized
Tenant users, with truthful loading, validation, error, audit, localization,
and workflow behavior.

This is a capability implementation session. It may add or correct only the
minimum necessary Angular components, routes, services, API contract seams,
server validation, and focused tests required to complete these existing
slices. It must not redesign the domain, invent missing business decisions,
or create a parallel Master Data model.

## Required entry reading and live verification

Read and verify, in this order:

1. `AGENTS.md`, `CLAUDE.md`, `.ai/CURRENT_STATE.md`, and this `TASK.md`;
2. `docs/30_Release_1_Full_Feature_Fast_Track_Delivery_Plan.md`;
3. `docs/31_Release_1_Consolidated_Owner_Decision_Pack.md`;
4. `docs/32_Release_1_Tax_VAT_Scope_Clarification.md` and
   `docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md`;
5. the approved Master Data/Product Catalog and adjacent BRDs/specifications
   in `docs/16` through `docs/20`, the applicable glossary, and the existing
   Master Data contracts/entities/ADRs;
6. ADR-002, ADR-005, ADR-011, the shared platform/auth/audit/localization
   contracts, and any directly affected API/UI tests;
7. live Jira MESP-117, the prior Category/UOM/Product/Supplier/Customer issue
   evidence, MESP-23, and the named security/audit/production gates; and
8. current branch, worktree, `main`, `origin/main`, current diff, and the
   actual Angular/backend topology before changing files.

Do not reread every BRD or the entire PRD unless a real cross-module question
requires it. Use the owning contract and the exact approved decision as the
source of truth. If the Definition of Ready is not met, record the concrete
missing prerequisite and stop without speculative implementation.

## Approved scope

The implementation may cover the shared UX and necessary existing seams for:

- Category and Unit of Measure lists, search/filter/pagination, detail/forms,
  authorized lifecycle actions, validation, and references;
- Product identity/list/detail/forms, Tenant-unique SKU/barcode behavior as
  already contracted, Active/Deactivate/Reactivate behavior, and tracking
  configuration display only; operational batch/lot/serial/expiry behavior
  remains Inventory-owned;
- Supplier and Business Customer list/detail/forms, authorized lifecycle and
  confirmation behavior, and bounded references already in their contracts;
- shared navigation, table/form patterns, bilingual EN/AR presentation,
  RTL/LTR layout, localized validation/error/empty/loading states,
  permission-aware actions, server-derived authority, audit visibility, and
  optimistic-concurrency/conflict handling; and
- focused API/client contract corrections and tests only where they are
  required to make the existing five slices usable and preserve their module
  ownership.

Preserve the approved boundaries: Tenant-safe ownership with no cross-Tenant
sharing; no client-supplied authority; no invented Draft state; no hidden
approval thresholds; no EAN/GS1 rules; no Product/Item variant entity; no
history rewrite; no unaudited client-side business calculation; and no
automatic approval or posting.

## Explicit exclusions and gates

This session must not:

- activate or execute MESP-39, activate MESP-40, or perform migration;
- implement Tax/VAT, Currency/FX, Finance posting/valuation, Reporting
  catalogue, Procurement, Inventory operational tracking/stock/reservation,
  Sales, returns, credit notes, payment gateways, bank feeds, external SSO,
  webhooks, providers, credentials, or infrastructure;
- resolve C1-C9, MESP-48, MESP-49's external boundary, MESP-50, statutory/
  legal/ZATCA/FATOORA behavior, certification, submission, clearance, signing,
  or production readiness;
- add Retail POS or Wafra-specific core behavior; or
- broaden the approved Release 1 contract because a UI needs a convenient
  default. Record unresolved decisions in MESP-23 and stop at the boundary.

Apply the cross-cutting gates from docs/33: Tenant isolation and server-side
authorization, business audit and actor/time/source evidence, EN/AR and
RTL/LTR localization, validation/concurrency/data-integrity behavior, and the
SQL/provider/production gates. Specialist validation remains mandatory before
production or irreversible decisions even when this local capability is
implemented safely.

## Definition of Done and validation

MESP-117 is complete only when the real repository demonstrates the agreed
shared UX for the five existing slices, including authorized list/detail/form
flows, truthful loading/empty/error/conflict states, bilingual/RTL behavior,
server-authoritative permissions and validation, audit evidence, and focused
tests for the changed behavior. Do not claim completion for screenshots,
placeholder data, or a disconnected demo path.

Before handoff:

- run the narrowest relevant backend, contract, and Angular tests/builds,
  including affected regressions and lint/type checks where configured;
- inspect the complete task diff and confirm no source or configuration change
  crosses the allowlisted Master Data/shared UX boundary;
- verify Tenant isolation, authorization, audit, localization, validation,
  concurrency, and no-history-rewrite behavior in the changed paths;
- update MESP-117 with activation, validation, review, and closure evidence;
- update MESP-23 only for genuinely discovered open decisions or blockers;
- update `.ai/CURRENT_STATE.md`, `docs/staticts.md`, and the relevant plan/state
  documents conservatively. Production percentages increase only for verified
  usable capability, never for ticket activity or UI scaffolding alone;
- use one focused branch and PR, review the complete diff, merge only when
  clean, synchronize `main` and `origin/main`, and record the reviewed head and
  final merge SHA; and
- replace this file with the exact next bounded task and stop. Do not execute
  that next task in the same chat.

## Completion report required

Report MESP-117's activated scope, changed files, bounded decisions used,
validation results, security/audit/localization/concurrency evidence, Jira
status/comments, any MESP-23 additions, production-capability percentage
change (or explicitly unchanged), PR/reviewed head/merge SHA, synchronized
branch state, and the exact next TASK handoff. Explicitly state that MESP-39,
MESP-40 activation, external integrations, production gates, and all other
capabilities were not executed unless separately authorized by a later task.

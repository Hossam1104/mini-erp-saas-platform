# Next session - MESP-121 - Implement Price List and deterministic B2B pricing capability

## MESP-121 Phase A handoff - 13 August 2026

MESP-121 is the live active capability and remains **In Progress**. Activation
evidence is Jira comment `11025`. Luna completed **Phase A only** on the exact
shared branch `feat/MESP-121-price-list-b2b-pricing`; source commit
`dffa142e6b9c2fef4987d6229689087a9ecf238f` is pushed and draft PR [#64](https://github.com/Hossam1104/mini-erp-saas-platform/pull/64)
is open. The same branch and draft PR are reserved for Kimi's Phase B Angular
work, followed by Sonnet's final review/fix pass. Do not merge this PR,
transition MESP-121 to Done, or start MESP-122.

Phase A delivered the Tenant-owned Price List identity, existing Product /
Business Customer / Currency / UOM references, effective-dated version
history, lifecycle, deterministic applicability and priority resolution,
duplicate/overlap/conflict prevention, provenance, immutable reference
evidence, server-derived authorization and organization scope, audit,
optimistic concurrency, idempotency seams, module-owned persistence, and the
Foundation-catalogued REST/OpenAPI contract. The public operation IDs are:

`master-data.price-list.list`, `master-data.price-list.read`,
`master-data.price-list.history.read`, `master-data.price-list.create`,
`master-data.price-list.edit`, `master-data.price-list.price.append`,
`master-data.price-list.deactivate`, `master-data.price-list.reactivate`,
`master-data.price-list.reference.read`, and
`master-data.price-list.audit.read`.

Phase B must add the connected Angular list/detail/history/create/edit/lifecycle
and applicability journeys on this same branch, with EN/AR and RTL/LTR
behavior plus loading, empty, denied, validation, conflict, unavailable, and
pending states. It must not add Sales Orders, promotions/POS, Finance,
Procurement pricing, external providers, migration/cutover, MESP-39,
MESP-40 activation, or any other capability. The final project completion
percentages remain unchanged for this Phase A handoff. This file must remain
the MESP-121 task/handoff; it must not be replaced with MESP-122 in this
session.

## Session boundary

MESP-120 is complete at its approved bounded Exchange Rate and multi-currency
Master Data scope. Focused PR #63 was reviewed at
`f4d6485fd8b70a88ba34b68f1acae15a8c255ff6` and merged to `main` at
`14f6f4923d2897d891f33f5eb4405d2fe2089e69`. Jira MESP-120 is **Done** with
activation comment `10990`, validation/review comment `11023`, and closure
comment `11024`. The implementation and its post-merge state/tracker/root-task
synchronization are the completed preceding session.

The active capability is **MESP-121 - Implement Price List and deterministic
B2B pricing capability**. Its live Jira status and activation evidence were
verified for this session. Continue only the bounded Phase B handoff above;
do not start MESP-122 or any other capability automatically.

Release 1 remains the full-feature reusable B2B ERP. **31 August 2026 -
Release 1 Integrated Preview** is a running preview of the real codebase, not
an MVP, throwaway/demo UI, Wafra fork, or scope cut. Unfinished functionality
remains required after the preview.

## Capability

Implement Price Lists, customer/product/currency applicability, effective
dating, deterministic precedence, controlled manual pricing where approved,
snapshots, and downstream Sales consumption. Do not invent retail promotions
or POS behavior.

## Ownership and traceability

- Owning Epic: MESP-6 with Sales consumption under MESP-9.
- Owning baselines: the approved Master Data and Sales BRDs/specifications.
- Decision gates: MD-OD-004 / SAL-OD-01 precedence and applicability;
  MD-OD-005 approval catalogue; MESP-46 credit remains separate. No
  promotion/retail rules may be invented.
- Source of truth: Price List master configuration. Sales snapshots the
  commercial facts used on documents; do not make downstream documents depend
  on mutable current configuration.

## Required entry reading and live verification

Read and verify, in this order:

1. `AGENTS.md`, `CLAUDE.md`, `.ai/CURRENT_STATE.md`, and this `TASK.md`;
2. `docs/30_Release_1_Full_Feature_Fast_Track_Delivery_Plan.md`,
   `docs/31_Release_1_Consolidated_Owner_Decision_Pack.md`, and
   `docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md`;
3. live Jira MESP-121, MESP-23, MESP-46, and the decision/contract evidence
   for MD-OD-004, SAL-OD-01, and MD-OD-005, reconciling any stale Jira wording
   with the immutable Product Decision Register;
4. the approved Master Data and Sales BRDs/specifications that define price
   lists, commercial applicability, precedence, snapshots, and downstream
   ownership;
5. the existing Product, Business Customer, Currency, and Payment Terms
   implementations and their affected contracts/tests;
6. ADR-002, ADR-005, ADR-006, ADR-011, the Foundation REST operation
   catalogue, shared authorization/audit/localization contracts, and directly
   affected module boundaries; and
7. the current branch/worktree, `main`, `origin/main`, current diff, and the
   actual backend/Angular topology before changing files.

Do not reread every BRD or the entire PRD routinely. Use the live MESP-121
contract, exact decision evidence, existing Master Data capabilities, and
affected Sales contracts as the source of truth. If precedence, applicability,
approval, snapshot, or another decision required for safe deterministic pricing
is genuinely unresolved, record the concrete blocker in MESP-23 and stop
rather than inventing a rule.

## Approved boundaries and scope

The implementation may cover:

- Tenant-owned reusable Price List identities and controlled lifecycle, using
  the existing Product, Business Customer, and Currency identities;
- customer/product/currency applicability and effective-dated price versions
  within the exact approved contract;
- deterministic precedence and conflict/duplicate handling only where
  MD-OD-004 and SAL-OD-01 authorize the rule;
- controlled manual pricing where explicitly approved, with exact server-
  derived authority, audit evidence, optimistic concurrency, idempotency
  seams, and safe unknown/no-applicable-price outcomes;
- immutable commercial snapshots/evidence for downstream Sales consumption,
  without allowing later master-data edits to rewrite historical documents;
- module-owned persistence/schema mappings, API/contracts, generated
  OpenAPI/Scalar documentation, and complete authorization/Tenant context;
- connected Angular English/Arabic, RTL/LTR Price List list/detail/history/
  create/edit/lifecycle/applicability journeys with loading, empty, denied,
  conflict, validation, unavailable, and pending states; and
- focused domain, persistence/provider, API, architecture, authorization,
  Tenant-isolation, audit, localization, and Angular tests, with SQL/provider/
  production validation reported honestly.

## Explicit exclusions and gates

This session must not:

- implement retail promotions, POS, discount campaigns, coupons, loyalty,
  retail pricing, or Wafra-specific core behavior;
- invent precedence, customer segmentation, approval, override, price formula,
  rounding, tax, currency-conversion, or conflict rules outside the exact
  approved MD-OD-004/SAL-OD-01/MD-OD-005 contract;
- implement MESP-46 credit-limit/credit-control behavior or Finance posting,
  accounting, valuation, settlement, period, tax, FX, or irreversible
  downstream consequences;
- add automated/external providers, integrations, credentials, bank feeds,
  webhooks, production infrastructure, or external pricing sources;
- activate or execute MESP-39, activate MESP-40, perform migration/cutover, or
  close SQL/provider/production, legal, privacy, or specialist gates;
- create a parallel Product, Customer, Currency, or Price List model that
  bypasses existing Tenant ownership and module boundaries; or
- implement Procurement, Inventory, Returns, Reporting catalogue, future
  Promotions, or any other capability beyond the MESP-121 contract.

Preserve G-SEC, G-AUD, G-LOC, G-DATA, G-PROD, MESP-48, MESP-49, MESP-50,
Finance/Sales specialist validation, SQL/provider, privacy/legal, migration,
and production gates. No external production integration is authorized.

## Definition of Done and validation

MESP-121 is complete only when the real repository demonstrates the approved
Price List capability end to end for authorized Tenant users, including the
applicable domain behavior, application/service behavior, persistence/schema,
database integrity, API contracts, server-derived authorization, audit,
effective dating/history, deterministic precedence, concurrency/idempotency,
safe unknown outcomes, immutable Sales snapshot evidence, bilingual/RTL
Angular journeys, affected integration contracts, and focused tests. Do not
claim completion for placeholder data or a disconnected demo.

Every public REST operation must be present in the Foundation operation
catalogue with its exact route, permission, Tenant scope, antiforgery, audit,
unsafe-effect, concurrency, idempotency, and effective-date metadata; appear
in generated OpenAPI/Scalar with a stable operation ID, useful summary and
boundary description, explicit request/parameter/response/error outcomes; and
be covered by architecture/contract tests that reject missing or placeholder
documentation.

Before handoff:

- run the narrowest relevant domain, backend, contract, persistence/provider,
  authorization/Tenant-isolation, and Angular tests/builds, including affected
  regressions;
- inspect the complete diff for Tenant isolation, pricing precedence,
  snapshot immutability, audit, concurrency, localization, effective-date
  integrity, no silent commercial assumptions, and source-scope boundaries;
- update MESP-121 with Phase A activation/validation and the shared-branch
  handoff evidence; final review and closure remain pending for the later
  review phase;
- update MESP-23 only for a genuinely discovered open decision or blocker;
- update `.ai/CURRENT_STATE.md`, `docs/staticts.md`, and relevant plan/state
  documents conservatively; percentages reflect verified usable capability,
  never Jira or documentation activity alone;
- use one focused branch and draft PR, review the complete Phase A diff, and
  leave the PR unmerged for Kimi Phase B and Sonnet final review; and
- preserve this MESP-121 Phase A handoff in this file. Do not replace it with
  MESP-122 or execute Phase B in the same Luna session.

## Completion report required

Report MESP-121's activated Phase A scope, decision rows used, validation results,
Tenant/authorization/audit/localization/concurrency/effective-date/precedence/
snapshot evidence, Jira status/comments, any MESP-23 additions,
production-capability percentage changes or unchanged status, draft PR/head
and merge-pending state, synchronized branch state, and the exact Phase B
handoff. Explicitly state that Kimi Phase B and Sonnet final review remain
pending.
Explicitly state that MESP-39, MESP-40 activation, external providers,
Finance/accounting/credit behavior, production gates, migration/cutover,
retail POS/promotions, and all other capabilities were not executed unless
separately authorized.

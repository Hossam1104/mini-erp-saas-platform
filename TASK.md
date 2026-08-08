# M95-SL-02 — Category and UOM

## Session boundary

This is the exact next implementation session after the completed MESP-96 /
M95-SL-01 correction. A fresh Codex/Luna chat executes only this root task.
Do not start M95-SL-03 Product, any later Master Data slice, Retail POS, or
Wafra-specific core behavior in the same chat.

MESP-96 remains **Done** in Jira. M95-SL-01 remains complete, contract-only,
and non-persistent. Its functional PR #30 merged at
`87f150d95f583168a86aa56200916343c6404f7f`; the bounded post-merge correction
commit is `85d3c48f20a97f8057e5960c305a3bcc0cb8d672` on
`fix/mesp-96-optional-scope-hint`; correction PR #31 merged at
`4eeefe0d1a9af209cc3e31608812ec35ef283fd9`. The required state/task handoff
is published on synchronized `main`; Jira correction evidence is comment
`10657` and final repository reconciliation is comment `10658`.
Historical completion comments `10655` and `10656` remain preserved.

No Category/UOM source implementation, entity, table, migration, database
access, endpoint, or Jira child slice was created by the preceding session.

## Objective

Implement only the first data-bearing Master Data slice: Product Category and
Unit of Measure identity and safe conversion boundaries, after the completed
shared Tenant/scope and authorization contracts from M95-SL-01.

The slice must be configuration-led, Tenant-isolated, modular-monolith
compatible, and bounded to Category/UOM. Do not make technical choices that
silently answer an unresolved business decision.

## Required pre-work

Start from a clean, synchronized `main` and verify the exact local and remote
head. Read and reconcile:

- `AGENTS.md`, `CLAUDE.md`, `.ai/CURRENT_STATE.md`, and this `TASK.md`;
- the approved `docs/16_Master_Data_and_Product_Catalog_BRD.md`;
- `docs/17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md`;
- `docs/94_Product_Delivery_Master_Plan.md` and `docs/Decisions.md`;
- the relevant PRD/glossary and the actual Master Data contracts,
  application code, persistence conventions, and architecture tests;
- ADR-002, ADR-005, ADR-006, and ADR-011 material, including their live status
  in `docs/Decisions.md` and the Technology Architecture Baseline.

Verify the dedicated Jira item for this slice is the single active
implementation item and that its Definition of Ready is recorded. Do not
invent a Jira key, activate parallel work, or begin a later slice.

## Mandatory decision gate — resolve or explicitly Owner-bound before data work

Category/UOM is the first data-bearing Master Data slice. Before creating
persistence or implementing behavior that depends on any of the following,
inspect live Jira and the approved repository evidence. Use an approved bound
if one exists; do not treat a Founder Decision Pack recommendation as approved
by itself.

1. **MD-OD-001 — business availability scope.** Apply the approved
   Tenant/Company/Branch/Warehouse scope and inheritance boundary. An absent or
   optional scope must not be interpreted as Tenant-wide merely to simplify
   persistence or authorization. Client hints never replace trusted server
   authority.
2. **MD-OD-008 — Draft-before-Active lifecycle.** Apply the approved creation,
   activation, deactivation, and reactivation bound. Do not choose Draft or
   no-Draft behavior by technical preference.
3. **MD-OD-005 — approval catalogue and slice boundary.** Implement only the
   exact Category/UOM operations covered by an approved approval policy. Keep
   the generic no-self-approval and fail-closed hooks; do not infer Tax, Price
   List, Exchange Rate, or other approval requirements for this slice.
4. **MD-OD-002 — Category hierarchy depth.** Apply the approved maximum depth,
   parent/child rule, cycle behavior, and any flat-category bound. Do not
   assume unlimited, recursive, or flat hierarchy behavior without an
   explicit Owner-approved bound.
5. **MD-OD-006 — UOM precision and rounding.** Apply the approved precision,
   scale, conversion, and rounding boundary. Positive non-zero conversion
   validation may remain separate from precision/rounding policy, but do not
   invent a rounding algorithm or quantity scale.

If an approved bound is absent and persistence or executable Category/UOM
behavior would require inventing one of these decisions, stop at that genuine
decision blocker. Report the exact missing decision and recommend an explicit
Owner decision for the affected Category/UOM scope; do not create speculative
tables, migrations, defaults, or policy values.

## In-scope implementation

- Category and UOM contracts, owned entities, application behavior, and
  persistence only to the extent authorized by the verified decision gate.
- Tenant ownership, same-Tenant organization scope, cross-Tenant denial, and
  configuration-led multi-tenant behavior.
- Category duplicate and hierarchy validation within the approved scope.
- UOM duplicate and positive conversion-factor validation, with the approved
  precision/rounding policy isolated from generic contract validation.
- Lifecycle, authorization, approval, audit/evidence, optimistic concurrency,
  and safe error behavior required by the approved Category/UOM bound.
- Focused Category/UOM regression and architecture tests, including no
  cross-module table/repository access and no authority expansion from client
  input.

## Hard exclusions

Do not implement or decide:

- Product/Item identity, SKU, Barcode, variants, tracking, batch, lot, serial,
  expiry, tax linkage, or Product persistence (M95-SL-03);
- Supplier, Business Customer, Price List, Tax, Payment Term, Currency,
  Exchange Rate, import/migration, reporting, or downstream transaction
  behavior;
- Retail POS, anonymous consumers, or Wafra-specific core logic;
- a Tenant-wide default, an unapproved organization hierarchy rule, an
  unapproved approval catalogue, or an unapproved Draft/Active default;
- localization/search/forms/document behavior that depends on unresolved
  ADR-011 decisions;
- a new production project, microservice, endpoint topology, provider,
  database topology, or production readiness claim.

## Architecture and safety constraints

- Preserve ADR-002's `MiniErp.Api -> MiniErp.App -> MiniErp.Contracts`
  modular-monolith direction and existing architecture enforcement.
- Follow ADR-006 for module-owned persistence/context/schema/migration and
  cross-module transaction ownership. Do not reach another module's DbSet,
  repository, table, or migration.
- Preserve ADR-005's deny-by-default authorization and resource-policy
  boundary; use server-derived Tenant context and exact authorized scope.
- Preserve ADR-011 timing: do not implement affected Arabic search, collation,
  RTL, localized forms, or bilingual documents before that ADR is complete.
- Preserve MESP-48 supported-volume and MESP-50 retention, privacy,
  legal-hold, purge, residency, backup, and restoration gates.
- Do not hard-code Tenant IDs, Wafra values, Saudi-only behavior, or any
  tenant-specific policy. All behavior must be configuration-led.

## Required validation

Run targeted validation for the complete task diff:

- Release backend build with 0 warnings and 0 errors;
- focused Category/UOM tests plus existing `MasterDataBoundaryTests` and
  `ModuleBoundaryTests`;
- positive and negative Tenant/resource/scope isolation, including same codes
  in different Tenants and foreign/sibling denial;
- the approved Category hierarchy bound, cycle/depth behavior, UOM positive
  conversion, and the approved precision/rounding behavior;
- lifecycle and approval behavior only where explicitly bounded, optimistic
  concurrency, audit evidence, safe failures, and no-self-approval;
- architecture/dependency checks and a scan for Product/Item, SKU/Barcode,
  tracking, Retail POS, Wafra, unapproved decision defaults, and cross-module
  persistence access;
- `git diff --check`, complete diff review, and migration/database review.

If persistence is authorized, verify no unrelated entity/table/migration,
endpoint, provider, or database topology is introduced. If persistence is not
authorized because a decision gate is missing, stop and report the blocker.

## Completion and handoff

When the bounded slice is genuinely complete:

- update `.ai/CURRENT_STATE.md` with exact Jira, branch, commit, PR, merge,
  synchronized-main, validation, and decision-gate evidence;
- update `docs/94_Product_Delivery_Master_Plan.md` and any genuinely affected
  state/plan Markdown without rewriting unrelated history;
- keep all unresolved MD-OD entries visible and record exactly which approved
  bounds were used;
- keep MESP-48, MESP-49, and MESP-50 open unless separately approved by their
  owners;
- add factual Jira implementation/closure evidence, review the complete
  branch diff, commit and push a focused branch, publish a focused PR, merge
  only when clean and unblocked, then synchronize local `main` and
  `origin/main`;
- do not execute M95-SL-03 or any later `TASK.md` session automatically.

Stop after this one session and return the exact implementation, validation,
scope, decision-gate, Jira, PR, merge, and final-main report to
Hossam/ChatGPT for review.

`STOP — return the completion report to Hossam/ChatGPT before executing the
next root TASK.md.`

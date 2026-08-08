# Execute MESP-99 — M95-SL-02 Category and UOM

Execute exactly one bounded MESP-99 session in this chat. This is the first
data-bearing Master Data implementation slice after the completed MESP-100
readiness correction. Do not continue automatically to M95-SL-03 or any later
Master Data slice.

## Session handoff

MESP-100 is Done and its merged branch, PR, merge commit, validation, and Jira
closure evidence are recorded in `.ai/CURRENT_STATE.md`. MESP-99 is the
single active Jira implementation item for M95-SL-02. Start from the verified
synchronized `main` recorded there and read the live MESP-99 description and
comments before changing scope.

Hossam's standing Owner approval covers normal implementation, review,
commit, merge, closure, and next-session activation inside this approved
scope. Stop for a real Tenant-isolation or authorization weakness,
accounting/data-integrity risk, destructive migration/data-loss risk,
unresolved business decision, legal/privacy or external-validation blocker,
credential/production-infrastructure blocker, or material scope/architecture
change. Do not stop for ceremonial approval.

## Required reading

Before changing source, read:

- `.ai/CURRENT_STATE.md`;
- the live Jira items MESP-99 and its parent MESP-6;
- `docs/16_Master_Data_and_Product_Catalog_BRD.md`;
- `docs/17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md`;
- `docs/01_Technology_Architecture_Baseline.md`;
- `docs/ADR-002_Backend_Project_Structure_and_Module_Enforcement.md`;
- `docs/ADR-006_Shared_Database_Tenant_Isolation_and_Module_Persistence.md`;
- `docs/Decisions.md`, the approved glossary, Foundation specifications,
  and `docs/94_Product_Delivery_Master_Plan.md`;
- `backend/src/MiniErp.Contracts/Modules/MasterData`;
- `backend/src/MiniErp.App/Modules/MasterData`;
- the Infrastructure persistence boundary and existing architecture tests.

Treat explicit Owner decisions recorded in Jira and the approved MESP-31 BRD
as authoritative. Do not turn a recommendation, unresolved Open Decision, or
test helper into a new business requirement.

## Approved MESP-99 bounds

These five dispositions are approved for Category and UOM only. Preserve the
remaining MD-OD-001 through MD-OD-011 register for every other Master Data
domain.

| Decision | Required Category/UOM behavior |
|---|---|
| MD-OD-001 | Category and UOM are Tenant-wide inside the owning Tenant and reusable by all Companies and Branches in that Tenant. No cross-Tenant sharing. Client Tenant or scope input is never authority and cannot replace or broaden trusted server context. |
| MD-OD-005 | Routine Create, Edit, Activate, Deactivate, and Reactivate require no separate approver. Valid permission, server-derived Tenant authority, exact resource/scope authorization, and audit evidence remain mandatory. Preserve the generic approval/no-self-approval/fail-closed framework for future policies. |
| MD-OD-008 | No Draft lifecycle. An authorized valid record is created Active and may be Deactivated or Reactivated under the same authorization and audit controls. |
| MD-OD-002 | Category has an optional parent, a maximum depth of three levels, a same-Tenant parent requirement, and cycle prevention. Keep the depth rule configuration-led/evolvable so a policy change does not require schema redesign. |
| MD-OD-006 | Quantity precision is six decimal places; conversion-factor precision is eight decimal places; factors are positive and non-zero; calculated quantities round to six places using `MidpointRounding.AwayFromZero`; user values exceeding supported precision are rejected rather than silently rounded. |

Do not generalize these bounds to Product/Item, Supplier, Business Customer,
Price List, Tax, Payment Term, Currency, Exchange Rate, tracking, or a later
slice.

## Implementation scope

Implement only the Category and Unit of Measure behavior defined by the
approved MESP-99 Jira item and the five bounds above.

The slice may add the required Category/UOM domain contracts, application
behavior, persistence model, module-owned EF context/configuration, schema,
migrations, endpoints, and tests, but all of them must remain inside the
approved modular-monolith boundaries:

- Category/UOM persistence is owned by the Master Data module inside
  `MiniErp.Infrastructure`; another module may not add its DbSet, repository,
  table mapping, schema object, or migration operation.
- Use the existing four-project direction:
  `MiniErp.Api -> MiniErp.Infrastructure -> MiniErp.App -> MiniErp.Contracts`.
  The API may also reference App and Contracts for host composition. Contracts
  has no production-project dependency, App depends on Contracts only, and
  Infrastructure never references Api.
- Keep server-derived Tenant context, exact resource and scope authorization,
  fail-closed permission resolution, and audit behavior on every command and
  query. Category/UOM Tenant-wide business availability is a production-owned
  MESP-99 policy; do not manufacture it through a generic Tenant-only fallback.
- Use the server-owned immutable `MasterDataOperationCatalog` from MESP-100.
  Every defined operation must map to exactly one existing capability; callers
  must not provide a weaker or unrelated capability. Unknown or unmapped
  operations fail closed. If M99 adds a Reactivate operation, extend the
  operation enum, catalog, authorization tests, and audit vocabulary together.
- Enforce same-Tenant category parents, optional parentage, maximum depth, and
  cycle prevention in domain/application behavior and persistence constraints
  as appropriate. Do not silently accept a foreign parent.
- Enforce Active-on-create/no-Draft lifecycle and Deactivate/Reactivate
  transitions with valid permission, exact authorization, concurrency and
  audit controls.
- Reject over-precision input and use the approved UOM precision and rounding
  rules. Do not implement Product/Item conversion consumers or inventory
  behavior in this slice.

## Mandatory carry-forward corrections

Complete these before or as part of lifecycle implementation:

1. Replace the M95-SL-01 case-insensitive substring forbidden-token guard with
   an identifier/symbol-aware check so valid lifecycle names such as Active,
   Deactivate, and Reactivate do not create false positives while prohibited
   identifiers remain rejected.
2. Ensure invalid Master Data audit evidence cannot be constructed through a
   friend assembly or internal-access path. Enforce invariants at the evidence
   type/factory boundary, not only in caller discipline.
3. Preserve first persistent audit fidelity: Tenant, actor, session, affected
   record ID, business code, business scope, action, before/after values,
   policy outcome, approver where applicable, correlation/evidence identity,
   timestamp, and reason.
4. Derive Category/UOM scope and Tenant-wide availability from actual
   production-owned policy and trusted context; do not use a test-only
   containment helper as the production rule.
5. Make module-registration evidence reflect actual composition and registration
   state. A mechanical consolidation of duplicate
   `ModuleRegistrationEvidence` types is allowed only if it does not broaden
   scope or weaken the boundary.

## Hard exclusions

Do not implement any behavior outside MESP-99 Category/UOM:

- Product, Item, SKU, Barcode, variant, tracking, batch, lot, serial, or expiry;
- Supplier, Business Customer, Tax, Price List, Payment Term, Currency, or
  Exchange Rate implementation;
- Retail POS or Wafra-specific core behavior;
- generic approval decisions for other domains;
- M95-SL-03 or any later slice;
- production provisioning, production credentials, or a production database;
- a new production project, microservice, separate database, or replacement
  architecture.

Do not close unresolved MD-OD decisions by inference. Keep MESP-48,
MESP-49, and MESP-50 supported-volume, retention, privacy, legal-hold, purge,
residency, backup/restore, and production/provider gates open unless their own
approved evidence changes them.

## Validation and review

Run proportional validation for the complete task diff:

- Release backend build with 0 warnings and 0 errors;
- focused Master Data authorization, Tenant-isolation, scope, lifecycle,
  hierarchy, precision/rounding, audit, and module-boundary tests;
- architecture dependency/reference tests and project-reference validation
  against ADR-002 and ADR-006;
- safe disposable persistence/model/migration tests only where the approved
  local SQL Server harness is configured; do not provision production;
- `git diff --check`, source scans, and a full review of the complete diff;
- scans confirming no excluded domain, Retail POS/Wafra behavior, or
  cross-module persistence shortcut was introduced.

Record an unavailable SQL Server harness as the existing credential/provider
gate; never weaken its safety validator or invent a production connection.

## Jira, Git, and session boundary

Keep MESP-99 as the only active implementation item. Update Jira with factual
progress and closure evidence only after validation. Use one focused branch,
one intentional implementation commit series, a focused PR, complete PR
review, and a clean merge under the standing Owner approval. Synchronize local
`main` and `origin/main` after merge.

At the end of this session, update `TASK.md` to the next exact bounded task,
update `.ai/CURRENT_STATE.md` and every genuinely affected Markdown
state/plan/decision file, update Jira, commit and push, merge only when clean,
and stop. Never execute the next root `TASK.md` automatically in this chat.

# Next session - MESP-104 / M95-SL-04 Supplier master-data implementation only

MESP-102 / M95-SL-03 Product Identity implementation is complete. PR #37
merged to `main` at
`202d59068caac5d1fac402794627e41d7f452456`, and Jira closure evidence is
comment `10677` (activation `10675`, validation/merge `10676`).

MESP-103 / M95-SL-04 Supplier readiness is also complete and closed. The
Supplier-only Owner disposition is Jira comment `10681`, and closure evidence
is comment `10682`. MESP-104 is the separately prepared implementation item
under MESP-6, currently **To Do**; its non-activated handoff is Jira comment
`10683`. No Supplier implementation source has started. A fresh Codex/Luna
chat executes this root TASK.md exactly once and
then stops for review; do not execute it automatically in the current chat.

## Current approved Supplier boundary - 9 August 2026

The following Owner decisions apply only to the bounded Supplier master-data
slice. They do not resolve the global decision register or automatically define
Business Customer, Procurement, Finance, Tax, payment/banking, settlement, or
other downstream-domain policy.

- **MD-OD-001 - Supplier business availability:** Supplier master data is
  Tenant-wide inside its owning Tenant and reusable by that Tenant's Companies
  and Branches. Cross-Tenant sharing is prohibited. Client-supplied Company,
  Branch, Tenant, or scope values cannot override trusted server-derived
  authorization.
- **MD-OD-005 - Supplier approval policy:** Routine Supplier creation,
  identity/contact/reference maintenance, activation, deactivation, and
  reactivation require no separate approver in Release 1. Permission, Tenant
  authorization, optimistic concurrency, audit, and fail-closed controls are
  mandatory. Saudi statutory data and future payment, banking, settlement, or
  similarly sensitive changes are outside this base disposition and remain
  subject to their owning requirements and controls.
- **MD-OD-008 - Supplier lifecycle:** Supplier has no Draft state. A valid
  authorized Supplier is created Active and supports guarded
  Deactivate/Reactivate behavior. Deactivation prevents new applicable
  business use while historical references and audit history remain preserved.
- **MD-OD-007 remains open:** Saudi statutory fields are not resolved by this
  disposition. Keep the external Saudi legal/tax validation and production
  gate under MESP-49.

## Exact objective

Implement the bounded M95-SL-04 Supplier master-data slice described by the
approved BRD/LIS and the Owner evidence above. Deliver only the Supplier
identity, contact/reference, lifecycle, Tenant-ownership, authorization,
concurrency, audit, and API/persistence behavior needed by this slice. Supplier
is an external Business Party role; it is never a User, Tenant member,
credential holder, login identity, or consumer session.

The implementation must preserve the existing four-project topology:

- `MiniErp.Api`
- `MiniErp.App`
- `MiniErp.Contracts`
- `MiniErp.Infrastructure`

ADR-002 and ADR-006 remain binding. The future implementation is module-owned,
Tenant-owned, server-authorized, concurrency-safe, audited before effect, and
fail closed. Do not claim SQL/provider/production readiness when the configured
`MESP_SQLSERVER_CONNECTION_STRING` gate is unavailable.

## Entry gates

- Re-read `.ai/CURRENT_STATE.md`, `docs/19_Supplier_M95_SL_04_Readiness.md`,
  the approved `docs/16_Master_Data_and_Product_Catalog_BRD.md`, the relevant
  sections of `docs/17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md`,
  `docs/00_ERP_Business_Glossary.md`, the Foundation specification,
  `docs/01_Technology_Architecture_Baseline.md`, ADR-002, ADR-005, ADR-006,
  ADR-011's indexed baseline, `docs/94_Product_Delivery_Master_Plan.md`,
  `docs/Decisions.md`, and `docs/staticts.md`.
- Verify live Jira: MESP-103 is Done with Owner/closure evidence `10681`/
  `10682`; MESP-104 is the single intended Supplier implementation item and is
  To Do. Activate MESP-104 explicitly at the start of this fresh session, then
  record activation evidence before implementation.
- Preserve MD-OD-007 as an unresolved external Saudi legal/tax validation and
  production gate under MESP-49. Do not infer statutory fields or legal
  completeness from the Owner disposition.
- Preserve open MESP-48 supported-volume/retention/privacy/legal-hold/purge/
  residency/backup/restoration gates and MESP-50 retention/privacy/legal-hold
  gates. Do not invent credentials, production infrastructure, or a database
  connection string.

## Allowed implementation boundary

- Supplier contracts, application behavior, module-owned persistence, API
  endpoints, and focused tests required for the bounded Supplier slice.
- Server-derived Tenant ownership and exact authorization; client Company,
  Branch, Tenant, and scope hints are advisory at most and cannot broaden
  authority.
- Supplier identity/reference/contact behavior, same-role duplicate controls,
  cross-role Supplier/Business Customer match review without false duplicate
  rejection, guarded Active/Inactive lifecycle, historical-reference
  preservation, optimistic concurrency, idempotency where required, and
  append-before-effect audit evidence.
- Permission, authorization, concurrency, audit, provider, and policy failures
  must fail closed and must not be reported as successful business effects.
- Keep Saudi statutory fields extensible and conditional within the approved
  boundary; leave external/legal validation to MESP-49 and the owning control.

## Hard exclusions

- No Product/Item/SKU/Barcode/tracking changes and no modification of the
  completed M95-SL-03 Product implementation.
- No Business Customer implementation, unified Party entity by implication,
  Procurement transaction/PO/receipt/invoice behavior, Tax, Finance, payment,
  banking, settlement, Currency, Exchange Rate, Price List, Inventory, Sales,
  or downstream workflow behavior.
- No Supplier login, credential, authentication identity, membership, user
  account, anonymous consumer path, or external-party access path.
- No Draft lifecycle state, cross-Tenant sharing, client-authority trust,
  unscoped query, hard delete of referenced records, or loss of historical
  references/audit history.
- No Saudi legal conclusion, external compliance certification, production
  database provisioning, production migration execution, fabricated provider
  claim, or invented SQL credential.
- Do not broaden the Owner disposition to any domain outside Supplier.

## Required validation and handoff

- Review the complete source and documentation diff for Tenant leakage,
  authorization bypass, missing audit-before-effect, concurrency gaps,
  lifecycle/history errors, unscoped persistence, and downstream scope creep.
- Run the release build, Supplier-focused tests, relevant non-SQL architecture/
  module-boundary tests, REST/contract tests, and `git diff --check`. Record
  SQL safety-test limitations truthfully if the configured connection string
  remains unavailable.
- Verify no Product source changed and no excluded downstream behavior or
  migration was added. Inspect the final changed-file list and test output.
- Update every genuinely affected Markdown state/plan file, `.ai/CURRENT_STATE.md`,
  `docs/staticts.md`, Jira activation/validation/closure evidence, and this
  TASK.md to the next exact bounded session.
- Commit the bounded implementation, push the branch, publish a focused PR,
  review it, and merge only when the diff is clean, checks are truthful, and no
  security/Tenant/data-integrity/production gate is being bypassed. Then
  synchronize the local `main` baseline and stop for ChatGPT review.

## Stop conditions

Stop and report a blocker on unresolved Supplier ownership/scope, Tenant
isolation or authorization weakness, credential/login ambiguity, accounting or
data-integrity risk, destructive migration/data loss, missing provider or
production infrastructure, unresolved Saudi legal/external validation that is
required for the implementation, or material scope/architecture change.
Keep MESP-48, MESP-49, and MESP-50 open unless their own separately authorized
gates are satisfied. Do not execute another root TASK.md in this session.

# Next session - M95-SL-04 Supplier readiness revalidation after consolidated Owner decision bundle

MESP-102 / M95-SL-03 Product Identity implementation is complete. PR #37
merged to `main` at
`202d59068caac5d1fac402794627e41d7f452456`, and Jira closure evidence is
comment `10677` (activation `10675`, validation/merge `10676`). This
Product session is complete. Do not start this Supplier task automatically in
the current chat; a fresh session executes exactly this root TASK.md once and
then stops for review.

## Current Supplier readiness stop - 9 August 2026

MESP-103 is the single active Supplier readiness item under MESP-6 and is
**In Progress** with activation evidence in Jira comment `10679` and
analysis/decision-bundle evidence in comment `10680`. Independent readiness
analysis is complete in
`docs/19_Supplier_M95_SL_04_Readiness.md`. Closure is intentionally pending one
consolidated Owner evidence comment disposing MD-OD-001, MD-OD-005, and
MD-OD-008 for Supplier. Their recommendations are not approved by analogy to
Product or Category/UOM. MD-OD-007 remains a Saudi statutory/external-
validation gate under MESP-49. No Supplier source implementation has started.

The exact next session must revalidate the one decision bundle, update the
readiness evidence, and close MESP-103 only if the bundle and all non-business
Definition-of-Ready gates are genuinely complete. It must not create a new
Supplier implementation item, write Supplier source, or execute downstream
work automatically.

## Exact objective

Revalidate, and if the single consolidated Owner decision bundle is complete,
close the M95-SL-04 Supplier readiness/specification and decision gate only.
Supplier is an external Business Party role and does not create a login or
credential path. Confirm only the explicitly disposed Supplier role ownership,
scope, duplicate/contact, lifecycle, and statutory-field gates needed for a
later bounded implementation.
Preserve the approved BRD/LIS/glossary meaning and do not invent Saudi legal,
tax, procurement, or external-validation behavior.

## Entry gates

- Re-read the current Jira state and confirm MESP-102 is Done with PR #37
  merged, Jira comment `10677`, and the repository state in
  `.ai/CURRENT_STATE.md`.
- In this fresh session, revalidate MESP-103 under MESP-6, confirm it remains
  the only active implementation/readiness item, and do not create a duplicate
  Supplier item. It was activated in the completed readiness session and must
  not be reactivated or replaced without evidence of a real Jira discrepancy.
- Re-read the approved `docs/16_Master_Data_and_Product_Catalog_BRD.md`,
  `docs/17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md`,
  `docs/00_ERP_Business_Glossary.md`, the Foundation specification,
  `docs/01_Technology_Architecture_Baseline.md`, ADR-002, ADR-005, ADR-006,
  ADR-011's indexed baseline, `docs/94_Product_Delivery_Master_Plan.md`,
  `docs/staticts.md`, and `.ai/CURRENT_STATE.md`.
- Confirm MD-OD-001, MD-OD-005, and MD-OD-008 are explicitly bounded for the
  affected Supplier scope. MD-OD-007 Saudi statutory fields remain an
  external-validation/production gate and must not be guessed or silently
  resolved.
- Preserve ADR-002 four-project/module enforcement, ADR-005 authorization,
  ADR-006 shared SQL Server/module-owned persistence and provider gates, and
  ADR-011 localization/search/RTL/document timing. MESP-48, MESP-49, and
  MESP-50 remain open.

## Allowed readiness boundary

- Supplier role ownership as an external Business Party role, including its
  relationship to the later Business Parties seam.
- Supplier scope, same-role duplicate policy, cross-role match treatment,
  contact/reference expectations, lifecycle/history, no-login proof, and
  audit/concurrency/readiness acceptance criteria.
- Explicit decision-register treatment for unresolved Supplier and Saudi
  statutory/external-validation questions.
- Documentation, traceability, Jira evidence, and a precise handoff for a
  later separately activated Supplier implementation task.

## Hard exclusions

- No Supplier source code, entity, table, EF mapping, migration, repository,
  service, endpoint, API contract implementation, UI, or business behavior.
- No Product changes, Product/Item/SKU/Barcode/tracking work, or changes to
  the completed M95-SL-03 implementation.
- No Business Customer, Procurement workflow, purchasing transaction,
  approval catalogue, Tax, Finance, Inventory, Sales, or downstream behavior.
- No user credentials, login, authentication identity, anonymous consumer path,
  Retail POS, or Wafra-specific core behavior.
- No Saudi statutory/legal conclusion, external compliance certification,
  production database provisioning, migration execution, or provider claim.

## Required validation and handoff

- Validate the dedicated Jira item, reviewed sources, decision register,
  Supplier-only scope, and the no-source implementation boundary.
- Record unresolved decisions and external-validation gates explicitly; do not
  convert a readiness document into a business or legal approval.
- Review the complete documentation/Jira diff, update `.ai/CURRENT_STATE.md`,
  every genuinely affected Markdown state/plan file, `docs/staticts.md`, Jira,
  and this TASK.md to the next exact bounded session.
- Commit and push the bounded readiness work, merge only when clean and
  unblocked, and stop. Never execute the next task automatically.

If MD-OD-001, MD-OD-005, or MD-OD-008 still lacks Supplier-specific Owner
evidence, do not close or merge the readiness gate as complete. Keep MESP-103
In Progress, preserve one consolidated decision bundle, and stop for that
decision rather than opening repeated approval loops.

## Stop conditions

Stop on unresolved Supplier ownership/scope decisions, Tenant-isolation or
authorization weakness, credential/login ambiguity, legal/privacy or Saudi
external-validation dependency, destructive migration/data-loss risk, missing
provider/production infrastructure, or material scope/architecture change.
Keep MESP-48, MESP-49, and MESP-50 open unless their own separately authorized
gates are satisfied.

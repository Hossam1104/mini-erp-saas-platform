# Next session - MESP-106 / Master Data authorization and duplicate-audit classification hardening only

MESP-107 / M95-SL-05 Business Customer master-data implementation is **Done**.
The bounded implementation merged through PR #41 at
`fb632982d06fd4f6bf965fb15dff7701a0bddcec`; its implementation head was
`acd3f399f17c9f551efa65b9dd284a797ae31ec6`. Jira activation, validation, and
closure evidence are comments `10692`, `10726`, and `10727`.

The repository is synchronized on `main`. The completed Customer slice adds
only the approved external B2B Customer identity, Tenant-wide ownership inside
the owning Tenant, server-derived authority, same-role integrity, contacts,
Active/Inactive lifecycle, concurrency, audit, contracts/routes, and
module-owned Business Parties persistence. It adds no login, credential,
membership, portal, consumer, unified Party, statutory registration, Saudi
legal conclusion, downstream commercial behavior, migration, provider, or
production-readiness claim.

MESP-106 is the single next exact sequence position. It is currently **To Do**
and non-blocking. Do not activate it, change its Jira status, or execute its
source work automatically in the preceding MESP-107 session. A fresh session
must verify its separate activation/Definition of Ready before changing source.

## Exact objective

Execute only MESP-106, **Master Data authorization and duplicate-audit
classification hardening**. Review and, if required, correct the shared
classification seam so that:

- unavailable or unmapped authorization dependencies such as
  `permission_unavailable`, `scope_policy_unavailable`,
  `approval_policy_unavailable`, `resource_policy_unavailable`, and
  `authorization_operation_unmapped` remain fail-closed but are represented as
  service/configuration unavailability rather than a genuine caller denial;
- genuine permission, resource, scope, Tenant, and cross-Tenant denials remain
  denials and never become availability successes;
- deterministic Supplier duplicate outcomes such as `supplier_duplicate`,
  `supplier_code_duplicate`, and `supplier_registration_duplicate` use the
  approved validation/conflict audit classification rather than generic
  internal-failure classification;
- the already-implemented Customer classification is reviewed for consistency
  without expanding Customer, Supplier, or Product behavior; and
- no effect, false success, audit loss, or Tenant existence leakage occurs on
  any hardened failure path.

The deliverable may be the smallest safe source/test correction, or a bounded
implementation note and targeted tests proving that the existing behavior is
already correct. Do not create unrelated refactoring merely to increase ticket
coverage.

## Entry gates

- Read `AGENTS.md`, `.ai/CURRENT_STATE.md`, this `TASK.md`, and the current
  `docs/staticts.md` before changing scope.
- Read the approved `docs/16_Master_Data_and_Product_Catalog_BRD.md`,
  `docs/17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md`,
  `docs/00_ERP_Business_Glossary.md`, the Foundation specification,
  `docs/01_Technology_Architecture_Baseline.md`, ADR-002, ADR-005, ADR-006,
  ADR-011's indexed baseline, `docs/94_Product_Delivery_Master_Plan.md`,
  `docs/Decisions.md`, and the Supplier and Customer readiness/implementation
  evidence.
- Verify live Jira: MESP-107 is Done with closure evidence `10727`; MESP-106
  is the single next To Do/non-blocking hardening item and is separately
  activated before source work; MESP-48, MESP-49, and MESP-50 remain open.
- Confirm the actual topology remains
  `MiniErp.Api -> MiniErp.Infrastructure -> MiniErp.App -> MiniErp.Contracts`.
  Do not add cross-module EF access, a new ADR, a migration, or a production
  connection assumption.
- Inspect the current branch, complete diff, existing Supplier/Customer
  authorization and audit paths, and current test conventions before editing.

## Required boundary

- Keep the change cross-cutting only to the approved authorization/audit
  classification seam and its focused tests/evidence.
- Preserve server-derived Tenant/resource authority, fail-closed behavior,
  append-before-effect audit, optimistic concurrency, and no-false-success
  guarantees.
- Preserve the separate Supplier and Customer role records and the Customer
  external B2B/no-login/no-unified-Party boundary.
- If existing behavior is already correct, do not alter Product, Supplier, or
  Customer business rules; record the evidence and add only missing regression
  coverage if genuinely needed.
- Keep the SQL/provider/production gate truthful. Missing
  `MESP_SQLSERVER_CONNECTION_STRING` does not block ordinary non-SQL hardening,
  but it prevents claiming SQL Server or production readiness.

## Hard exclusions

- No new Customer/Supplier/Product fields, tables, migrations, routes, UI, or
  downstream commercial behavior beyond a necessary classification fix.
- No Product/Item/SKU/Barcode/Category/UOM changes, unified Party, login,
  credentials, membership, consumer, portal, Retail POS, or Wafra-specific
  core behavior.
- No Sales, AR/AP, Finance, Tax, credit, payment, banking, settlement, Price
  List, Payment Terms, Currency, Exchange Rate, Inventory, statutory, Saudi
  legal, data-retention, purge, residency, or production-infrastructure work.
- No automatic activation or execution of another Jira item after MESP-106.

## Required validation and handoff

- Add focused tests for dependency outage versus genuine denial, the Supplier
  duplicate audit reason/outcome, Customer consistency where applicable, no
  effect/no false success, and Tenant isolation/no existence leakage.
- Run the focused tests, relevant full non-SQL suite, Release build, and
  `git diff --check`. Run SQL safety checks only when the explicit connection
  gate is available; otherwise record the exact gate without fabricating a
  pass.
- Review the complete task diff for denial/outage misclassification, audit
  loss, Tenant leakage, false success, cross-module access, and accidental
  scope expansion.
- Update genuinely affected Markdown state/plan files, `.ai/CURRENT_STATE.md`,
  `docs/staticts.md`, and this `TASK.md`. Update MESP-106 with activation,
  validation, and closure evidence. Keep MESP-107 Done and MESP-48/MESP-49/
  MESP-50 open.
- Commit and push the bounded work, publish a focused MESP-106 PR, inspect
  review threads, address valid findings, rerun validation, merge only when
  clean and authorized, synchronize local `main`, close MESP-106 only with
  implementation/validation/merge evidence, and then stop for ChatGPT review.
- Do not execute the next root `TASK.md` automatically in the same chat.

## Stop conditions

Stop and report a blocker on Tenant leakage, authentication/authorization
weakness, audit or data-integrity risk, destructive migration/data loss,
unresolved scope/decision, legal or external validation, credential/
production-infrastructure failure, or material scope/architecture change.

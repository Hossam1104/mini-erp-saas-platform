# Next session - MESP-107 / M95-SL-05 Business Customer master-data implementation only

MESP-104 / M95-SL-04 Supplier master-data implementation is complete and
closed. The bounded implementation was delivered from
`9bf9afcd8a9ea427ed32b63ad9b655081e9592d3`, merged through PR #39 to `main`
at `721adeb27c366d2b8aedde66d006ac6a49956f99`, and closed in Jira with
activation, validation, and closure evidence in comments `10685`, `10686`, and
`10687`. The final validation was a Release build with 0 warnings/0 errors,
Supplier-focused tests 7/7, and the non-SQL suite 609/609. The 21 SQL Server
safety tests remain gated by the unavailable
`MESP_SQLSERVER_CONNECTION_STRING`; no migration, provider, or production
readiness claim was made.

MESP-105 / M95-SL-05 Business Customer readiness is **Done**. Hossam's
Customer-only Owner disposition is recorded in Jira comment `10691`, and
MESP-107 is the separate implementation item under MESP-6, activated in Jira
with implementation evidence comment `10692`. The exact approved bounds are
Tenant-wide Customer identity inside the owning Tenant, no cross-Tenant
sharing, no separate approver for routine Customer master-data maintenance,
and no Draft state with Active-on-authorized-create plus guarded
Deactivate/Reactivate. No Customer source behavior was added by the readiness
or activation handoff.

The next exact bounded sequence position is **MESP-107 implementation only**.
A fresh session must re-read the approved BRD/LIS, architecture, glossary,
ADRs, decision register, foundation specification, delivery plan, and the
Business Customer readiness record before changing source. It may implement
only the approved Business Customer master-data identity slice. It must not
start downstream Sales, AR/Finance, credit, payment, tax, currency, portal,
unified Party, Retail POS, or Saudi statutory behavior.

A fresh Codex/Luna chat executes this root TASK.md exactly once, completes only
the bounded implementation item described below, updates the repository state
and Jira truthfully, then stops for ChatGPT review. Do not execute the next
root TASK.md automatically in the current chat.

## Approved Customer-only disposition

The following decisions are approved only for M95-SL-05 Business Customer and
must not be generalized to other Master Data domains or treated as global
answers to the preserved decision register:

- **BC-OD-001 / MD-OD-001:** Customer master identity is Tenant-wide inside
  its owning Tenant and may be reused by authorized Companies and Branches in
  that Tenant. Cross-Tenant sharing is prohibited. Tenant ownership and
  resource authorization come from trusted server-side context; client-
  supplied Tenant, Company, Branch, or scope values cannot expand authority.
  Downstream commercial policy is not Tenant-wide by implication and must
  reference this Tenant-owned identity without duplicating ownership.
- **BC-OD-005 / MD-OD-005:** Release 1 requires no separate approver for
  routine Customer creation, identity/name maintenance, Customer
  code/reference maintenance, contacts, activation, deactivation, or
  reactivation. Permission, trusted Tenant/resource authorization,
  optimistic concurrency, audit, fail-closed dependency handling, and
  applicable integrity checks remain mandatory. Credit, banking/payment,
  statutory/tax, Finance/AR, Sales override, and settlement operations remain
  with their owning domains.
- **BC-OD-008 / MD-OD-008:** There is no Draft state. Authorized creation is
  Active. Deactivate and Reactivate are explicit guarded operations.
  Deactivation prevents selection for new applicable downstream transactions
  that require an Active Customer while preserving historical references,
  posted-document references, reporting visibility, and audit history.
  Reactivation requires current authorization, Tenant/resource validation,
  optimistic concurrency, duplicate/integrity validation, and audit.

MD-OD-007 is not resolved. Saudi statutory/legal/tax validation and the
related production gate remain external under MESP-49; no additional Saudi
identifiers, VAT/e-invoicing classifications, or statutory completeness may be
inferred. MESP-48 and MESP-50 remain open production gates.

## Exact objective

Implement MESP-107's bounded Business Customer master-data identity slice in
the actual four-project topology (`MiniErp.Api`, `MiniErp.App`,
`MiniErp.Contracts`, and `MiniErp.Infrastructure`) while preserving ADR-002,
ADR-005, ADR-006, and ADR-011. The implementation must provide only the
approved external B2B Customer master boundary and its required authorization,
integrity, concurrency, audit, lifecycle, contract, and focused-test evidence.

The implementation must keep Customer identity distinct from User/login,
Tenant membership, credentials, consumer sessions, external portals, and a
unified Party or variant entity. It must keep Customer master identity
separate from Sales, AR/Finance, credit, payment, tax, price-list, currency,
exchange-rate, inventory, and statutory behavior.

## Entry gates

- Re-read `.ai/CURRENT_STATE.md`, `docs/staticts.md`, the approved
  `docs/16_Master_Data_and_Product_Catalog_BRD.md`,
  `docs/17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md`,
  `docs/00_ERP_Business_Glossary.md`, the Foundation specification,
  `docs/01_Technology_Architecture_Baseline.md`, ADR-002, ADR-005, ADR-006,
  ADR-011's indexed baseline, `docs/94_Product_Delivery_Master_Plan.md`,
  `docs/Decisions.md`, and
  `docs/20_Business_Customer_M95_SL_05_Readiness.md`.
- Verify live Jira first: MESP-105 is Done with Owner disposition evidence
  `10691`; MESP-107 is the single active M95-SL-05 implementation item with
  activation evidence `10692`; and MESP-106 is To Do/non-blocking. Do not
  activate another item or infer permission for downstream work.
- Confirm the Customer-only bounds in the Jira and repository records. Do not
  inherit Supplier or Product decisions for other fields, domains, or scopes.
- Confirm MESP-48, MESP-49, and MESP-50 remain open. Do not require or invent
  credentials, a production connection string, a migration execution,
  provider validation, or a Saudi legal conclusion.
- Inspect the current branch, complete diff, existing Business Parties seam,
  module ownership, and existing test conventions before adding source.

## Required implementation boundary

- Implement an external B2B Business Customer master identity owned by one
  Tenant and reusable by authorized Companies/Branches in that Tenant.
- Derive Tenant/resource authority from trusted server-side context. Reject or
  ignore client-supplied scope expansion and prevent cross-Tenant existence
  leakage, duplicate matching, reads, writes, and lifecycle changes.
- Implement only the approved Customer identity/name, Customer code/reference,
  contact, duplicate/integrity, Active lifecycle, optimistic concurrency,
  authorization, audit, and stable downstream-reference boundary needed by
  MESP-107. Preserve history on deactivation and make inactive Customers
  unavailable to new active-only use where the implemented contract owns that
  check.
- Create authorized Customers Active; do not create Draft records. Guard
  Deactivate and Reactivate with permission, Tenant/resource validation,
  concurrency, duplicate/integrity validation, and audit.
- Keep routine master-data operations permissioned and auditable without
  inventing a separate approver. Keep sensitive approvals and commercial
  policies in their owning domains.
- Follow the actual module-owned persistence and API composition rules. Do not
  add a migration or claim SQL/provider/production readiness when the existing
  environment gate is unavailable; record any such gate truthfully.
- Use ADR-011's approved implementation timing for localized/bilingual
  behavior. Do not invent Saudi statutory fields, legal completeness,
  collation, normalization, or e-invoicing rules.

## Hard exclusions

- No Product/Item/SKU/Barcode/Category/UOM/Supplier changes and no unified
  Party, variant, consumer, or hidden retail-customer entity.
- No customer login, credentials, user account, Tenant membership, consumer
  session, external-party portal, or authentication identity.
- No Procurement, Sales orders/quotes, AR/AP, Finance, Tax, credit control,
  payment/receipt, banking, settlement, Price List, Payment Terms, Currency,
  Exchange Rate, Inventory, or downstream commercial policy behavior.
- No Saudi statutory/legal or tax inference; MD-OD-007 and MESP-49 remain
  external gates.
- No Retail POS, Wafra-specific core behavior, production provisioning,
  destructive migration, data purge, or unrelated refactoring.
- No automatic activation of a later slice or a second implementation item.

## Required validation and handoff

- Run the focused Customer tests and the relevant non-SQL validation suite;
  run the Release build and `git diff --check`. If SQL safety tests remain
  connection-gated, report the exact gate without fabricating a pass.
- Review the complete task diff for Tenant leakage, client-authority
  expansion, authentication ambiguity, duplicate/lifecycle integrity,
  audit-before-effect behavior, stale-write handling, accidental downstream
  policy, and accidental changes outside Customer scope.
- Update every genuinely affected Markdown state/plan file, `.ai/CURRENT_STATE.md`,
  `docs/staticts.md`, and this TASK.md. Update MESP-107 with activation,
  validation, and closure evidence; do not mark MESP-107 Done before the
  implementation, review, and validation are actually complete.
- Commit and push the bounded implementation, publish the review PR, merge
  only when clean and authorized, synchronize local `main`, and stop for
  ChatGPT review. Do not execute the next root TASK.md automatically.

## Stop conditions

Stop and report a blocker on Tenant leakage, authentication/authorization
weakness, accounting or data-integrity risk, destructive migration/data loss,
unresolved Customer scope, required Saudi legal/external validation,
credential/production-infrastructure failure, or material scope/architecture
change. Keep MESP-48, MESP-49, and MESP-50 open unless their own separately
authorized gates are satisfied.

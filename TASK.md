# Next session - M95-SL-05 Business Customer readiness and decision gate only

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

The next exact bounded sequence position is M95-SL-05 Business Customer
readiness and decision gate only. Live Jira currently has no dedicated
Business Customer issue after MESP-104 and no `MESP-105` key exists. Therefore
this handoff is not an activation or implementation authorization: a fresh
session must verify the live Jira item and Owner gate first, must not invent a
key, and must not start Business Customer source behavior automatically.

A fresh Codex/Luna chat executes this root TASK.md exactly once, completes only
the bounded readiness/decision work described below, updates the repository
state and Jira truthfully, then stops for ChatGPT review. Do not execute the
next root TASK.md automatically in the current chat.

## Current approved baseline and non-inheritance rule

The Supplier-only Owner disposition is complete, but its decisions are not a
global Business Customer baseline. Do not copy MD-OD-001, MD-OD-005, or
MD-OD-008 into Business Customer without a separate affected-scope analysis
and Owner disposition. Preserve MD-OD-007 as the external Saudi statutory,
legal/tax, and production gate under MESP-49. Preserve MESP-48 supported-volume,
retention, privacy, legal-hold, purge, residency, backup, and restoration gates
and MESP-50 retention/privacy/legal-hold gates.

Release 1 remains B2B ERP only. Retail POS, Wafra-specific core behavior,
consumer flows, and downstream Procurement, Finance, Tax, payment, banking,
settlement, AR/AP, Sales, Inventory, Price List, Currency, and Exchange Rate
behavior remain outside this readiness slice unless the approved source
documents identify a decision dependency that must be recorded for later
ownership. No unified Party model is implied by the existence of Supplier and
Business Customer roles.

## Exact objective

Prepare and independently review the M95-SL-05 Business Customer readiness and
decision gate only. Read the approved BRD/LIS and the existing architecture,
glossary, ADR, decision-register, foundation, and delivery-plan baselines;
identify the Business Customer-specific business, ownership, identity,
availability, lifecycle, authorization, audit, concurrency, localization,
legal/tax, and downstream dependencies; and present only the decisions that
require explicit Owner disposition before any Business Customer
implementation.

This session is documentation/readiness work. It must not add Business
Customer source behavior, entities, tables, schemas, migrations, database
access, endpoints, UI, generated clients, or downstream transaction behavior.
It must not modify the completed Supplier implementation or Product source.

## Entry gates

- Re-read `.ai/CURRENT_STATE.md`, `docs/staticts.md`, the approved
  `docs/16_Master_Data_and_Product_Catalog_BRD.md`,
  `docs/17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md`,
  `docs/00_ERP_Business_Glossary.md`, the Foundation specification,
  `docs/01_Technology_Architecture_Baseline.md`, ADR-002, ADR-005, ADR-006,
  ADR-011's indexed baseline, `docs/94_Product_Delivery_Master_Plan.md`,
  `docs/Decisions.md`, and the completed
  `docs/19_Supplier_M95_SL_04_Readiness.md` record.
- Verify live Jira first: MESP-104 is Done with comments `10685`/`10686`/
  `10687`, PR #39 is merged, and there is no active implementation item. If a
  dedicated Business Customer readiness item does not exist, do not fabricate
  `MESP-105`, do not activate an unrelated issue, and record the missing Jira
  item as a readiness handoff condition.
- Confirm that no Business Customer Owner decisions have been silently
  inherited from Supplier, Product, or the global decision register. Keep
  unresolved decisions explicit and scoped to Business Customer.
- Confirm MESP-48, MESP-49, and MESP-50 remain open and that no credential,
  production connection string, migration, or Saudi legal conclusion is
  required or invented for this documentation-only slice.

## Required readiness boundary

- Define Business Customer as an explicit Release-1 business scope without
  inventing a unified Party/consumer identity, customer login, membership, or
  authentication path.
- Analyze Tenant ownership, Company/Branch reuse, cross-Tenant isolation,
  identity/reference and localized-name needs, duplicate/match handling,
  lifecycle, authorization/resource scope, optimistic concurrency, audit, and
  history requirements only to the level supported by the approved sources.
- Separate Business Customer master data from AR, credit, payment terms,
  tax, invoicing, Finance, Sales, and other downstream ownership. Record
  unresolved dependencies rather than implementing them.
- Identify any Saudi statutory/legal/tax dependency and leave its external
  validation and production gate under the owning issue, including MESP-49.
- Produce a bounded decision bundle and an implementation-ready/non-ready
  verdict. A readiness verdict must not be presented as working production
  capability.

## Hard exclusions

- No Business Customer/Product/Item/SKU/Barcode/Category/UOM/Supplier source
  changes; no unified Party or variant entity by implication.
- No customer login, credentials, user account, Tenant membership, consumer
  session, external-party portal, or authentication identity.
- No Procurement/PO/receipt/invoice, AR/AP, Finance, Tax, credit, payment,
  banking, settlement, Sales, Inventory, Price List, Currency, or Exchange
  Rate behavior.
- No migration, SQL/provider configuration, database creation, production
  provisioning, legal certification, or fabricated external validation.
- No automatic activation of a missing or unrelated Jira item and no automatic
  start of a later slice.

## Required validation and handoff

- Review the complete documentation/Jira diff for scope creep, accidental
  decision inheritance, Tenant-isolation ambiguity, authentication ambiguity,
  legal/tax overclaiming, and accidental source changes.
- Run the relevant documentation checks and `git diff --check`; verify that
  Supplier source files and the merged `main` baseline are unchanged by this
  readiness session.
- Update every genuinely affected Markdown state/plan file, `.ai/CURRENT_STATE.md`,
  `docs/staticts.md` only if production progress or Jira state materially
  changes, Jira evidence if a dedicated item exists, and this TASK.md to the
  next exact bounded session.
- Commit and push the documentation/readiness work, merge only when the
  authorized dedicated item and review gates are satisfied, synchronize local
  `main`, and stop for ChatGPT review. Do not execute the next root TASK.md in
  this session.

## Stop conditions

Stop and report a blocker on unresolved Business Customer ownership or scope,
Tenant-isolation or authorization weakness, credential/login ambiguity,
accounting or data-integrity risk, destructive migration/data loss, required
Saudi legal/external validation, missing authorization for a Jira item, or a
material scope/architecture change. Keep MESP-48, MESP-49, and MESP-50 open
unless their own separately authorized gates are satisfied.

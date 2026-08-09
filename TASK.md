# Next session - M95-SL-05 Owner disposition review and implementation-item activation only

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

The bounded M95-SL-05 Business Customer readiness analysis is now recorded in
`docs/20_Business_Customer_M95_SL_05_Readiness.md`. Live Jira MESP-105 is the
dedicated item under MESP-6, is In Progress, and has activation evidence in
comment `10688`. Draft PR #40 carries the docs-only handoff. MESP-106 is a
separate To Do, non-blocking shared hardening follow-up. The Customer-specific
MD-OD-001, MD-OD-005, and MD-OD-008 decisions remain one unresolved Owner
bundle. No Customer source behavior was added.

The next exact bounded sequence position is Owner disposition review for that
single bundle. A fresh session must verify explicit Owner evidence first. If
the bundle remains unresolved, keep MESP-105 In Progress and stop. If the Owner
has disposed all three Customer-scoped decisions, create and activate a
separate Business Customer implementation item under MESP-6 only after its
Definition of Ready is checked; do not implement Customer source behavior
automatically in the same handoff.

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

Review the completed M95-SL-05 readiness record and obtain or record one
explicit Customer-scoped Owner disposition for MD-OD-001, MD-OD-005, and
MD-OD-008. Re-read the approved BRD/LIS and the existing architecture,
glossary, ADR, decision-register, foundation, and delivery-plan baselines;
confirm that the decision bundle is not inherited from Supplier or Product;
and, only if all three decisions are explicitly disposed, establish the
separate implementation item and handoff boundary.

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
  `10687`, MESP-105 is the single active In Progress readiness item with
  activation evidence `10688`, and MESP-106 is To Do/non-blocking. Confirm no
  Business Customer implementation item is active. Do not activate an
  unrelated issue or infer Owner approval from Supplier/Product evidence.
- Confirm that no Business Customer Owner decisions have been silently
  inherited from Supplier, Product, or the global decision register. Keep the
  three-row decision bundle explicit and scoped to Business Customer; an
  analysis recommendation is not an Owner disposition.
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
  `docs/staticts.md` because the Jira state materially changed, Jira evidence
  on MESP-105/MESP-106, and this TASK.md to the next exact bounded session.
- Commit and push the documentation/readiness work, merge only when the
  authorized dedicated item and review gates are satisfied, synchronize local
  `main`, and stop for ChatGPT review. Do not execute the next root TASK.md in
  this session.

## Stop conditions

Stop and report a blocker on unresolved Business Customer ownership or scope,
Tenant-isolation or authorization weakness, credential/login ambiguity,
accounting or data-integrity risk, destructive migration/data loss, required
Saudi legal/external validation, missing authorization for a Jira item, or a
material scope/architecture change. If the three Customer decisions remain
unresolved, keep MESP-105 In Progress, do not create/activate the implementation
item, and stop with the one consolidated Owner bundle. Keep MESP-48, MESP-49,
and MESP-50 open unless their own separately authorized gates are satisfied.

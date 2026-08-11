# Next session - MESP-36 Reporting and Analytics BRD only

MESP-35 is **Done** as the bounded, documentation-only Release 1 B2B Sales
and Order-to-Cash business baseline. The canonical artifact is
docs/24_Sales_and_Order_to_Cash_BRD.md. Focused PR #51 merged cleanly to
main at 1daffde06106ab2f1b93ae1773ccd317ddc52089 from reviewed branch head
e5daa1048e9c54f34a23f613929a8832c6d8f8c5. Jira activation, validation,
Owner approval, MESP-23 handoff, final validation, and closure evidence are
comments 10762, 10763, 10764, 10765, 10766, and 10767.

The MESP-35 BRD explicitly preserves FIN-OD-09 / MESP-110 as To Do and
unapproved, preserves MESP-54 as a separate open Finance dependency, and
does not define Payment Term Release 1 shape or due-date mechanics,
fiscal-year/year-end accounting mechanics, or Finance posting-dimension
policy. The applicable MESP-23 register remains open except the exact
approved MESP-52 / PD-020 and MESP-56 / PD-021 scopes. No source or
production behavior was added.

MESP-36 is the next exact separately authorized BRD task. It remains **To Do**
and must not be activated automatically. MESP-37, Currency, implementation
work, and all later tasks remain unstarted.

## Exact objective

Execute only MESP-36 - Produce the Release 1 Reporting and Analytics
business-requirements baseline. Cover operational dashboards and the report
catalogue across executive, Procurement, Inventory, B2B Sales, Finance,
audit, and SaaS administration, with data-freshness indicators and
reconciliation paths.

Use the live MESP-36 Jira description as the task-specific source of required
outputs. Its primary PRD anchors are RPT-001 through RPT-003, BR-012, and the
PRD section 19.2 minimum report catalogue. The BRD sequence position is 11 of
15.

Do not execute MESP-37, Currency, implementation, source, schema, migration,
API, UI, provider, database, infrastructure, production, Retail POS, or
Wafra-specific core work. Do not execute any next task automatically.

## Required entry evidence

Before activating MESP-36 in live Jira, read:

- AGENTS.md;
- .ai/CURRENT_STATE.md;
- this TASK.md;
- docs/staticts.md;
- the canonical approved PRD docs/MESP_PRD_v1.2.docx;
- docs/00_ERP_Business_Glossary.md;
- the approved Procurement, Inventory, Finance, and Sales BRDs;
- the Product Decision Register and the live MESP-23 register;
- Decisions.md and applicable ADR/index evidence; and
- docs/94_Product_Delivery_Master_Plan.md.

Reverify the live Jira sequence and gates immediately before activation:

- MESP-25 and MESP-26 are Done;
- MESP-34 Finance, MESP-109 Finance reconciliation, and MESP-35 Sales are
  Done, with their published evidence;
- MESP-36 is the only intended active task and is still To Do before
  activation;
- MESP-23 remains In Progress;
- FIN-OD-09 / MESP-110 remains To Do and unapproved;
- MESP-54 and all other applicable MESP-23 rows remain open except the exact
  MESP-52 / PD-020 and MESP-56 / PD-021 closures; and
- MESP-37, Currency, and later tasks remain To Do/unstarted.

Do not treat MESP-35 completion as approval of any Sales, Finance,
Inventory, Saudi, migration, integration, reporting, or exchange-rate
decision. Preserve the exact MESP-35 boundary and evidence.

## BRD coverage

Define business requirements without inventing unresolved policy for:

- report purpose, users, actors, responsibilities, triggers, preconditions,
  report ownership, catalogue, dashboards, KPIs, filters, drill-down,
  exports, subscriptions/notifications, and lifecycle;
- executive, Procurement, Inventory, B2B Sales, Finance, audit, platform
  administration, Tenant, Company, Branch, Warehouse, and operational views;
- source lineage to master data, quotes/orders/deliveries/invoices/receipts,
  returns/credits, purchase commitments/receipts/invoices/payments, stock
  ledger/valuation, AR/AP/GL/tax/cash, identity/access, audit, and
  administrative events;
- data freshness, posted-versus-pending-versus-unknown status, period and
  currency facts, reconciliation owner, correction/reversal, and immutable
  source history;
- permissions, server-derived Tenant and organizational scope, Company
  boundaries, separation of duties, delegated access, privacy, retention,
  export, attachment, and audit controls;
- partial, stale, duplicate, missing, denied, unauthorized, concurrent,
  delayed, retryable, dead-lettered, and unknown data outcomes;
- report validation, calculated measures, source snapshots, versioned
  definitions, effective dates, rounding and currency display boundaries,
  without resolving Finance policy;
- Inventory quantity and valuation facts, Finance accounting truth, source
  reconciliation, and no mutation of source transactions;
- migration and opening-state reporting boundaries under MESP-51;
- Saudi, tax, localization, privacy, legal, banking, and external-validation
  implications without statutory conclusions;
- authenticated integrations, imports/exports, observability, recovery,
  volume, retention, residency, backup, and production gates; and
- traceable Given/When/Then scenarios covering authorization, Tenant
  isolation, freshness, reconciliation, partials, denial, immutable
  financial history, Inventory/Finance sources, and downstream correction.

Release 1 remains B2B ERP only. Customers and suppliers remain external
business parties, not Users. Retail POS, cashier sessions, retail checkout,
store cash drawers, loyalty, promotions, and Wafra-specific core behavior
remain excluded.

## Decision discipline

Do not infer report catalogue, KPI formulas, reconciliation ownership,
currency/rate display, fiscal-year/year-end behavior, Payment Term, tax,
Saudi, retention, privacy, migration, integration, or permission policy from
common practice, source code, or recommended defaults. Keep each unresolved
decision visible with its owner and implementation or production gate.

FIN-OD-09 / MESP-110 remains open and unapproved. Do not define Payment Term
shape or due-date mechanics, fiscal-year/year-end accounting mechanics, or
Finance posting-dimension policy inside Reporting. Preserve MESP-54 and all
other applicable open MESP-23 decisions. Do not close Sales, Finance,
Inventory, Saudi, migration, or platform decisions merely because reports
consume their data.

## Required boundary

- Documentation, Jira, and governance only.
- No application source, EF entity, table, migration, endpoint, API
  contract, UI, provider, database, infrastructure, or automated-test
  behavior change.
- Do not execute a migration or provision production/external
  infrastructure.
- Do not resolve MESP-48, MESP-49, MESP-50, ADR-011, Finance policy,
  Inventory policy, MESP-54, FIN-OD-09 / MESP-110, or another domain's
  decision by implication.
- Do not activate or execute MESP-37, Currency, or any later task
  automatically.

## Required completion and handoff

Run focused documentation checks, inspect the complete task-related diff,
update every genuinely affected state/plan file, review and conservatively
update docs/staticts.md, and record exact Jira activation, validation, Owner
approval, MESP-23 handoff, and closure evidence. Publish the canonical
Reporting BRD through a focused review PR, merge only when clean and
unblocked, synchronize main, then update this TASK.md with the next exact
separately authorized task and stop for ChatGPT review.

This handoff is the end of the MESP-35 session. Do not execute MESP-36,
MESP-37, Currency, implementation, or any next task in this session.

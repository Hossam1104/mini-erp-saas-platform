# Next session — MESP-37 — Release 1 Saudi Localization BRD only

## Session boundary

This is the exact next executable session after the completed MESP-112 Release
1 Saudi scope rebaseline. Execute only the MESP-37 documentation-only BRD
boundary below. Do not start the next task automatically.

MESP-37 — Produce Saudi Localization and Compliance BRD is **To Do** and must
remain To Do until this fresh session verifies its entry evidence and the
normal activation decision is made. MESP-37 was not activated or executed by
MESP-112.

The canonical current scope overlay is
docs/27_Release_1_Saudi_Localization_Scope_Rebaseline.md.
The approved PRD and earlier Saudi-readiness artifact remain historical
evidence and must not be silently rewritten.

## Objective

Produce a bounded Release 1 Saudi Localization BRD for a reusable multi-Tenant
B2B ERP. The BRD must specify localization and configurable Saudi country-pack
behavior without implementing or specifying statutory, tax-certification,
government-integration, legal-compliance, privacy-regulatory, or production
infrastructure functionality.

Use the product position **Saudi-localized Core ERP Release 1** or **Saudi
localization baseline**. Do not call the product ZATCA/FATOORA/Saudi
statutory/tax/legal/PDPL compliant, certified, or government-integrated.

## Required entry reading and verification

Before changing anything, read completely:

1. AGENTS.md;
2. .ai/CURRENT_STATE.md;
3. this TASK.md;
4. docs/27_Release_1_Saudi_Localization_Scope_Rebaseline.md;
5. docs/26_Saudi_Regulatory_Evidence_and_External_Validation_Readiness.md;
6. docs/94_Product_Delivery_Master_Plan.md;
7. docs/staticts.md;
8. docs/MESP_PRD_v1.2.docx;
9. the Product Decision Register in Jira MESP-22;
10. docs/Decisions.md and applicable ADRs;
11. approved Procurement, Inventory, Finance, B2B Sales, and Reporting BRDs;
12. live Jira MESP-23, MESP-37, MESP-48, MESP-49, MESP-50, MESP-53,
    MESP-54, MESP-110, MESP-111, and MESP-112; and
13. the current Git branch, status, and relevant merged-main baseline.

Recheck current official evidence only if the BRD makes a future-boundary
reference that requires it. Do not use a source check to introduce statutory
scope into this BRD.

## In scope for the MESP-37 BRD

The BRD may define business requirements, ownership, boundary terms,
configuration facts, and Given/When/Then acceptance scenarios for:

- Arabic language support;
- English language support;
- RTL page structure, forms, tables, and visual usability;
- bilingual navigation, labels, validation messages, generic documents,
  reports, and exports where those are in core ERP scope;
- Saudi locale behavior and configurable defaults;
- SAR/default configuration for a Saudi-oriented country pack;
- configurable timezone and locale behavior, including Asia/Riyadh as a
  configurable Saudi-oriented default;
- reusable Saudi country-pack configuration and Tenant-safe ownership;
- cross-module localization impacts on Procurement, Inventory, Finance, Sales,
  Reporting, Platform, audit, configuration, and generic ERP documents;
- dates, numbers, currency presentation, search, sort, filtering, fallback,
  and mixed Arabic/English content as localization acceptance concerns;
- audit and configuration evidence;
- multi-Tenant configuration and server-derived authority;
- localization error/fallback and visual acceptance scenarios; and
- explicit future external-compliance/integration extension boundaries.

The BRD may preserve generic configurable ERP tax or accounting facts already
owned by Finance, but it must not select Saudi statutory treatment, tax rates,
tax returns, statutory invoice content, or a compliance claim.

## Explicitly out of scope and deferred

Do not specify or implement:

- ZATCA or FATOORA integration, clearance, reporting, onboarding, sandbox,
  credentials, signing keys, submission, XML, QR, security, certification,
  or taxpayer activation;
- statutory VAT automation or a Saudi tax engine;
- statutory e-invoicing or regulator submission;
- taxpayer phase, wave, obligation, or date logic;
- government, regulator, banking, commercial, or other production external
  integrations;
- legal-compliance automation;
- PDPL-specific regulatory rights/legal-basis workflow, DPO workflow,
  controller registration, TIA, SCC/BCR, regulator submission, or
  legal/privacy certification;
- Saudi tax, statutory, legal, or PDPL certification;
- provider, hosting, primary-data location, backup, DR, support geography,
  retention, deletion, purge, subprocessors, or production infrastructure
  decisions;
- Currency implementation or exchange-rate source policy;
- later Finance, Integration, Tax, Privacy, Infrastructure, Production,
  Master Data, or other tasks;
- Retail POS behavior; or
- Wafra-specific core behavior, customer forks, or hard-coded customer rules.

Name deferred areas only as future external-compliance/integration boundaries.
Do not infer a legal conclusion or taxpayer applicability from the scope
exclusion.

## Required BRD outputs

The fresh MESP-37 session must produce or update only the bounded BRD and
genuinely affected governance evidence:

- an explicit status, purpose, owner, and source baseline;
- localization in-scope and out-of-scope matrices;
- Arabic/English/RTL and bilingual document/report requirements;
- locale, timezone, SAR, country-pack, and Tenant configuration boundaries;
- cross-module ownership and dependency table;
- audit/configuration and server-authority requirements;
- localization Given/When/Then acceptance catalogue;
- generic document/report/export/fallback/mixed-content scenarios;
- non-claims and future external-compliance/integration boundaries;
- MESP-23 decision traceability without closing unrelated open rows; and
- clear future implementation/test handoff without creating source tasks.

Do not silently change the approved PRD or close MESP-54, MESP-48, MESP-50,
MESP-53, or MESP-110.

## Jira and activation discipline

Use the existing MESP-37 Jira item. Do not create a duplicate. Move only the
authorized MESP-37 item through its lifecycle. Keep MESP-49 Done only for its
R1 deferred disposition, keep MESP-50 open, and preserve MESP-23 as the living
register. Do not activate a Currency, Integration, Tax, Privacy, Production,
or implementation item.

Record owner approval and any open decision in the Product Decision Register
before treating a recommendation as a requirement. Preserve the MESP-112
scope overlay and closure evidence as the entry boundary.

## Validation and completion

Before completion:

- inspect every changed file and the complete base-to-final diff;
- run git diff --check;
- verify that only documentation, TASK/state/plan/statistics, and Jira
  governance changed;
- verify no source, tests, entities, tables, EF models, migrations, APIs,
  UI, providers, credentials, integrations, tax, ZATCA/FATOORA, privacy/legal
  workflows, production configuration, or Wafra behavior changed;
- verify MESP-37 status and all affected Jira evidence live;
- preserve historical PRD wording and traceability;
- update docs/staticts.md conservatively without increasing production
  percentages for documentation alone;
- update .ai/CURRENT_STATE.md, the Product Delivery Master Plan, and every
  genuinely affected Markdown state file;
- update this TASK.md with the next exact separately authorized session;
- publish a focused PR, review it, and merge only if clean and unblocked; and
- verify main is synchronized, the worktree is clean, and stop for ChatGPT
  review.

Do not begin another task in this session. Do not implement MESP-37. This
prompt is the next session boundary only.

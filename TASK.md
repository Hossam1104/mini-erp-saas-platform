# Next session - MESP-37 Saudi Localization and Compliance BRD only

MESP-36 is **Done** as the bounded, documentation-only Release 1 B2B
Reporting and Analytics business baseline. The canonical artifact is
`docs/25_Reporting_and_Analytics_BRD.md`, v0.1 Approved Business Baseline.
Focused PR #52 merged cleanly to `main` at
`cd3ad20876a0569245ccc6e1ff677315dfcc1a2a` from reviewed head
`7022b24dc1c9ba6d02f9b77e0038b3e9b6211eeb`. Jira activation, validation,
Owner approval, final audit, MESP-23 handoff, and closure evidence are
comments `10769`, `10770`, `10771`, `10772`/`10773`, `10774`, and `10775`.

The Reporting BRD preserves MESP-53 as the critical open Reporting dependency
for the final report catalogue, figure/KPI definitions, named business and
reconciliation ownership, and scheduled/distribution policy. MESP-54 remains
To Do and unapproved for currency and exchange-rate policy. FIN-OD-09 /
MESP-110 remains To Do and unapproved for fiscal-year/year-end, Payment Term,
aging, and Finance posting-dimension policy. MESP-23 remains In Progress;
Currency remains unexecuted. No source, production, transactional, stock,
subledger, GL, or reporting mutation behavior was added.

MESP-37 is the next exact separately authorized BRD task. It is **To Do** and
must not be activated automatically. Do not execute Currency, implementation
work, or any later task in this session.

## Exact objective

Execute only MESP-37 - Produce the Release 1 Saudi Localization and Compliance
business-requirements baseline. Cover Arabic and English support, RTL
behavior, SAR defaults, VAT, Saudi invoice requirements, FATOORA readiness,
the Saudi PDPL privacy baseline, data residency, and country-pack controls.

Use the live MESP-37 Jira description and the approved PRD as the task-specific
source of required outputs. Its primary anchors are KSA-001 through KSA-008,
BR-002, and the PRD v1.2 Appendix B official Saudi reference baseline. The
BRD sequence position is 12 of 15.

The session must remain business-requirements and governance work. Tax rates
may be described only as configuration with effective dates and historical
reproduction of the applied rule. Final legal, tax, privacy, invoice, and
data-residency positions require validation by qualified Saudi advisors and
the designated business owner. Official ZATCA and SDAIA publications must be
re-checked before design freeze and before each production launch.

Do not execute MESP-36 again, Currency, MESP-38 or any later task,
implementation, source, schema, migration, API, UI, provider, database,
infrastructure, production, Retail POS, or Wafra-specific core work. Do not
execute any next task automatically.

## Required entry evidence

Before activating MESP-37 in live Jira, read:

- `AGENTS.md`;
- `.ai/CURRENT_STATE.md`;
- this `TASK.md`;
- `docs/staticts.md`;
- the canonical approved PRD `docs/MESP_PRD_v1.2.docx`;
- `docs/00_ERP_Business_Glossary.md`;
- the approved Procurement, Inventory, Finance, Sales, and Reporting BRDs;
- the Product Decision Register and the live MESP-23 register;
- `Decisions.md` and applicable ADR/index evidence; and
- `docs/94_Product_Delivery_Master_Plan.md`.

Reverify the complete live sequence and dependency gate immediately before
activation:

- MESP-35 is **Done**;
- MESP-109 is **Done** with its accepted non-blocking-findings evidence;
- MESP-36 Reporting and Analytics is **Done** with PR #52 and closure
  evidence above;
- MESP-37 is **To Do** before activation;
- MESP-23 remains **In Progress**;
- MESP-53 remains **To Do and unapproved** and is the critical open Reporting
  dependency;
- MESP-54 remains **To Do and unapproved**;
- FIN-OD-09 / MESP-110 remains **To Do and unapproved**;
- Currency remains unexecuted and MESP-37 must not activate it; and
- MESP-38 and later work remain To Do/unstarted.

Do not treat completion of MESP-36 or any prior BRD as approval of Saudi,
tax, privacy, residency, invoice, currency, exchange-rate, Reporting,
Finance, Inventory, Procurement, Sales, migration, integration, or production
policy.

## Required BRD coverage

Cover the live MESP-37 required outputs:

- business purpose;
- actors and responsibilities;
- triggers and preconditions;
- main, alternative, and exception paths;
- business rules;
- document lifecycle and status transitions;
- data and validation requirements;
- permissions, approval controls, and separation of duties;
- Inventory, accounting, and multi-currency impacts;
- Saudi localization impact;
- reports and KPIs without inventing final policy;
- audit evidence;
- authenticated integration requirements;
- migration requirements;
- traceable Given/When/Then acceptance scenarios;
- visible open decisions and external-validation gates; and
- bounded business-owner approval evidence.

Preserve the approved Procurement, Inventory, Finance, B2B Sales, Reporting,
Platform Administration, Tenant, Company, Branch, and external-business-party
ownership boundaries. Saudi Localization must not become a second source of
transactional, stock, subledger, GL, or reporting truth and must not mutate
source data.

## Decision discipline

Do not silently define or approve the final Saudi statutory catalogue,
compliance conclusions, tax/legal/privacy/residency positions, invoice or
FATOORA obligations, Reporting Currency, exchange-rate or rounding policy,
Payment Term or aging mechanics, fiscal-year/year-end accounting mechanics,
Finance posting dimensions, final report catalogue, KPI formulas, named
reconciliation ownership, scheduled report/export policy, or any other open
MESP-23 decision. Keep each unresolved decision visible with its owner,
evidence requirement, and approval/production gate.

MESP-53, MESP-54, and FIN-OD-09 / MESP-110 must remain open unless genuine
Owner or qualified external evidence closes a specific decision. Preserve
MESP-48, MESP-49, MESP-50, and all other security, privacy, residency,
retention, legal-hold, purge, backup, restoration, volume, provider, and
production gates.

## Required boundary

- Documentation, Jira, and governance only.
- No application source, EF entity, table, migration, endpoint, API contract,
  UI, provider, database, infrastructure, automated-test behavior, or
  production configuration change.
- No migration execution or external/production infrastructure provisioning.
- Release 1 remains B2B ERP only; Retail POS and Wafra-specific core behavior
  remain excluded.
- Do not activate or execute Currency, implementation, MESP-38, or any later
  task automatically.

## Required completion and handoff

Run focused documentation and scope checks, inspect the complete task-related
diff, update every genuinely affected state/plan file, conservatively update
`docs/staticts.md`, and record exact Jira activation, validation, Owner
approval, MESP-23 handoff, final review, and closure evidence. Publish the
canonical Saudi BRD through a focused review PR, merge only when clean and
unblocked, synchronize `main`, then update this `TASK.md` with the next exact
separately authorized task and stop for ChatGPT review.

This handoff is the end of the MESP-36 session. Do not execute MESP-37,
Currency, implementation, or any next task in this session.

# Current State

Last reconciled: 2026-08-30

## Authoritative status

MESP-137 is accepted, merged, and Jira-closed through PR #84 at
`6b3aeb63da15253dee5466f7be001773b80c28ad` from accepted feature head
`9406e8c6408251323b96d4a0c25082142546b9ef`. The current implementation
branch for this checkpoint is `chore/project-health-reconciliation-cleanup`,
created from verified `origin/main` at
`c8c9084d2cf72550e7a51e4ab9475ef54d14e864`. The final checkpoint head is
recorded in `TASK.md` after the final validation commit.

MESP-144 is the project-health checkpoint and remains In Progress pending
GPT-5.6 Sol acceptance. No implementation capability is active. MESP-138 and
MESP-139 remain To Do/inactive. The latest default branch is `main` at
`4d6e33189a3835d5d8d2a58736055a837a3f5bc9` through PR #85. Fast-track
completion is 21/26 (80.8%); production readiness remains approximately 47%
overall and 41% Procurement/P2P.

## Accepted functional boundary

The repository contains the reusable Tenant/Company/Branch-scoped ERP spine:
Master Data and Business Parties; Procurement sourcing, quotations, purchase
orders, confirmations, receipts, returns, invoice handoffs and matching;
Inventory ledger, controls, transfers, reservations, and valuation; Finance
posting, periods, AP/AR/cash settlement, tax/FX, close, reconciliation and
core reports; MESP-136 B2B quotations and Sales Orders, and MESP-137
Sales-linked reservation, partial fulfillment, Delivery, Finance-owned
invoice-eligibility/AR handoff seams, durable evidence, and bilingual Angular
fulfillment surfaces.

ADR-019 Tenant-aware host entry, exact membership authorization,
Overview-first routing, operational Company/Branch context, generic branding,
and presentation-only SAR semantics are the current architecture direction.
Wafra remains validation/customer context only; no Wafra-specific core branch
exists.

## Inactive work and production gates

MESP-138 (Customer Return/Credit Note/receipts/correction), MESP-139 (generic
Reporting), and later work remain To Do/inactive. MESP-48 and MESP-50 remain open.
External
providers, statutory submission, production DNS/TLS, residency/retention,
backup/recovery, capacity, migration/cutover, legal, UAT, and specialist
acceptance gates remain separate from functional completion.

Current progress is 21/26 fast-track capabilities (80.8%), approximately 47%
overall production readiness, and approximately 41% Procurement/P2P readiness.
These figures are not interchangeable.

## Architecture and repository health

Backend dependency direction is `Api -> App -> Contracts`, with Infrastructure
implementing application persistence ports and the API composing the modules.
There are six module-owned EF persistence contexts plus the shared tenancy
context, additive migrations, a four-project production solution, one test
project, and a separate development cutover tool. Angular uses standalone
lazy feature routes and a shared API/session/localization shell.

The architecture audit found cohesive large files, migration/snapshot history,
public contracts, dynamic registrations, and test utilities rather than
proven dead code. No production class, method, endpoint, dependency,
migration, route, localization key, or asset was removed. Issue-numbered
Finance names remain because they are internal traceability seams and a rename
would be cosmetic churn without a semantic gain in this checkpoint.

## Validation baseline and known warning

The final Release build passed with 0 warnings and 0 errors. The full
disposable-LocalDB backend suite passed 1,124/1,124 with 0 failures and 0
skips, including SQL Server safety 80/80; the focused Sales suite passed
26/26. The combined REST/OpenAPI/host/security filter passed 217/217, EF
pending-model checks were clean for all seven contexts, Angular unit tests
passed 305/305, and Chromium browser tests passed 49/49. Both npm audits
reported 0 vulnerabilities and all six NuGet scans were clear. The Angular
production build retains the existing 510.08 kB initial-bundle budget warning
(10.08 kB over 500 kB); the budget was not raised. The one baseline
date-dependent Sales fixture failure was corrected in the test fake so the
requested effective date is used consistently; no production behavior changed.

## Next decision

Sol reviews the final Draft PR and MESP-144 evidence. Until acceptance, do not
activate MESP-137, merge the PR, change production-gate statuses, or mutate
protected assets or persistent databases.

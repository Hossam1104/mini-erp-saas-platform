<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="frontend/assets/Logo_16_9_BG_Removed_Dark.png">
    <img src="frontend/assets/Logo_16_9_BG_Removed.png" alt="Mini ERP SaaS Platform" width="420">
  </picture>
</p>

<p align="center"><strong>A reusable bilingual, multi-tenant B2B ERP foundation for Saudi SMEs.</strong></p>

MESP is a generic SaaS ERP product. Tenant isolation, Company/Branch scope,
configuration-led behavior, server-authoritative authorization, module-owned
persistence, audit, and evidence are product rules. Wafra is a validation
tenant, not a code fork.

## Authoritative current status - 30 August 2026

MESP-137 is accepted, merged, and Jira-closed through PR #84. MESP-144 is the
active repository-health checkpoint and remains In Progress on Draft PR #82.
No feature implementation capability is active; MESP-138 and MESP-139 remain
To Do/inactive. Fast-track progress is 21/26 (80.8%); production readiness
remains approximately 47% overall and 41% Procurement/P2P.

## Current status — 29 August 2026

> The dated status heading below is retained as a historical snapshot; use the
> authoritative current status section above for live state.

MESP-137 is accepted, merged, and Jira-closed through PR #84. The accepted
implementation boundary includes the Procurement, Inventory, Finance,
Tenant-aware entry, MESP-136 B2B Sales, and MESP-137 reservation/fulfillment
surfaces already present in `main`. The current repository health checkpoint is
tracked as Jira MESP-144 on a dedicated Draft-PR branch.

MESP-138, MESP-139, and later capabilities are inactive. MESP-48 and
MESP-50 remain open production-readiness gates. Fast-track progress is 21/26
(80.8%); production readiness remains approximately 47% overall and 41%
Procurement/P2P. Functional completion does not equal production readiness.

## Capability boundary

| Area | Present boundary |
| --- | --- |
| Platform | Tenant-aware host entry, exact membership authorization, Overview-first routing, operational Company/Branch context, generic branding, presentational SAR |
| Master Data | Product, category, UOM, price list, tax, currency, exchange-rate, payment-term and import foundations |
| Procurement | Purchase Requests, quotations/source decisions, Purchase Orders/confirmations, Goods Receipts, Supplier Returns, invoice handoffs and matching seams |
| Inventory | Warehouse context, ledger, opening balances, reservations, transfers, stock controls and valuation evidence |
| Finance | Journals/GL, fiscal controls, AP/AR/cash settlement, tax/FX, close, reconciliation and core reports |
| Sales | MESP-136, MESP-137 quotations/orders plus reservation, partial fulfillment, Delivery, and Finance-owned invoice eligibility seams |
| Not included | Customer Return/Credit Note, receipts/refunds, generic Reporting catalogue, external/statutory providers, production infrastructure, and Wafra-specific core behavior |

## Architecture

The backend solution has four production projects:

```text
MiniErp.Api            HTTP endpoints, auth composition, OpenAPI
  -> MiniErp.App       module application services and persistence ports
     -> MiniErp.Contracts public request/response and shared contracts
MiniErp.Infrastructure  module-owned EF contexts, migrations, persistence
  -> MiniErp.App / Contracts
```

The test project is `backend/tests/MiniErp.ArchitectureTests`. The separate
`MiniErp.DevelopmentDataCutover` tool is not part of the production solution
and is retained for the bounded local SQLite-to-SQL Server workflow.

Frontend feature routes are standalone and lazy-loaded. The shell preserves
server-owned session/context state, EN/AR localization, RTL/LTR direction,
safe blocked/error states, and relative API requests through the generated
development proxy.

## Technology

- .NET 10 and ASP.NET Core minimal APIs
- EF Core with SQL Server as the formal provider and SQLite for local/test fallback
- Angular 22.1, TypeScript 6, RxJS, Vitest, and Playwright
- Six module-owned EF contexts plus tenancy persistence and additive migrations

## Local quick start

From the repository root:

```powershell
dotnet build .\backend\MiniErp.sln --configuration Release
.\scripts\Start-MiniErpDevelopment.ps1 -Restart
```

The launcher selects an available API port, writes an ignored proxy for that
exact target, starts the Angular shell, and prints the URLs. The usual local
inspection mapping is API `5300` and frontend `4300`; the default API target
is `5000` when available. See [Run.md](Run.md) for authentication, SQL,
migrations, cookies, and manual two-process details.

For a local QA shortcut, explicitly set the Development-only loopback bypass;
it authenticates only the server-configured Development actor and never
accepts a browser-supplied Tenant, role, permission, or identity:

```powershell
[Environment]::SetEnvironmentVariable('MESP_DEV_AUTH_BYPASS', 'true', 'User')
[Environment]::SetEnvironmentVariable('MESP_DEV_TENANT_DISPLAY_NAME', 'Wafra', 'User')
```

Disable it when testing normal credential behavior.

## Validation

```powershell
dotnet build .\backend\MiniErp.sln --configuration Release --no-restore
.\scripts\Test-MiniErpBackend.ps1 -NoBuild
Set-Location .\frontend
npm test -- --watch=false --no-progress
npm run build
npm run test:e2e -- --project=chromium
npm audit
npm audit --omit=dev
```

The backend runner uses a disposable `MiniErpFoundation_*` LocalDB target for
SQL safety and restores the safety environment variable. Never substitute the
persistent `MESP` database. `git diff --check` and the EF pending-model check
are part of the handoff gate.

The current Angular build passes but retains the existing initial bundle
warning: 510.08 kB against a 500 kB budget. The budget is intentionally not
increased.

## Documentation map

- [TASK.md](TASK.md): current session boundary and Sol handoff
- [.ai/CURRENT_STATE.md](.ai/CURRENT_STATE.md): concise project truth
- [docs/staticts.md](docs/staticts.md): tracked progress/readiness source of truth
- [Run.md](Run.md): local runtime and database guide
- [backend/README.md](backend/README.md): backend architecture and persistence
- [frontend/README.md](frontend/README.md): Angular shell and route boundary
- `docs/ADR-*.md`: durable architectural decisions
- `docs/94_Product_Delivery_Master_Plan.md`: approved delivery planning record

Approved BRDs, specifications, ADRs, decision registers, validation evidence,
migration history, and the ten explicitly deprecated placeholder pointers are
not disposable cleanup artifacts.

## Production disclaimer

This repository is not a production-readiness claim. External providers,
statutory submission, DNS/TLS, residency/retention, backup/restore,
performance/capacity, migration/cutover, legal, UAT, and specialist gates
remain separately governed.

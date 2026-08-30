# Mini ERP Angular shell

## Current boundary

The Angular application is the first-party bilingual EN/AR, RTL/LTR shell for
the accepted Tenant-aware ERP boundary. MESP-136 Sales quotation and order
workspaces and MESP-137 fulfillment, Delivery, and invoice-eligibility surfaces
are present. MESP-138 and later capabilities remain inactive.
The frontend does not invent Tenant, Company/Branch, pricing, tax, FX,
approval, credit, accounting, inventory, or lifecycle authority.

## Routes

The shell uses standalone lazy feature routes under `/app`:

- `/app` and `/app/overview` — authenticated Overview-first entry
- `/app/workspaces` — compatible operational context surface
- `/app/master-data`, `/app/price-lists` — reference data
- `/app/procurement/...` — requests, quotations, orders, receipts, returns, handoffs and matching
- `/app/inventory` and `/app/inventory/valuation` — warehouse and valuation
- `/app/finance` — Finance, AP/AR/settlement, tax/FX, close and reports
- `/app/sales/quotations` and `/app/sales/orders` — MESP-136 Sales

The router uses relative API requests through the generated Development proxy.
It preserves safe loading, empty, blocked, authorization, concurrency, and
server-error states rather than treating a client-side form as authority.

## Local development

From this directory:

```powershell
npm install
npm start
```
The normal repository launcher is preferred because it coordinates the API
target and generated proxy. See [Run.md](../Run.md) for the canonical startup,
auth bypass, cookie recovery, host-boundary checks, and two-process fallback.

## Validation

```powershell
npm test -- --watch=false --no-progress
npm run build
npm run test:e2e -- --project=chromium
npm audit
npm audit --omit=dev
```

Current unit evidence is 305/305 tests across 43 spec files. The production
build passes while retaining the existing initial bundle warning: 510.08 kB
against a 500 kB budget. Do not increase the budget to hide the warning.

Owner-managed source assets under `frontend/assets` are protected and were not
changed by the checkpoint.

## Structure

```text
src/app/core       API client, session, auth, language and server context
src/app/shared     reusable context/status/presentation controls
src/app/features   Overview, master data, procurement, inventory, finance, sales
e2e                 Playwright browser contracts and smoke journeys
```

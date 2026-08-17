# Mini ERP Tenant-Aware Shell

This Angular 22 application is the first-party Release 1 B2B ERP shell. It
contains the responsive application shell, EN/AR localization and RTL/LTR
direction switching, sign-in/session bootstrap, server-resolved Tenant entry,
Overview-first routing, post-Overview Company/Branch context switching,
configuration-led branding/currency presentation, antiforgery bootstrap, and
accessible safe states. The bounded MESP-124 Purchase Order workspace adds
server-provided source selection, list/create/edit/detail, approval/issue
actions, manual full/partial/rejected/no-response confirmation, supplier-change
reapproval, history, and audit views. The browser never stores an
authentication token or establishes Tenant authority.

## Local development

From this directory:

```powershell
npm install
npm start
```

The official repository launcher runs the shell at `http://localhost:4300/`
and calls the API through its generated same-origin proxy. In Development,
`localhost` is the common entry host, `tenant.localhost` is the generic Tenant
host, and `admin.localhost` is the platform-admin boundary. The proxy
preserves the browser Host so the API remains the entry authority; Angular
does not parse or authorize subdomains.

## Validation

```powershell
npm test -- --watch=false --no-progress
npm run build
npm run test:e2e -- --project=chromium
npm audit --omit=dev
```

The current bounded evidence is Angular **210/210 across 24 spec files**;
the production build is **492.02 kB initial** with a **72.78 kB Purchase Order
lazy chunk** and **91.94 kB Supplier Quotation lazy chunk**; Chromium coverage
is **15/15** across the existing shell/quotation journeys and seven deterministic
MESP-124 scenarios; `npm audit --omit=dev` reports zero vulnerabilities. The
Playwright checks are automated API-fixture/browser evidence, not a manual
interactive browser sign-off.

The Playwright smoke journey mocks the approved entry, session, and business
responses so it can verify Overview-first navigation, bounded compatibility
context management, RTL direction, and server-confirmed operations without
inventing business data.

## Structure

- `src/app/core` — API client, secure-cookie request policy, session and
  server-resolved entry/context state, safe errors, currency presentation, and
  language direction.
- `src/app/features` — sign-in, authenticated shell, Tenant Overview,
  compatibility context management, and implemented capability workspaces.
- `src/app/shared` — reusable status, Tenant, and operational-context controls.
- `e2e` — focused Playwright TypeScript smoke coverage.

Tenant schema/migrations, DNS/TLS provisioning, full Platform Administration,
Purchase Order downstream effects (Goods Receipt, stock, invoice, AP, payment,
accounting, and three-way matching), Inventory, Finance, B2B Sales, Retail POS,
Wafra-specific core behavior, production deployment, and external/statutory
country-pack behavior remain explicitly out of scope.

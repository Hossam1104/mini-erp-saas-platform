# Mini ERP Foundation Shell

This Angular 22 application is the approved MESP-63 Wave 1 first-party shell
for Release 1 B2B ERP. It contains the responsive application shell, EN/AR
localization and RTL/LTR direction switching, sign-in/session bootstrap,
server-confirmed context switching, antiforgery bootstrap, and accessible safe
states. The browser never stores an authentication token or establishes Tenant
authority.

## Local development

From this directory:

```powershell
npm install
npm start
```

The shell runs at `http://localhost:4200/` and calls the merged backend
foundation contracts under `/api/v1/auth/*`. Configure a same-origin reverse
proxy when connecting it to the local API; no production provider or database
is part of this slice.

## Validation

```powershell
npm test -- --watch=false
npm run build
npm run test:e2e
```

The Playwright smoke journey mocks only the approved foundation responses so it
can verify navigation, session bootstrap, RTL direction, and server-confirmed
context switching without inventing business data.

## Structure

- `src/app/core` — API client, secure-cookie request policy, session and context state, safe errors, and language direction.
- `src/app/features` — sign-in, authenticated shell, context selection, and the read-only foundation workspace landing view.
- `src/app/shared` — reusable status and context-switcher primitives.
- `e2e` — focused Playwright TypeScript Wave 1 smoke coverage.

Procurement, Inventory, Finance, B2B Sales, Retail POS, Wafra-specific core
behavior, production deployment, SQL migrations, durable audit providers, and
MESP-61/MESP-64 work remain explicitly out of scope.

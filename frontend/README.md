# Mini ERP Tenant-Aware Shell

<!-- MESP-131-CURRENT-START -->
> **Current MESP-131 workspace overlay â€” 23 August 2026; pending Sol acceptance.** The Inventory feature now includes lazy `/app/inventory/valuation` for valuation summary, explainable MWA history, Pending/Blocked states, Inventory reconciliation, In-Transit value, Finance handoff facts, correction history and authorized CSV export. The surface uses server-owned Warehouse context, preserves EN/AR and RTL, never accepts browser-authored cost/FX authority, and keeps `frontend/assets` untouched.
>
> **MESP-131 executor-reported frontend evidence.** Angular `254/254` across 35 spec files; production initial total `499.94 kB` with valuation lazy chunk `35.96 kB`; focused MESP-131 Chromium `5/5`; full Chromium `32/32`; both npm audits report 0 vulnerabilities. Draft PR #75 remains open/Draft/unmerged pending Sol acceptance. Sol acceptance comments are `11788` and `11789`; no Jira writes were performed.
<!-- MESP-131-CURRENT-END -->

<!-- MESP-132-CURRENT-START -->
> **MESP-132 Finance workspace - 24 August 2026.** The shell now lazy-loads
> `/app/finance` with server-populated Company context and bounded tabs for
> Chart of Accounts, Fiscal Periods, Journals, Posting Rules, Inventory
> Finance Handoff, and GL inquiry. Manual journal UX shows debit, credit, and
> difference while the backend remains authoritative for balance, period,
> account, dimension, FX, mapping, authorization, and source uniqueness.
> The new surface preserves EN/AR, RTL/LTR, safe errors, responsive forms,
> accessible labels, and no raw GUID entry.
>
> **MESP-132 validation.** Angular passes `258/258` across 37 spec files;
> production initial total is `496.34 kB`, Finance lazy chunk is `36.60 kB`,
> focused Finance Chromium is `2/2`, full Chromium is `34/34`, and both npm
> audits report `0 vulnerabilities`. The implementation branch is
> `feat/MESP-132-finance-foundation`, implementation commit `af86b78` from
> exact base `fcec241dfedb529fef89d4336adf1e571917c52a`.
<!-- MESP-132-CURRENT-END -->

> **Historical MESP-129 workspace overlay - 22 August 2026.** The Inventory
> workspace now exposes server-authorized Warehouse Transfers with direct and
> two-step/InTransit flows, partial receipt, shortage/loss and overage-safe
> states, immutable history evidence, and human-readable Warehouse/Product/UOM
> facts. Goods Receipt lines expose the authoritative Inventory post action;
> Supplier Return exposes a real physical-post action only in the
> `AwaitingInventory` state; and Customer Return remains a truthful blocked
> state awaiting an authoritative Sales handoff. The UI preserves EN/AR,
> RTL/LTR, keyboard labels, responsive safe errors, and no client Tenant
> authority. No arbitrary customer-return form or MESP-130/MESP-131,
> commercial Sales, Finance, or Wafra-specific behavior is included.

> **MESP-129 validation.** Angular passes **241/241 across 32 spec files**;
> production build is **499.97 kB initial total** with a **33.12 kB Inventory
> lazy chunk**; full Chromium is **26/26**; both npm audit --omit=dev and full
> npm audit report **0 vulnerabilities**; and `frontend/assets` is untouched.
> Code-complete commit is `01ea8f7369d173c15cf55a723d6bd95006208282`; Draft PR
> **#73** remains open/Draft/unmerged.

> **Historical MESP-128 workspace overlay - 21 August 2026.** The shell now
> includes a lazy Inventory workspace at /app/inventory with server-provided
> Warehouse scope, human-readable Product/UOM selection, six distinct stock
> facts, append-only ledger visibility, opening-balance create/validate/post/
> correction controls, and partial reservation create/reduce/release controls.
> It preserves EN/AR RTL/LTR, accessible labels, responsive layout, safe server
> errors, and no client-supplied Tenant authority. Expected/Damaged/InTransit
> display the truthful zero state because those later posting workflows remain
> out of scope.

> **MESP-128 validation.** Angular passes **241/241 across 32 spec files**;
> production build is **499.97 kB initial total** with a **25.82 kB Inventory
> lazy chunk**; focused Inventory Chromium coverage is **2/2** and full
> Chromium is **26/26**; both npm audit --omit=dev and full npm audit report
> **0 vulnerabilities**.

> **Historical MESP-127 workspace overlay - 21 August 2026.** The Procurement
> shell now includes a lazy Supplier Returns workspace at
> `/app/procurement/supplier-returns`, `/new`, and `/:id`. It provides a
> server-derived accepted-Goods-Receipt source selector, remaining-return
> quantity display, source lineage, return reason/condition/commercial outcome,
> private evidence-reference capture, correction/reversal-aware detail
> evidence, Inventory handoff and Finance correction-reference states, history,
> audit, operational report metrics, visible safe error states, EN/AR RTL/LTR,
> responsive layout, keyboard-labelled controls, and reduced-motion styling.
> The workspace explicitly distinguishes Procurement evidence from authoritative
> Inventory stock movement and Finance/AP posting.

This Angular 22 application is the first-party Release 1 B2B ERP shell. It
contains the responsive application shell, EN/AR localization and RTL/LTR
direction switching, sign-in/session bootstrap, server-resolved Tenant entry,
Overview-first routing, post-Overview Company/Branch context switching,
configuration-led branding/currency presentation, antiforgery bootstrap, and
accessible safe states.

The Procurement workspaces include:
- **Purchase Requests & Approvals**: request creation, submission, multi-stage approval, and cancellation.
- **Supplier Quotations & Source Decisions**: quotation capture, side-by-side comparison, and single-source decision.
- **Purchase Orders & Confirmations**: server-provided source selection, list/create/edit/detail, approval/issue actions, manual full/partial/rejected/no-response confirmation, supplier-change reapproval, history, and audit views.
- **Goods Receipts**: receipt creation from Confirmed POs, authorized warehouse selection, physical partition (`Received = Accepted + Rejected`), descriptive damage overlay (`Damaged <= Received`), commercial remainder tracking, receipt cancellation, and history/audit tabs.
- **Purchase Invoice Handoffs**: handoff creation from accepted Goods Receipt lines, pro-rata tax allocation preview, un-invoiced remainder tracking, supplier invoice reference & date capture, handoff cancellation, and history/audit tabs.
- **Supplier Returns**: accepted Goods Receipt source selection, remaining return quantity, correction/reversal lineage, private evidence references, Inventory handoff evidence, Finance correction/credit references, operational reporting, and history/audit views.
- **Three-way Matching**: independent supplier invoice evidence, PO/accepted-receipt/invoice lineage, exact-safe and configured tolerance outcomes, variance evidence, controlled exception resolution, and bilingual history/audit views.

Cross-currency matching exposes only a server-owned Exchange Rate identity in
the request model. The server derives the effective version exclusively from
immutable supplier-invoice-date evidence. Rate, scale, version, pair, effective
window, effective date, and provenance are returned as the immutable
MESP-120-backed evaluation snapshot; the browser cannot author FX facts. The
workspace shows a human-readable selector only for active, pair-compatible
identities with a valid invoice-date version; it shows no raw GUID or editable
rate/scale/version/effective-date controls. Same-currency matching continues
to evaluate without an FX reference.

Matching keeps the existing exact server-derived Company/Branch scope. It does
not invent Company-to-Branch policy inheritance or allow a browser-selected
scope to widen the evidence boundary.

The browser never stores an authentication token or establishes Tenant authority.

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
npm audit
```

Current MESP-131 implementation evidence pending Sol acceptance:

- Angular unit tests: **254/254 across 35 spec files**.
- Production initial bundle: **499.94 kB**, still within the existing 500 kB budget.
- Valuation lazy chunk: **35.96 kB**.
- Focused MESP-131 Chromium: **5/5**.
- Full Chromium suite: **32/32**.
- Both npm audits: **0 vulnerabilities**.
- `frontend/assets`: **untouched**.

Current MESP-132 implementation evidence is recorded above and in
`docs/35_MESP-132_Finance_Foundation_Architecture.md`. The Finance route is
implemented but remains subject to Sol acceptance of the Draft PR; it is not
a production-readiness claim.

The Playwright checks are automated API-fixture/browser evidence, not a manual interactive production sign-off.

Terminal Cancelled/Rejected Purchase Order detail communicates the bounded
recovery rule in English and Arabic: the source decision is consumed and
continuation requires a new sourcing decision. Controlled same-Purchase-Order
reopening remains a future explicit capability/decision and is not implied by
the UI.

The Purchase Order workspace's `formatMoney` reuses the MESP-123 Supplier
Quotation non-ISO-currency safe-fallback pattern: standard ISO currency codes
render with `Intl.NumberFormat`'s currency style, while a valid MESP-configured
non-ISO code (e.g. a custom currency) falls back to a localized decimal render
suffixed with the raw currency code instead of throwing.

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
Purchase Order downstream effects (stock, invoice posting, AP, payment,
accounting), Inventory, AP/AR/cash/bank Finance follow-on, B2B Sales, Retail
POS, Wafra-specific core behavior, production deployment, generic Reporting,
and external/statutory country-pack behavior remain explicitly out of scope.

# Mini ERP Tenant-Aware Shell

<!-- MESP-135-CURRENT-START -->
> **MESP-135 Finance close, corrections, reconciliation and core reports - 26 August 2026.**
> MESP-134 is Done and merged into `main` at
> `1e49814172843c2ec2279b8dcc5fc0a41e5da372` (PR #78, closure `12122`).
> MESP-135 is the only active Finance capability under MESP-10,
> In Progress/activated by `12123`, with Finance reconciliation `12124`.
> The bounded branch is `feat/MESP-135-finance-close-reports` and must end in
> one Draft/Open/Unmerged PR for Sol acceptance.
>
> The Angular scope is lazy Finance close/year-end and reports workspaces with
> EN/AR, RTL-safe loading/empty/blocked/error/authorization states, server error
> mappings, filters, and bounded export for the Finance-owned reports. It
> reuses server-authoritative Tenant/Company, fiscal, posting, subledger, and
> monetary-evidence contracts. MESP-139 generic Reporting, scheduling,
> consolidation, statutory/provider work, and Wafra-specific behavior remain
> outside scope. No Jira, Opus, merge, or Ready transition is permitted.
<!-- MESP-135-CURRENT-END -->

<!-- MESP-134-HISTORICAL-START -->
> **MESP-134 Tax / FX / Reporting Currency / Revaluation HOLD 2 - 26 August 2026.**
> MESP-133 is accepted and merged at `3c616dd85b9cebb53990934321f1ae7d0d5410c9`.
> MESP-134 adds the lazy `/app/finance/tax-fx` workspace for monetary policy,
> exact Tax/FX/Reporting evidence, revaluation lifecycle, reconciliation, and
> blocked/evidence states. It remains bilingual EN/AR with RTL and
> server-authoritative for Company, Tax, Currency, Exchange Rate, posting, and
> evidence. HOLD 2 adds bilingual mappings for exact server error codes,
> including `unsupported_revaluation_scope`, while preserving EN/AR/RTL.
> Angular passes 283/283 across 39 spec files, focused Tax/FX is 9/9, and the
> production bundle is 496.44 kB initial with Finance/GL 34.52 kB, Tax/FX
> 40.38 kB, and settlement 56.04 kB lazy chunks. Draft PR #78 remains
> Open/Unmerged for Sol review. MESP-134 is the only active capability;
> MESP-135 remains inactive.
<!-- MESP-134-HISTORICAL-END -->

<!-- MESP-131-HISTORICAL-START -->
> **Current MESP-131 workspace overlay â€” 23 August 2026; pending Sol acceptance.** The Inventory feature now includes lazy `/app/inventory/valuation` for valuation summary, explainable MWA history, Pending/Blocked states, Inventory reconciliation, In-Transit value, Finance handoff facts, correction history and authorized CSV export. The surface uses server-owned Warehouse context, preserves EN/AR and RTL, never accepts browser-authored cost/FX authority, and keeps `frontend/assets` untouched.
>
> **MESP-131 executor-reported frontend evidence.** Angular `254/254` across 35 spec files; production initial total `499.94 kB` with valuation lazy chunk `35.96 kB`; focused MESP-131 Chromium `5/5`; full Chromium `32/32`; both npm audits report 0 vulnerabilities. Draft PR #75 remains open/Draft/unmerged pending Sol acceptance. Sol acceptance comments are `11788` and `11789`; no Jira writes were performed.
<!-- MESP-131-HISTORICAL-END -->

<!-- MESP-131-MERGED-CURRENT-START -->
> **Current MESP-131 merged-main workspace overlay — 24 August 2026.** PR #75
> is merged to `main`; MESP-131 and MESP-8 are Done in Jira. The Inventory
> valuation workspace remains the bounded EN/AR RTL surface for summary, MWA
> history, Pending/Blocked state, reconciliation, In-Transit value, Finance
> handoff facts, correction history, and authorized export. It uses server-owned
> Warehouse context and never accepts browser-authored cost or FX authority.
>
> **Accepted frontend evidence.** Angular `254/254` across 35 spec files;
> initial bundle `499.94 kB`; valuation lazy chunk `35.96 kB`; focused/full
> Chromium `5/5` and `32/32`; npm audits report 0 vulnerabilities; assets are
> untouched.
<!-- MESP-131-MERGED-CURRENT-END -->

<!-- MESP-133-HISTORICAL-START -->
> **MESP-133 Finance settlement workspaces verification-only HOLD 4 - 25 August 2026.** The shell
> keeps the lazy routes `/app/finance/ap`, `/app/finance/ar`, and
> `/app/finance/settlements`. The AP, AR, and settlement surfaces present
> server-derived Company scope, source lineage, outstanding/unapplied
> balances, approval and mapping failures, reversal constraints, and
> reconciliation status without claiming success for blocked backend states.
> The presentation remains bilingual EN/AR with RTL support; settlement
> source identity, direction, payment method, GL account, and evidence remain
> server-authoritative. No Wafra-specific branching or external provider UI
> was added.
>
> **MESP-133 validation.** Angular passes `274/274` across 38 spec files;
> production initial total is `496.44 kB`, Finance/GL lazy chunk is `34.31 kB`,
> settlement lazy chunk is `56.04 kB`, focused Angular workspace coverage is
> `15/15`, focused Finance Chromium is `6/6`, full
> Chromium is `38/38`, and both npm audits report `0 vulnerabilities`. PR #77
> remains Draft/unmerged for independent Sol review; MESP-132 is Done/merged.
> HOLD 4 added no frontend production code; the focused backend additions prove
> the real Procurement source-provider and historical recognition-rule boundary.
<!-- MESP-133-HISTORICAL-END -->

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

Accepted merged-main MESP-131 frontend evidence:

- Angular unit tests: **254/254 across 35 spec files**.
- Production initial bundle: **499.94 kB**, still within the existing 500 kB budget.
- Valuation lazy chunk: **35.96 kB**.
- Focused MESP-131 Chromium: **5/5**.
- Full Chromium suite: **32/32**.
- Both npm audits: **0 vulnerabilities**.
- `frontend/assets`: **untouched**.

Current MESP-132 implementation evidence is recorded above and in
`docs/35_MESP-132_Finance_Foundation_Architecture.md`. The Finance route is
implemented and merged, but it is not a production-readiness claim; Sol still
owns Jira closure and Finance Epic reconciliation.

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

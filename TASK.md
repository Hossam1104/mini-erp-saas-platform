# MINI ERP SAAS PLATFORM
# MESP-125 — GOODS RECEIPT + PURCHASE INVOICE HANDOFF
# BOUNDED PROCUREMENT & INVENTORY/FINANCE HANDOFF CAPABILITY
# IMPLEMENTATION TASK

Sole Executor:
Claude Sonnet 5

Reasoning:
HIGH

Mode:
IMPLEMENTATION

Feature Branch:
feat/MESP-125-goods-receipt-purchase-invoice-handoff

============================================================
0. ACTIVATION STATUS: ACTIVATED / IN PROGRESS
============================================================

Planned implementation capability:
MESP-125 — Implement Goods Receipt and Purchase Invoice handoff capability

Parent Epic:
MESP-7 — EPIC 07 - Procurement and Purchase-to-Pay

Jira Status:
IN PROGRESS (Activation comment `11503`)

Prerequisite Gates Verified:
- MESP-41 (Procurement approval policy): Done
- MESP-43 (Supplier quote evaluation): Done
- MESP-44 (Purchase order lifecycle & confirmation): Done
- MESP-45 (Goods receipt physical & tolerance baseline): Done
- MESP-113 (INV-OD-004 inventory valuation method): Done
- MESP-116 (Release 1 Consolidated Owner Decision Approval): Done (Owner approval comment `10957`)
- FIN-OD-01 / PD-046: APPROVED CONTRACT-BOUND (`docs/31_Release_1_Consolidated_Owner_Decision_Pack.md` §B6 / MESP-22 comment `10958`)

Executor Instruction:
You are Claude Sonnet 5 (Reasoning: HIGH).
Implement MESP-125 on branch `feat/MESP-125-goods-receipt-purchase-invoice-handoff` created from synchronized `main`.
Do NOT write or update Jira (GPT-5.6 Sol owns Jira management).

============================================================
1. MANDATORY ARCHITECTURE INPUTS
============================================================

Read completely before starting implementation:
1. `AGENTS.md` (Repository Working Agreement & Architecture Rules)
2. `CLAUDE.md` (Execution overlay & asset protection)
3. `.ai/CURRENT_STATE.md` (Current authoritative baseline & merged capabilities)
4. `docs/21_Procurement_and_Purchase_to_Pay_BRD.md` (Procurement requirements baseline)
5. `docs/23_Finance_and_Accounting_BRD.md` (Finance requirements baseline)
6. `docs/31_Release_1_Consolidated_Owner_Decision_Pack.md` (§B6, PD-046 approved Finance contract)
7. `docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md` (MESP-125 scope & dependencies)
8. `docs/Decisions.md` (ADR-002, ADR-005, ADR-006, ADR-011, ADR-018, ADR-019)
9. `docs/ADR-019_Tenant_Host_Resolution_Workspace_Context_and_Branding.md` (Tenant isolation & context)

Inspect existing merged MESP-124 / MESP-143 code before adding new components:
- `backend/src/MiniErp.Procurement/` (Purchase Order, Supplier Confirmation, approval, snapshots, persistence)
- `backend/src/MiniErp.Foundation/` (Authorization, audit, idempotency, REST metadata)
- `frontend/src/app/features/procurement/` (Purchase Request, Quotation, Purchase Order workspaces)
- `frontend/src/app/core/` (Session, context switcher, safe error handling, currency formatting)

============================================================
2. PERMANENT PRODUCT & ARCHITECTURAL RULES
============================================================

1. Generic SaaS Rule:
   - No Wafra-specific workflow, schema, permission, report, endpoint, domain logic, or UI branching.
   - `Wafra` remains tenant configuration / validation data only.

2. Tenant != Workspace (ADR-019):
   - Tenant is the server-enforced security and data-isolation boundary.
   - Operational context (Company / Branch / Warehouse) exists inside the authorized Tenant.
   - Never require raw GUID entry in operational UX; use server-resolved context and business party/warehouse references.

3. Cross-Module Ownership & Authority:
   - Cross-module entity IDs (e.g. `PurchaseOrderId`, `GoodsReceiptId`) provide commercial lineage only.
   - Lineage NEVER grants authorization across modules or tenants.
   - Tenant, Company, Branch, and Warehouse authorization must always be derived and verified server-side.

============================================================
3. CORE DOMAIN BOUNDARIES & OWNERSHIP
============================================================

Preserve these exact domain boundaries:

- **Procurement Domain owns**:
  - Purchase Orders, supplier commitments, and commercial lineage;
  - Supplier confirmation facts and commercial tolerances;
  - Receipt eligibility, commercial remainder tracking, and handoff identity to Inventory and Finance.

- **Inventory Domain owns**:
  - Physical Goods Receipt facts and recording at authorized Warehouses;
  - Accepted quantity, rejected quantity, damaged quantity, and quarantine/disposition facts;
  - Warehouse ownership and physical warehouse location scoping;
  - Posted physical-stock truth and stock-ledger mutations (when implemented).

- **Finance Domain owns**:
  - Purchase Invoices, Accounts Payable (AP), and supplier liabilities;
  - Tax/VAT calculation and invoice accounting interpretation;
  - Interim receipt-to-invoice accounting, clearing/accrual entries (per FIN-OD-01 / PD-046);
  - Matching (two-way / three-way), fiscal period control, and financial reconciliation.

============================================================
4. STRICT BOUNDARIES & EXCLUSIONS
============================================================

1. Goods Receipt Stock Boundary:
   - Inventory is the owner of posted physical stock.
   - Do NOT silently invent the later Inventory stock-ledger implementation (MESP-128+).
   - Goods Receipt in MESP-125 must expose a truthful handoff / posting eligibility boundary rather than creating a parallel or temporary stock truth.
   - Never let Procurement persistence become the stock ledger.

2. Purchase Invoice Boundary:
   - MESP-125 provides the Purchase Invoice HANDOFF capability.
   - Goods Receipt itself MUST NOT automatically create:
     - Accounts Payable;
     - Supplier liability;
     - Supplier payment;
     - General Ledger journal;
     - Tax posting;
     - Posted Purchase Invoice.
   - Purchase Invoice source/handoff may be represented only at the approved non-posted handoff boundary. Finance remains authoritative for later AP/tax/posting.

3. MESP-126 Boundary:
   - Do NOT implement MESP-126 inside MESP-125.
   - Specifically do NOT implement the full PO ↔ Goods Receipt ↔ Purchase Invoice three-way matching engine, exception resolution engine, matching tolerance engine, or posting hold/release engine.
   - Preserve the clean data and lineage needed for MESP-126 to consume later.

4. Other Exclusions:
   - Do NOT implement: supplier payment, AP settlement, GL posting, Inventory valuation engine, Moving Weighted Average (MWA) calculation engine, warehouse transfers, stock counts, stock issues, supplier returns, MESP-126, MESP-127, MESP-128+, external supplier portal, external provider integration, ZATCA/FATOORA, production credentials, or Wafra-specific behavior.

============================================================
5. FUNCTIONAL SCOPE & BACKEND REQUIREMENTS
============================================================

Implement a complete, production-grade, bounded vertical slice for MESP-125:

### 5.1 PO Eligibility & Receipt Handoff
- Server-authorized check for PO eligibility:
  - PO must be in an Issued or Confirmed state (not Draft, PendingApproval, Rejected, or Cancelled);
  - Remaining receivable quantity on at least one line must be > 0;
  - Caller must have authorized Tenant and Company/Branch/Warehouse context.
- Receipt request / handoff from Procurement to Inventory with immutable lineage back to PO, Line, Product, UOM, Supplier, Source Decision, and PR.

### 5.2 Goods Receipt Capture & Partial Receiving
- Capture physical receipt events against eligible PO lines:
  - Goods Receipt header: TenantId, CompanyId, BranchId, WarehouseId, PurchaseOrderId, SupplierId, ReceiptNumber/Reference, ReceiptDate, ReceivedByActorId, Notes, Status, Version, Audit timestamps;
  - Goods Receipt lines: PurchaseOrderLineId, ProductId, UOMId, OrderedQuantity, PreviouslyReceivedQuantity, ReceivedQuantity, AcceptedQuantity, RejectedQuantity, DamagedQuantity, RemainingReceivableQuantity, RejectionReason, Notes.
- Support partial receipts: multiple sequential Goods Receipt events against one PO until all eligible quantities are fully received or closed.
- Track exact remaining receivable quantity per line.

### 5.3 Server-Side Quantity Integrity Invariants
- Enforce strict server-side quantity invariants:
  - `ReceivedQuantity = AcceptedQuantity + RejectedQuantity + DamagedQuantity` (or accepted + rejected/damaged disposition);
  - `AcceptedQuantity >= 0`, `RejectedQuantity >= 0`, `DamagedQuantity >= 0`;
  - `AcceptedQuantity + RejectedQuantity + DamagedQuantity <= RemainingReceivableQuantity` (no over-receipt beyond approved/confirmed PO quantity unless explicit approved tolerance applies);
  - Client-calculated remainder is never authoritative; server derives and validates all remainder quantities.

### 5.4 Purchase Invoice Handoff
- Expose a truthful, immutable Purchase Invoice Handoff boundary:
  - Sourced from eligible Goods Receipt(s) and/or PO;
  - Captures commercial/receipt lineage, received/accepted quantities, supplier pricing references, tax references;
  - Status: `EligibleForInvoice`, `HandoffCreated`, `PendingFinanceReview` (non-posted);
  - Lineage connects PO line, Goods Receipt line, Product, UOM, unit price, tax rate reference, and commercial amounts;
  - Does NOT create AP subledger or GL postings.

### 5.5 Concurrency, Idempotency & Audit
- Optimistic concurrency: `If-Match` / ETag version tokens on receipt and handoff mutations;
- Idempotency with durable replay:
  - Same idempotency key + identical request payload → deterministic replay of stored result;
  - Same idempotency key + differing payload or target → HTTP 409 `idempotency_conflict`;
  - Probe replay after authentication/authorization but before state-dependent validation;
  - Stored versioned audit snapshots prevent duplicate mutation, duplicate receipt, duplicate handoff, and duplicate audit records.
- Immutable history and audit log for every Goods Receipt and handoff lifecycle event.

### 5.6 Persistence & API Contracts
- Formal module-owned persistence (dedicated SQL Server schema and SQLite fallback);
- Formal additive EF Core migrations for any new or altered schema;
- Foundation REST operation catalogue with antiforgery protection for unsafe operations;
- Complete OpenAPI / Scalar metadata and safe error contracts (RFC 7807 problem details);
- No raw GUID exposure in operational user flows.

============================================================
6. FRONTEND REQUIREMENTS (ANGULAR)
============================================================

1. Workspace & Routing:
   - Lazy-loaded Goods Receipt and Purchase Invoice handoff workspace under Procurement/Inventory;
   - Clean route structure: list view, create/receive flow from eligible PO, receipt detail view, invoice handoff status view;
   - Integrated into the authenticated shell and Overview-first navigation.

2. Bilingual & Directional Support:
   - Full English and Arabic (EN / AR) localization;
   - Complete RTL and LTR support with proper directional mirroring;
   - Localization for all labels, statuses, messages, table headers, validation errors, and empty states.

3. UI Quality & Accessibility:
   - Dense, professional ERP-grade layout matching established design system;
   - Table headers, ARIA attributes, keyboard navigation, tab panels, focus trapping in dialogs;
   - Safe initial focus and Escape handling on modal dialogs;
   - Clear visual status badges for receipt state (Draft, Received, Partial, Complete) and handoff state;
   - Server-authoritative action availability (`canReceive`, `canCreateInvoiceHandoff`).

4. Currency & Number Formatting:
   - Reusable `formatMoney` with non-ISO safe fallback;
   - SAR presentation asset support without hardcoding.

============================================================
7. TESTING & VALIDATION REQUIREMENTS
============================================================

### 7.1 Backend Validation
- Release build must succeed with 0 warnings and 0 errors:
  ```powershell
  dotnet build .\backend\MiniErp.sln -c Release
  ```
- Run the official safe backend test runner:
  ```powershell
  .\scripts\Test-MiniErpBackend.ps1
  ```
  - Accepted baseline: **793/793 passed, 0 skipped**;
  - Must genuinely execute the SQL Server safety harness against disposable LocalDB and clean up with 0 orphan databases;
  - `MESP_SQLSERVER_CONNECTION_STRING` must remain untouched.
- Add comprehensive focused tests covering:
  - PO receipt eligibility & state validation;
  - Single full Goods Receipt;
  - Partial Goods Receipt and exact remainder computation;
  - Multiple sequential receipts against one PO until remainder = 0;
  - Rejection of over-receipt beyond eligible receivable quantity;
  - Rejected and damaged quantity recording;
  - Cross-Tenant isolation and Company/Branch/Warehouse authorization denial;
  - Optimistic concurrency conflict (`If-Match` mismatch);
  - Durable idempotent replay (identical retry succeeds without duplicate history/audit, conflicting retry returns 409);
  - Purchase Invoice handoff creation and lineage;
  - Prevention of premature AP, tax posting, or GL entries.

### 7.2 Frontend Validation
- Angular unit tests:
  ```powershell
  cd frontend
  npm test -- --watch=false --no-progress
  ```
  - Baseline: **216/216 passed** across 25 spec files (0 regressions).
- Production build & bundle budgets:
  ```powershell
  npm run build
  ```
  - Initial bundle must remain under the 500 kB budget (current baseline: 492.02 kB);
  - Do NOT raise bundle budgets.
- Security audit:
  ```powershell
  npm audit --omit=dev
  npm audit
  ```
  - Must report **0 vulnerabilities**.

### 7.3 Playwright E2E Validation
- Run focused and full Playwright tests:
  ```powershell
  npm run test:e2e -- --project=chromium
  ```
  - Add focused Playwright journey for Goods Receipt capture and invoice handoff;
  - Full Chromium suite must pass cleanly.

============================================================
8. DEVELOPMENT RUNTIME HANDOFF
============================================================

After all code changes, tests, and documentation are complete:

1. Protected Unrelated Ports:
   - Ports `5000` and `5001` belong to unrelated services — NEVER touch, probe, or terminate them.
   - Mini ERP ports are API `5300` and Angular `4300`.

2. Safe Process Management:
   - Check port ownership for 5300 and 4300;
   - Stop only verified stale Mini ERP processes.

3. Start Official Development Launcher:
   ```powershell
   dotnet build .\backend\MiniErp.sln --configuration Release
   .\scripts\Start-MiniErpDevelopment.ps1 -Restart
   ```

4. Runtime Verification:
   - Verify API health endpoint: `http://localhost:5300/health`;
   - Verify public module registration: `http://localhost:5300/module-registration`;
   - Verify OpenAPI / Scalar docs: `http://localhost:5300/scalar/v1`;
   - Verify Angular frontend and MESP-125 Goods Receipt route on `http://localhost:4300/`;
   - Leave API and Angular RUNNING for the Owner.

============================================================
9. GIT, PR & COMPLETION RULES
============================================================

1. Feature Branch:
   - Create `feat/MESP-125-goods-receipt-purchase-invoice-handoff` from synchronized `main`.
   - Commit changes logically with conventional commit messages (e.g. `feat(MESP-125): ...`).
   - Push to `origin/feat/MESP-125-goods-receipt-purchase-invoice-handoff`.

2. Pull Request:
   - Create one Draft PR against `main`.
   - Do NOT merge.
   - Do NOT mark Ready for Review.

3. Owner Asset Protection:
   - Files under `frontend/assets` are protected Owner source assets.
   - Do NOT delete, rename, replace, regenerate, or recolor them.

4. Jira Management:
   - Zero Jira operations by the executor; GPT-5.6 Sol manages Jira transitions.

5. Final Documentation & Hand-Off:
   - Update `docs/staticts.md`, `.ai/CURRENT_STATE.md`, `README.md`, `backend/README.md`, `docs/Decisions.md` with exact delivered validation counts and capabilities;
   - Replace `TASK.md` with the full independent Claude Opus 5 pre-merge review prompt for MESP-125.

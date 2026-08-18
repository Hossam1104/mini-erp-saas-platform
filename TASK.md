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
IN PROGRESS (Activation comment `11503`; Sol pre-implementation hold `11504`; Sol hold clarification `11505`)

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
5. `docs/22_Inventory_and_Warehouse_Management_BRD.md` (Inventory requirements baseline)
6. `docs/23_Finance_and_Accounting_BRD.md` (Finance requirements baseline)
7. `docs/31_Release_1_Consolidated_Owner_Decision_Pack.md` (§B6, PD-027, PD-028, PD-046 approved Finance/Procurement/Inventory contract)
8. `docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md` (MESP-125 scope & dependencies)
9. `docs/Decisions.md` (ADR-002, ADR-005, ADR-006, ADR-011, ADR-018, ADR-019)
10. `docs/ADR-019_Tenant_Host_Resolution_Workspace_Context_and_Branding.md` (Tenant isolation & context)

Inspect existing merged MESP-124 / MESP-143 code before adding new components:
- `backend/src/MiniErp.App/Modules/Procurement/` (Purchase Order, Supplier Confirmation, approval, snapshots, services)
- `backend/src/MiniErp.Contracts/Modules/Procurement/` (Contracts, commands, queries, responses, events)
- `backend/src/MiniErp.Infrastructure/Persistence/Modules/Procurement/` (Entities, EF Core configurations, migrations, repositories)
- `backend/src/MiniErp.Api/PurchaseOrderEndpoints.cs`
- `backend/src/MiniErp.Api/Program.cs`
- `backend/src/MiniErp.Api/RestOpenApiDocumentation.cs`
- Applicable Foundation and building-block code under the real `App`, `Contracts`, `Infrastructure`, and `Api` project structure:
  - `backend/src/MiniErp.Contracts/Modules/Foundation/`
  - `backend/src/MiniErp.App/Modules/Identity/`
  - `backend/src/MiniErp.App/Modules/Audit/`
  - `backend/src/MiniErp.Infrastructure/Persistence/`
- Frontend:
  - `frontend/src/app/features/procurement/` (Purchase Request, Quotation, Purchase Order workspaces)
  - `frontend/src/app/core/` (Session, context switcher, safe error handling, currency formatting)
  - `frontend/src/app/features/`

Note on Backend Layout:
The backend solution consists strictly of:
- `backend/src/MiniErp.Api`
- `backend/src/MiniErp.App`
- `backend/src/MiniErp.Contracts`
- `backend/src/MiniErp.Infrastructure`
Do NOT create new top-level projects merely because legacy prompt drafts named them.

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

3. Cross-Module Ownership & Lineage Authority:
   - Cross-module entity IDs (e.g. `PurchaseOrderId`, `GoodsReceiptId`) provide commercial lineage only.
   - Lineage NEVER grants authorization across modules or tenants.
   - Tenant, Company, Branch, and Warehouse authorization must always be derived and verified server-side.

4. Source of Truth by Domain:
   - **Procurement Domain**: Authoritative for commercial source, Purchase Orders, Supplier Confirmation, receipt eligibility, and commercial handoff.
   - **Inventory Domain**: Authoritative for physical Goods Receipt recording, acceptance/rejection/condition evidence, warehouse custody, and eventual posted-stock truth.
   - **Finance Domain**: Authoritative for Purchase Invoice, Accounts Payable (AP), tax/accounting interpretation, and financial posting.

5. Persistence and Database Providers:
   - Follow the existing module persistence/provider pattern.
   - SQL Server remains the formal migration and runtime path.
   - Preserve the existing Development SQLite fallback only where the current infrastructure already supports it.
   - Do NOT invent a new provider architecture or require a second/parallel persistence strategy.

============================================================
3. CORE DOMAIN BOUNDARIES & EXCLUSIONS
============================================================

Preserve these exact domain boundaries:

1. Goods Receipt / Stock Boundary:
   - Physical Goods Receipt acceptance is Inventory-owned.
   - Only POSTED accepted quantity may eventually increase stock.
   - The full Inventory stock-ledger capability (MESP-128+) is owned by later Inventory work.
   - MESP-125 must implement the approved receipt and handoff evidence needed by its own capability, preserve Inventory ownership, and maintain a clean future stock-posting boundary.
   - NEVER create Procurement-owned stock balances or a temporary parallel stock ledger.
   - If an authoritative Inventory stock-ledger / posting implementation does not yet exist, do NOT fabricate one inside MESP-125. Represent the receipt / posting boundary truthfully without claiming physical on-hand stock has changed.

2. Purchase Invoice Handoff Boundary:
   - MESP-125 provides the Purchase Invoice HANDOFF capability.
   - Goods Receipt itself MUST NOT automatically create:
     - Accounts Payable (AP);
     - Supplier liability;
     - Supplier payment;
     - General Ledger (GL) journal entries;
     - Tax posting;
     - Posted Purchase Invoice.
   - Purchase Invoice source/handoff is represented strictly at the approved non-posted handoff boundary. Finance remains authoritative for all downstream AP, tax, and posting decisions (per FIN-OD-01 / PD-046).

3. MESP-126 Boundary:
   - Do NOT implement MESP-126 inside MESP-125.
   - Specifically do NOT implement the full PO ↔ Goods Receipt ↔ Purchase Invoice three-way matching engine, exception resolution engine, matching tolerance engine, or posting hold/release engine.
   - Preserve sufficient immutable PO, Supplier Confirmation, Goods Receipt, line, quantity, unit price, currency, and tax-reference lineage for MESP-126 and later Finance work to consume.

4. Other Exclusions:
   - Do NOT implement: supplier payment, AP settlement, GL posting, Inventory valuation engine, Moving Weighted Average (MWA) calculation engine, warehouse transfers, stock counts, stock issues, supplier returns, MESP-126, MESP-127, MESP-128+, external supplier portal, external provider integration, ZATCA/FATOORA, production credentials, or Wafra-specific behavior.

============================================================
4. FUNCTIONAL SCOPE & BACKEND REQUIREMENTS
============================================================

Implement a complete, production-grade, bounded vertical slice for MESP-125:

### 4.1 PO Receipt Eligibility & Derivation
- Server-authoritative derivation of PO receipt eligibility:
  - Do NOT hard-code PO status rules. Sonnet must derive receipt eligibility from PD-027, PD-028, existing MESP-124 PurchaseOrder status semantics (`Issued`, `Confirmed`, `PartiallyConfirmed`, `NoResponse`, `Rejected`, `ChangedPendingApproval`, `Cancelled`), the Procurement BRD, the Inventory BRD, and server-authoritative remaining quantity.
  - Principle: A partial Supplier Confirmation can create only the confirmed operational obligation while the remainder stays pending/rejected.
  - If approved evidence is genuinely ambiguous about an edge case, STOP and report that edge case rather than inventing an unapproved business rule.
  - Remaining receivable quantity on at least one line must be > 0.
  - Caller must have authorized Tenant and Company/Branch/Warehouse context.
- Receipt request / handoff from Procurement to Inventory with immutable lineage back to PO, Line, Product, UOM, Supplier, Source Decision, and PR.

### 4.2 Goods Receipt Capture & Partial Receiving
- Capture physical receipt events against eligible PO lines:
  - Goods Receipt header: TenantId, CompanyId, BranchId, WarehouseId, PurchaseOrderId, SupplierId, ReceiptNumber/Reference, ReceiptDate, ReceivedByActorId, Notes, Status, Version, Audit timestamps.
  - Goods Receipt lines: PurchaseOrderLineId, ProductId, UOMId, OrderedQuantity, PreviouslyReceivedQuantity, ReceivedQuantity, AcceptedQuantity, RejectedQuantity, Condition/Damage evidence, RemainingReceivableQuantity, RejectionReason, Notes.
- Support partial receipts: multiple sequential Goods Receipt events against one PO until all eligible quantities are fully received or closed.
- Track exact remaining receivable quantity per line derived authoritatively by the server.

### 4.3 Server-Side Quantity Integrity & Non-Overlapping Invariants
- Enforce strict server-side quantity invariants:
  - `AcceptedQuantity >= 0`
  - `RejectedQuantity >= 0`
  - All explicit quantity fields `>= 0`
  - No receipt may consume more than the server-derived eligible quantity.
  - Do NOT enforce the unsafe equation `ReceivedQuantity = AcceptedQuantity + RejectedQuantity + DamagedQuantity`. Damaged quantity is condition/disposition evidence (e.g. damaged accepted vs. damaged rejected) and not automatically an additive third bucket.
  - Rejected-at-receipt quantity is NOT automatically damaged stock; damaged quantity is NOT automatically rejected.
  - Only posted accepted quantity may eventually increase stock.
  - No quantity may be silently double-counted.
  - Client-calculated remainder is never authoritative; server derives and validates all remainder quantities.

### 4.4 Commercial Remainder Derivation
- Do not automatically reduce the PO commercial remainder by every physically arrived quantity.
- The server must derive the commercial/receivable remainder from authoritative PO, Supplier Confirmation, and Goods Receipt facts.
- Rejected goods must not silently satisfy the supplier's remaining commercial obligation unless an approved disposition explicitly closes that quantity.
- Procurement must retain visibility into accepted quantity, rejected/condition evidence, outstanding quantity, partial receipt status, and remaining/open commercial obligation.

### 4.5 Purchase Invoice Handoff & Lifecycle States
- Implement the minimum explicit Goods Receipt and Purchase Invoice handoff lifecycle needed by the approved BRDs and MESP-125 contract.
- State names and transitions must be derived from approved business semantics, remain auditable, and distinguish recorded/unposted/blocked/eligible/completed or equivalent states without implying AP, tax, GL, stock posting, or matching at the wrong stage.
- Sourced from eligible Goods Receipt(s) and/or PO.
- Captures commercial/receipt lineage, received/accepted quantities, supplier pricing references, tax rate references, and commercial amounts.
- Lineage connects PO line, Goods Receipt line, Product, UOM, unit price, tax rate reference, and commercial amounts.
- Does NOT create AP subledger, supplier liability, or GL postings.

### 4.6 Concurrency, Idempotency & Audit
- Optimistic concurrency: `If-Match` / ETag version tokens on receipt and handoff mutations;
- Idempotency with durable replay:
  - Same idempotency key + identical request payload → deterministic replay of stored result;
  - Same idempotency key + differing payload or target → HTTP 409 `idempotency_conflict`;
  - Probe replay after authentication/authorization but before state-dependent validation;
  - Stored versioned audit snapshots prevent duplicate mutation, duplicate receipt, duplicate handoff, and duplicate audit records.
- Immutable history and audit log for every Goods Receipt and handoff lifecycle event.

### 4.7 Persistence & API Contracts
- Formal module-owned persistence (dedicated SQL Server schema and SQLite fallback where supported);
- Formal additive EF Core migrations for any new or altered schema;
- Foundation REST operation catalogue with antiforgery protection for unsafe operations;
- Complete OpenAPI / Scalar metadata and safe error contracts (RFC 7807 problem details);
- No raw GUID exposure in operational user flows.

============================================================
5. FRONTEND REQUIREMENTS (ANGULAR)
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
   - Clear visual status badges for receipt lifecycle state and invoice handoff state derived from approved semantics;
   - Server-authoritative action availability (e.g. `canReceive`, `canCreateInvoiceHandoff`).

4. Currency & Number Formatting:
   - Reusable `formatMoney` with non-ISO safe fallback;
   - SAR presentation asset support without hardcoding.

============================================================
6. TESTING & VALIDATION REQUIREMENTS
============================================================

### 6.1 Backend Validation
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
  - Authorized receipt eligibility derived from approved PO/confirmation states;
  - Wrong-stage receipt denial;
  - Single full Goods Receipt;
  - Partial Goods Receipt and exact remainder computation;
  - Multiple sequential receipts against one PO until remainder is satisfied or closed;
  - Exact outstanding/remainder derived server-side;
  - Accepted/rejected/damaged/condition semantics without double counting;
  - Rejected quantity not silently treated as accepted stock or closing supplier obligation;
  - Rejection of over-receipt beyond eligible receivable quantity;
  - Cross-Tenant isolation and Company/Branch/Warehouse authorization denial;
  - Optimistic concurrency conflict (`If-Match` mismatch);
  - Durable idempotent replay (identical retry succeeds without duplicate history/audit, conflicting retry returns 409 `idempotency_conflict`);
  - Same-key conflicting request 409;
  - No duplicate receipt/history/audit;
  - Purchase Invoice handoff creation and immutable lineage;
  - Prevention of premature AP, tax posting, GL entries, or supplier payment;
  - No Procurement-owned stock ledger or fabricated stock updates.

### 6.2 Frontend Validation
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

### 6.3 Playwright E2E Validation
- Run focused and full Playwright tests:
  ```powershell
  npm run test:e2e -- --project=chromium
  ```
  - Add focused Playwright journey for Goods Receipt capture and invoice handoff;
  - Full Chromium suite must pass cleanly.

============================================================
7. DEVELOPMENT RUNTIME HANDOFF
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
8. GIT, PR & COMPLETION RULES
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

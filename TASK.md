# MINI ERP SAAS PLATFORM
# MESP-125 — GOODS RECEIPT + PURCHASE INVOICE HANDOFF
# INDEPENDENT PRE-MERGE CAPABILITY & SECURITY REVIEW TASK

Sole Reviewer:
Claude Opus 5

Reasoning:
HIGH

Mode:
INDEPENDENT PRE-MERGE CAPABILITY & SECURITY REVIEW (READ-ONLY)

Feature Branch:
feat/MESP-125-goods-receipt-purchase-invoice-handoff

Base Branch:
main

============================================================
0. REVIEW MANDATE & RULES
============================================================

Review Mandate:
Conduct a rigorous, independent, read-only pre-merge review of the MESP-125
(Goods Receipt and Purchase Invoice Handoff) implementation on branch
`feat/MESP-125-goods-receipt-purchase-invoice-handoff`.

Rules for Claude Opus 5:
1. READ-ONLY: Do NOT modify code, delete assets, or write Jira.
2. DO NOT MERGE: The branch remains a Draft PR against `main`. GPT-5.6 Sol and
   the Owner decide on PR merge and Jira closure.
3. Protected Assets: Verify that `frontend/assets/` remains completely untouched.
4. Port Rules: API is port 5300, Angular is port 4300. Ports 5000/5001 are
   unrelated protected services.

============================================================
1. MANDATORY REVIEW CRITERIA
============================================================

Verify the following 12 key architectural, security, and functional dimensions:

### 1. Tenant & Operational Scope Isolation
- Verify Tenant isolation on all Goods Receipt and Invoice Handoff read/write paths.
- Ensure Company/Branch/Warehouse operational context is derived and authorized server-side.
- Ensure cross-tenant or mismatched-scope warehouse selections fail closed (`warehouse_not_authorized`, `warehouse_scope_denied`).

### 2. Warehouse Provider & Authorization
- Verify `IProcurementWarehouseProvider` contract and its DI registration in API bootstrap.
- Confirm inactive warehouses are rejected (`warehouse_inactive`).
- Verify warehouse listing returns only active warehouses belonging to the authorized Tenant.

### 3. Physical Receiving & Quantity Invariants
- Verify physical partition invariant: `ReceivedQuantity = AcceptedQuantity + RejectedQuantity` (`ReceivedQuantity > 0`, `AcceptedQuantity >= 0`, `RejectedQuantity >= 0`).
- Confirm independent condition overlay: `DamagedQuantity <= ReceivedQuantity` (descriptive condition/disposition overlay, non-additive, never double-counted; `Received != Accepted + Rejected + Damaged` and `Received != Accepted + Damaged`).
- Verify over-receipt prevention: total accepted quantity across all active receipts cannot exceed the PO line receivable quantity (`over_receipt_not_allowed`).
- Confirm damaged quantity is preserved as condition evidence without double-counting against physical total.

### 4. Commercial Remainder & PO Receipt Eligibility
- Verify receipt eligibility is derived from Confirmed POs with receivable remainder > 0.
- Verify commercial remainder calculation: `RemainingReceivableQuantity = ConfirmedQuantity - sum(Active AcceptedQuantity)`. Rejected physical quantity does not satisfy the supplier's commercial obligation.
- Confirm wrong-stage POs (Draft, PendingApproval, Rejected, Cancelled) reject receipt attempts.
- Confirm partial receipts correctly decrement server-derived receivable remainder, and cancelled receipts restore the remainder.

### 5. Goods Receipt Cancellation & Active Handoff Reference Blocking
- Verify receipt cancellation requires a reason note.
- Verify cancellation is blocked if the receipt is referenced by an active Purchase Invoice Handoff (`goods_receipt_referenced_by_active_invoice_handoff`).
- Verify that cancelling the referencing handoff releases the receipt, allowing subsequent cancellation.

### 6. Purchase Invoice Handoff & Pro-Rata Tax
- Verify handoff is created strictly from accepted Goods Receipt lines belonging to Confirmed POs.
- Verify pro-rata tax allocation matches line proportion without rounding leaks.
- Verify un-invoiced remainder tracking prevents duplicate invoicing of the same received line (`RemainingHandoffQuantity = AcceptedQuantity - sum(Active HandedOffQuantity)`).
- Verify handoff cancellation releases receipt lines for re-invoicing.

### 7. FIN-OD-01 / PD-046 Boundary Preservation
- Confirm that Goods Receipt and Invoice Handoff do NOT fabricate general ledger journal entries, AP subledger postings, supplier payments, or inventory stock ledger entries.
- Confirm Finance domain authority is fully respected.

### 8. Concurrency & Race Condition Prevention
- Verify optimistic concurrency via `If-Match` / ETag headers on all mutation endpoints.
- Verify that `.TouchVersion()` on source entities (PO, Receipt) enforces EF Core concurrency checks to prevent concurrent over-receipt or over-invoicing races (10 -> 7/7 concurrent requests result in 1 success and 1 `concurrency_conflict` / `over_receipt_not_allowed`).

### 9. Idempotency & Durable Replay
- Verify deterministic request fingerprinting (SHA-256).
- Verify versioned immutable audit snapshots.
- Verify identical retries replay stored responses, and conflicting retries return HTTP 409 `idempotency_conflict`.

### 10. Bilingual Angular Workspaces & Accessibility
- Verify Goods Receipt workspace at `/app/procurement/goods-receipts` (List, Create with PO source selector and warehouse picker, Detail with tabs, Cancel dialog).
- Verify Purchase Invoice Handoff workspace at `/app/procurement/invoice-handoffs` (List, Create with receipt lines and pro-rata tax preview, Detail with tabs, Cancel dialog).
- Verify English/Arabic bilingual toggle, RTL/LTR layout, ARIA attributes, focus trapping, and keyboard navigation.

### 11. Asset & Schema Integrity
- Verify zero modifications to `frontend/assets/`.
- Verify EF Core migrations for procurement persistence.

### 12. Full Verification Baseline
- Release solution build: `dotnet build backend\MiniErp.sln -c Release` (0 warnings / 0 errors).
- Official backend test runner: `.\scripts\validate-foundation.ps1` or `.\scripts\Test-MiniErpBackend.ps1` (812/812 passing against disposable LocalDB).
- Angular unit tests: `npm test -- --watch=false --no-progress` inside `frontend/` (232/232 passing across 29 spec files).
- Production build: `npm run build` inside `frontend/` (initial bundle <= 500 kB budget).
- Playwright E2E: `npm run test:e2e` inside `frontend/` (19/19 passing).
- Dependency security: `npm audit --omit=dev` (0 vulnerabilities).

============================================================
2. OUTPUT VERDICT FORMAT
============================================================

Your review report must include:
1. Executive Verdict: `APPROVE FOR MERGE` | `CHANGES REQUIRED` | `BLOCKED`
2. Findings Breakdown: P0 (Blocker), P1 (Critical), P2 (Major), P3 (Minor/Observation)
3. Architectural & Domain Invariant Assessment (Tenant isolation, FIN-OD-01, quantity integrity, concurrency, idempotency)
4. Full Validation Evidence Summary
5. Jira & Handoff Recommendation for GPT-5.6 Sol and Owner

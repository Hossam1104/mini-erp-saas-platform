# MINI ERP SAAS PLATFORM
# MESP-125 — GOODS RECEIPT + PURCHASE INVOICE HANDOFF
# ACTIVATION GATE BLOCKED — FIN-OD-01 OWNER DECISION REQUIRED

============================================================
0. ACTIVATION STATUS: BLOCKED / NOT ACTIVATED
============================================================

Planned implementation capability:
MESP-125 — Implement Goods Receipt and Purchase Invoice handoff capability

Parent Epic:
MESP-7 — EPIC 07 - Procurement and Purchase-to-Pay

Current Jira Status:
To Do

Current Repository Status:
NOT ACTIVATED / IMPLEMENTATION PROHIBITED

Blocking Gate:
FIN-OD-01 — Goods Receipt interim accounting, clearing/accrual, valuation/posting treatment owner decision

============================================================
1. MANDATORY EXECUTOR INSTRUCTION
============================================================

IF YOU ARE AN AI EXECUTOR OPENING THIS TASK.md:

1. STOP IMMEDIATELY.
2. DO NOT create branches or write code.
3. DO NOT modify backend, frontend, tests, migrations, schema, or assets.
4. DO NOT write or transition Jira tickets.
5. DO NOT invent, infer, or assume FIN-OD-01 Finance policy.
6. The allowed next action is:
   GPT-5.6 Sol / Product Owner (Hossam) must resolve and record the FIN-OD-01 decision first.

Only AFTER FIN-OD-01 is formally approved and recorded in the repository and Jira may a fresh implementation session activate MESP-125.

============================================================
2. BASELINE CONTEXT & MERGED CAPABILITY
============================================================

Repository baseline:
- Branch: main (synchronized with origin/main)
- Starting HEAD: c742d9c897edb715c7e3c25df7e9ca2c4f30d1e6
- MESP-143: Complete, reviewed (APPROVE FOR MERGE), squash-merged in PR #67 (`866cb75bb7d0d97c929216b1a449f458a2614097`).
- MESP-124: Complete, independently reviewed by Claude Opus 5 (APPROVE FOR MERGE), squash-merged in PR #68 at commit `c742d9c897edb715c7e3c25df7e9ca2c4f30d1e6` (merge timestamp 2026-08-18T21:37:47Z; reviewed feature head `0eca12dbecffe7e8abeff6914566fa4de329d2c7`).

Authoritative MESP-124 merged capabilities:
- PR / quotation / source-decision lineage
- Purchase Order draft, edit, submit, approval, rejection, return
- Multi-stage approval, delegation, and separation of duties (no self-approval)
- Purchase Order issue, cancellation
- Manual Supplier Confirmation: full, partial, rejected, no-response
- Supplier-proposed quantity, price, and delivery-date changes with controlled reapproval
- Exact confirmation remainder tracking
- Server-authoritative Tenant and Company/Branch context enforcement
- Lifetime Tenant-scoped `(TenantId, SourceDecisionId)` unique consumption
- Exact durable idempotent replay over immutable audit snapshots
- Optimistic concurrency (`If-Match` / ETag)
- Immutable history, audit, and commercial snapshots
- English / Arabic RTL/LTR responsive Angular workspace
- Dialog/tab/table accessibility (ARIA controls, focus trap, keyboard navigation)
- Formal module-owned SQL Server EF Core migrations

Validation baseline achieved on merge:
- Release build: 0 warnings / 0 errors
- Backend tests: 793/793 passed, 0 skipped (including LocalDB SQL Server safety harness)
- Focused Purchase Order tests: 14/14
- Focused PO + REST foundation tests: 47/47
- Angular unit tests: 216/216 across 25 spec files
- Production build: 492.02 kB initial / 76.78 kB PO lazy / 91.94 kB quotation lazy
- npm audit --omit=dev: 0 vulnerabilities
- full npm audit: 0 vulnerabilities
- Focused PO Playwright Chromium: 8/8
- Full Chromium Playwright: 16/16

============================================================
3. DECISION GATES & DOMAIN BOUNDARIES FOR MESP-125
============================================================

Prior Decision Gates Verified:
- MESP-41 (Procurement approval policy): Done
- MESP-43 (Supplier quote evaluation): Done
- MESP-44 (Purchase order lifecycle & confirmation): Done
- MESP-45 (Goods receipt physical & tolerance baseline): Done
- MESP-113 (INV-OD-004 inventory valuation method): Done

Active Blocker:
- FIN-OD-01 (Goods Receipt interim accounting, clearing/accrual, valuation/posting treatment):
  NOT RESOLVED / NOT APPROVED.
  The approved Finance baseline (`docs/23_Finance_and_Accounting_BRD.md` & `docs/99_Independent_Opus_5_Finance_BRD_Reconciliation.md`) explicitly stipulates that interim receipt-to-invoice accounting, clearing/accrual accounts, and interim inventory valuation treatment require an explicit Owner decision before Goods Receipt accounting can be implemented.

Permanent Domain Boundaries to Preserve for MESP-125:
- **Inventory Domain owns**:
  - Physical goods receipt recording at authorized Warehouses
  - Accepted, rejected, damaged, and quarantined quantities
  - Immutable stock ledger mutations and posted on-hand inventory
  - Physical serial/lot/tracking facts (where applicable)
- **Procurement Domain owns**:
  - Purchase Orders, supplier commitments, and commercial lineage
  - Supplier confirmation facts and commercial tolerances
  - Receipt eligibility, commercial remainder, and handoff to Inventory/Finance
- **Finance Domain owns**:
  - Purchase Invoices, Accounts Payable (AP), and supplier liabilities
  - Tax/VAT calculation and invoice accounting interpretation
  - Interim receipt-to-invoice accounting, clearing/accrual entries (per FIN-OD-01)
  - Matching (two-way / three-way), fiscal period control, and financial reconciliation
- **Strict Invariants**:
  - A Goods Receipt MUST NOT silently create Accounts Payable, supplier liability, payment, or a Purchase Invoice.
  - Procurement MUST NOT become the physical stock source of truth.
  - Inventory MUST NOT create financial general ledger postings without Finance domain authority.

============================================================
4. CARRIED FORWARD NON-BLOCKING P3 ITEMS (FROM MESP-124)
============================================================

These non-blocking observations from the Claude Opus 5 pre-merge review are carried forward for future maintenance/governance:

- **P3-1**: Approval stage empty `EligibleApproverIds` semantics are implicit/inherited from MESP-123.
- **P3-2**: Supplier-change rejection pending-change query has minor line-ID predicate asymmetry.
- **P3-3**: Some state/config errors still map to generic HTTP 400 rather than more precise HTTP 409 / 503 semantics.
- **P3-4**: Angular creates a new idempotency key per explicit user retry; durable replay is therefore mainly server/API retry protection.
- **P3-5**: `ReplayResponseSnapshotJson` duplicates commercial data in immutable audit and must feed retention/privacy/purge governance (MESP-50).
- **P3-6**: `scripts/Test-MiniErpBackend.ps1` should neutralize inherited `MESP_DEV_AUTH_BYPASS` during tests.
- **P3-7**: Cancelled/Rejected PO permanently consumes the source decision in MESP-124; controlled reopen remains a future explicit capability/decision.
- **P3-8**: Transitive `nanoid` lockfile-only security patch is intentionally present.

============================================================
5. NEXT EXACT ACTION
============================================================

1. GPT-5.6 Sol and Product Owner (Hossam) resolve FIN-OD-01.
2. Record the approved decision in `docs/Decisions.md` and Jira.
3. Prepare the active MESP-125 implementation specification and task prompt.
4. Activate MESP-125 in Jira and repository context.

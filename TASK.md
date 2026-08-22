# MESP-129 - Sol Delta-Acceptance Handoff: OPUS P1 Supplier Return Capacity Remediation

Reviewer: GPT-5.6 Sol

Repository: `D:\AI Tools\Hossam\mini-erp-saas-platform`

Capability: MESP-129 - Goods Receipt, Warehouse Transfer, and Supplier Return physical movements

Branch: `feat/MESP-129-physical-stock-movements`

Exact main base SHA: `2cf6b315c69c87f26ca4bbfc774e3e0eb451c5e3`

Exact remediation starting SHA: `b5a0aaca856d571089c65d341de4b8e19205793d`

P1 implementation/test commit SHA: `a824e8aa75a7958eac177f65ca6ef7618ccf3695`

Final branch SHA: `a824e8aa75a7958eac177f65ca6ef7618ccf3695` source baseline; the final documentation/runtime handoff tip is reported in the completion report because Git cannot embed a commit's own SHA in that commit's content.

Draft PR: `#73` - https://github.com/Hossam1104/mini-erp-saas-platform/pull/73

Jira: MESP-129 remains IN PROGRESS / ACTIVATED. No Jira writes were performed.

## Exact P1 defect

`InventoryPersistence.PostSupplierReturnAsync` previously queried persisted
OnHand and active Reserved independently for each Supplier Return line. Earlier
same-identity movements were EF Added-but-unsaved, so repeated lines resolving
to one Company/Branch/Warehouse/Product/UOM/TrackingIdentity key each saw the
same pre-loop persisted balance. Multiple commercial lines could therefore
oversubscribe physical stock or consume Reserved stock.

## Bounded correction

The existing Serializable transaction, `AcquireConcurrencyAnchorsAsync`,
`StockIdentityKey`, replay/source-uniqueness architecture, handoff seam, and
movement-per-line lineage are unchanged. The corrected posting path:

1. materializes each source line with its StockIdentityKey;
2. acquires the existing deterministic concurrency anchors;
3. resolves OnHand and active Reserved once per distinct identity;
4. accumulates staged outbound quantity per identity and validates
   `OnHand - TotalOutbound >= Reserved` for every line before creating a
   movement; and
5. creates one immutable outbound movement per commercial Supplier Return line
   only after all aggregate capacity checks pass.

If any identity fails, the transaction returns conflict and rolls back all
work: zero Supplier Return movements, zero success replay, zero success audit,
and no handoff attempt or recorded effect.

## Required regressions delivered

- Over-capacity same identity: distinct Supplier Return, Goods Receipt, and
  Purchase Order lines return 6 + 6 against OnHand 10 / Reserved 0; posting
  fails closed, OnHand remains 10, no movement/replay/success audit exists, and
  the handoff writer is not called.
- Reservation protection: distinct lines return 8 + 8 against OnHand 20 /
  Reserved 5; available capacity is 15, posting fails closed, OnHand remains
  20, Reserved remains 5, Available remains 15, and no movement/replay/success
  audit/handoff effect exists.
- Exact-boundary success: distinct lines return 7 + 8 against OnHand 20 /
  Reserved 5; posting succeeds at total outbound 15, leaves OnHand 5 /
  Reserved 5 / Available 0, and creates exactly two separate movements.
- Lineage proof: both success movements retain shared SupplierReturnId,
  GoodsReceiptId, PurchaseOrderId, Warehouse/Product/UOM identity, and their
  own SupplierReturnLineId, GoodsReceiptLineId, and PurchaseOrderLineId.

## Validation evidence

- Release build: 0 warnings / 0 errors.
- Focused Inventory tests: 33 executed, 33 passed, 0 failed, 0 skipped.
- Focused Goods Receipt/Supplier Return tests: 23 executed, 23 passed, 0
  failed, 0 skipped.
- SQL Server safety: 29 executed, 29 passed, 0 failed, 0 skipped through the
  canonical disposable LocalDB runner; existing SQL safety remains green.
- Canonical `scripts/Test-MiniErpBackend.ps1 -NoBuild:$false`: 896 executed,
  896 passed, 0 failed, 0 skipped, including disposable LocalDB safety.
- Angular unit tests: 241 executed, 241 passed, 0 failed, 0 skipped across
  32 spec files.
- Angular production bundle: 499.97 kB initial; Inventory lazy chunk 33.12
  kB; Supplier Quotation lazy chunk 91.94 kB; the 500 kB budget remains green.
- Full Chromium Playwright: 26 executed, 26 passed, 0 failed, 0 skipped.
- `npm audit --omit=dev`: 0 vulnerabilities.
- `npm audit`: 0 vulnerabilities.
- `git diff --check`: clean.
- `frontend/assets`: untouched.

## Release-1 deployment constraint

The current `SupplierReturnPhysicalEffectGate` uses a process-local
`ConcurrentDictionary<Guid, SemaphoreSlim>` and is acceptable only while
exactly one API process is active. Horizontal API scale-out is not approved
while this gate is the only cross-module Supplier Return lifecycle
coordinator. Before multiple API instances are enabled, durable cross-instance
coordination must replace or supplement it. This constraint is specific to the
current MESP-129 Supplier Return physical/commercial coordination; it does not
state that the whole ERP can never scale out and does not change MESP-128 stock
concurrency anchors.

## Deferred P3 items

No Redis, distributed lock, or new database lock architecture was introduced.
SupplierReturnPhysicalEffectGate dictionary eviction, the harmless Goods
Receipt `TrackingEnabled ? string.Empty : string.Empty` cleanup, handoff retry
redesign for Product/Warehouse deactivation, and generic cleanup/refactoring
remain deferred.

## Runtime verification

The official `scripts/Start-MiniErpDevelopment.ps1 -Restart` launcher selected
the following URLs and the processes remain alive for Owner inspection:

- Backend URL: `http://localhost:5300`
- Frontend URL: `http://localhost:4300`
- Backend PID: `26432` (`MiniErp.Api`)
- Frontend PID: `22280` (`node` / Angular development server)
- `GET http://localhost:5300/health`: HTTP 200
- `GET http://localhost:4300/`: HTTP 200
- `GET http://localhost:4300/main.js`: HTTP 200
- Both backend and frontend processes remain alive.

## Delivery state and boundaries

PR #73 remains Open, Draft, and unmerged. Do not mark Ready, merge, rebase,
force-push, create another PR, or write Jira. Do not start MESP-130, MESP-131,
Sales commercial returns, Finance/AP/AR/GL, payment, external integrations,
statutory/ZATCA/FATOORA, production DNS/TLS, supplier portal, or any other
downstream implementation.

The next exact action is Sol targeted delta acceptance of the exact final
branch SHA and this P1 delta against the existing MESP-129 acceptance model.

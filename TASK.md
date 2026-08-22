# MESP-129 - Sol Acceptance Handoff: Physical Inventory Movements, Transfers, In-Transit, and Returns

Reviewer: GPT-5.6 Sol

Repository: `D:\AI Tools\Hossam\mini-erp-saas-platform`

Capability: MESP-129 - Implement Goods Receipt, Warehouse Transfer, In Transit, and returns

Branch: `feat/MESP-129-physical-stock-movements`

Exact main base SHA: `2cf6b315c69c87f26ca4bbfc774e3e0eb451c5e3`

Code-complete implementation SHA: `01ea8f7369d173c15cf55a723d6bd95006208282`

Exact remediation starting SHA (local and `origin`):
`d8d852f4e93602ce66583157163e652e57795f2e`

Remediation source and final handoff SHAs are recorded below after the bounded
correction commits are pushed.

Draft PR: `#73` - https://github.com/Hossam1104/mini-erp-saas-platform/pull/73

Jira: MESP-129 is IN PROGRESS / ACTIVATED. Jira writes are prohibited in this
handoff; Sol owns the acceptance decision.

Delivery state: the six Sol acceptance remediations are implemented and
validated from the exact starting SHA above. The branch and PR must remain
open, Draft, and unmerged. Do not merge, rebase, force-push, create a second
PR, or start MESP-130/MESP-131 or downstream implementation.

## Exact bounded scope delivered

This session started from the exact synchronized MESP-128 main baseline and
implemented only the MESP-129 physical Inventory capability:

- authoritative Procurement Goods Receipt accepted-quantity posting;
- Goods Receipt cancellation safety after an active Inventory effect;
- authoritative Procurement Supplier Return physical outbound posting;
- Inventory-owned direct and two-step Warehouse Transfer;
- shipment, partial receipt, derived InTransit, shortage/loss, overage rejection,
  and safe pre-shipment cancellation;
- immutable physical movement and transfer-event evidence;
- server-side Tenant, Company, Branch, Warehouse, Product, UOM, and permission
  enforcement;
- durable idempotency, independent business-source uniqueness, concurrency,
  audit/history, REST/OpenAPI metadata, and bilingual Angular workflow; and
- a reusable but currently unavailable authoritative Sales Customer Return
  Inventory integration boundary.

## Sol acceptance remediations applied

The bounded remediation addresses exactly the six findings in the attached
MESP-129 acceptance model:

1. Tracked Goods Receipt and Supplier Return physical posting now fails closed
   with `tracking_identity_required` when the authoritative Procurement source
   says `Product.TrackingEnabled`; no tracking identity or tracked bucket is
   fabricated, while untracked posting remains unchanged.
2. Goods Receipt cancellation now has truthful `ActiveEffectExists`,
   `NoActiveEffect`, and `Unavailable` verification outcomes. Unavailable,
   unknown, or throwing verification returns
   `inventory_effect_verification_unavailable` without changing status/history.
3. Supplier Return replay probes durable Inventory idempotency before
   Procurement source eligibility. Exact Tenant/actor/operation/key/fingerprint
   replay converges after the Procurement handoff advances; fingerprint changes
   conflict and cannot create another movement.
4. Duplicate Warehouse Transfer receipt references are detected after
   normalization for the same transfer and received event, converge with
   explicit Duplicate audit evidence, and never surface as a persistence 503 or
   create another destination movement. The unique database index remains.
5. Receipt mutations acquire the existing MESP-128 destination stock-identity
   anchor (Tenant, Company, Branch, destination Warehouse, Product, UOM,
   TrackingIdentity) under Serializable isolation before receipt effects.
6. The SQL safety regression now migrates one disposable LocalDB catalog through
   Tenancy, Master Data, Business Parties, Procurement, and Inventory in order,
   with separate history tables and shared Tenancy ownership; it uses committed
   migrations, not `EnsureCreated`.

The production headline remains approximately 47% overall and 41%
Procurement/P2P pending acceptance and merge. No Jira completion credit is
claimed from implementation or test activity.

## Explicit non-scope

Do not infer or start any of the following from this handoff:

- MESP-130 Inventory Count, generic Stock Adjustment, or Stock Issue;
- MESP-131 Moving Weighted Average valuation or landed cost;
- AP, AR, GL, tax accounting, payments, supplier balances, or Finance journals;
- commercial Sales Customer Return, Sales Order, Delivery, Credit Note, or
  other downstream Sales behavior;
- external providers, statutory/ZATCA/FATOORA behavior, production DNS/TLS,
  supplier portal, or production cutover;
- Wafra-specific core logic; or
- automatic reconciliation, correction, or reversal behavior outside the
  explicit MESP-129 evidence seams.

## Goods Receipt physical effect

Inventory consumes the existing Procurement Goods Receipt via an application /
provider contract; Inventory does not duplicate Goods Receipt creation.

- Only authoritative `AcceptedQuantity` creates an inbound movement.
- `RejectedQuantity` never enters OnHand. `DamagedQuantity` remains the
  Procurement non-additive condition overlay and is not treated as accepted
  stock without an authoritative source distinction.
- The source is Tenant/scope/warehouse valid, Recorded, line-identifiable, and
  Product/UOM authoritative. Cancelled or unavailable source evidence fails
  closed.
- The durable business identity is Tenant + GoodsReceiptId + GoodsReceiptLineId.
  It is independent of request key, actor, time, quantity, and movement ID.
- Repeating the same source line with a different idempotency key returns the
  existing evidence and creates no second movement. The duplicate attempt has
  explicit durable Duplicate audit evidence.
- Goods Receipt cancellation is blocked through an application/provider effect
  reader while an active Inventory physical effect exists; verification
  unavailability fails closed without mutating Procurement. Tracked sources
  without an authoritative tracking identity are rejected before persistence.
  Procurement does not query Inventory tables directly and no movement is
  silently deleted.

## Supplier Return physical effect

Inventory consumes the existing Procurement Supplier Return source and handoff
seam.

- Only the repository's `AwaitingInventory` state is eligible. Draft,
  Submitted, Rejected, Cancelled, Reversed, and other states do not post stock.
- Outbound movements preserve SupplierReturnId/LineId, GoodsReceiptId/LineId,
  PurchaseOrderId/LineId where available, Warehouse, Product, UOM, tracking
  identity, source snapshots, actor, reason, time, correlation, and audit.
- Returned quantity is server-derived from the authoritative source and cannot
  exceed the return quantity, current physical stock, or active Reserved stock.
  The resulting OnHand is always at least active Reserved.
- Durable source uniqueness prevents duplicate Supplier Return physical effects
  across different request keys, actors, retries, and process restarts. Replay
  is probed in Inventory before source eligibility, is exact for the same
  Tenant/actor/operation/key/fingerprint, and converges after the Procurement
  handoff advances; fingerprint conflict is deterministic.
- After the physical commit, Inventory writes a handoff reference that names
  actual Inventory movement evidence through the existing Procurement seam.
  If the handoff update is unavailable, the API exposes `inventory_handoff_pending`;
  a retry detects the existing source effect and does not post stock again.
- The upstream state contract prevents commercial reversal after physical
  posting unless an approved physical reconciliation path exists. No silent
  commercial/physical disagreement is allowed and no Finance credit is made.

## Warehouse Transfer physical effect

Warehouse Transfer is Inventory-owned.

- Source and destination Warehouse authority is resolved server-side for every
  read and mutation. Both warehouses must be active, distinct, same Tenant,
  same Company, and separately authorized. Cross-Company/intercompany transfer
  fails closed.
- Direct transfer is one Serializable atomic operation: source OnHand decreases
  Q, destination OnHand increases Q, InTransit is zero, and both movements
  share durable transfer lineage. A source-only commit is not possible through
  the direct operation.
- Two-step shipment decreases source OnHand by Q and creates exactly Q derived
  InTransit. Destination OnHand does not increase at shipment.
- Receipt increases destination OnHand only by the received quantity and derives
  remaining InTransit from immutable transfer events. Partial receipt is valid.
- Receipt above remaining shipped quantity is rejected; the excess is not
  fabricated as a transfer, opening balance, or generic adjustment.
- A repeated normalized receipt reference for the same transfer converges to
  the existing transfer state with Duplicate audit evidence and no second
  destination movement or persistence-unavailable classification.
- Shortage/loss resolution records transfer/line, quantity, reason, actor,
  time, scopes, reference, and audit/history. It closes outstanding InTransit
  without another source outbound movement or a Finance loss journal.
- A Draft transfer can be cancelled without physical movement. After shipment,
  cancellation is blocked until outstanding InTransit is received, resolved as
  loss, or otherwise explicitly reconciled by a future approved operation.
- Transfer list/read/history/audit enforce both source and destination
  authorization and Tenant ownership.

## Shared integrity rules

- OnHand, Reserved, Available, Expected, Damaged, and InTransit remain distinct;
  projections derive from immutable movement/event evidence without double
  counting.
- Negative stock is blocked by default. Supplier Return, direct transfer, and
  transfer shipment respect active reservations and never auto-release or
  reduce a reservation.
- Existing MESP-128 `InventoryConcurrencyAnchor` is retained. Multi-identity
  operations acquire anchors in deterministic global order under Serializable
  isolation; ordering never follows caller input order. Receipt operations
  acquire the destination identity anchor explicitly before destination stock
  mutation.
- The only current Product tracking rule remains `TrackingEnabled`. Enabled
  products require tracking identity; disabled products reject it. No batch,
  lot, serial, expiry, GS1, EAN, or scanner engine was invented.
- Authoritative Product base UOM identity/code is used through existing source
  and Master Data contracts. No alternate-UOM conversion engine was added.
- New MESP-129 physical movements have `ValuationStatus = Pending` and nullable
  UnitCost/CurrencyCode when no authoritative Inventory cost exists. Opening
  balance cost evidence remains known. No zero-cost fiction, PO-price copying,
  MWA, or GL effect was added.

## Customer Return boundary

There is no authoritative Sales commercial Customer Return source in this
repository. MESP-129 therefore provides only the Inventory provider/application
boundary and a safe unavailable result. The API cannot accept arbitrary
CustomerId, InvoiceId, DeliveryId, ProductId, quantity, or client stock
evidence to increase OnHand. The Angular workspace displays a truthful
awaiting-authoritative-Sales-handoff state. Sales later owns the source.

## Concurrency, idempotency, audit, and module boundaries

- Every public mutation uses the existing anti-forgery, server Tenant context,
  permission metadata, ETag/If-Match lifecycle concurrency, Idempotency-Key,
  correlation, safe error classification, and audit patterns.
- Request idempotency is separate from business-source uniqueness for Goods
  Receipt lines, Supplier Return sources/lines, transfer shipment, and transfer
  receipt.
- Important successes, duplicate source attempts, authorization denials,
  reservation/negative-stock blocks, overage, shortage/loss, cancellation,
  concurrency conflicts, idempotency conflicts, and handoff-pending outcomes
  are durably evidenced where the operation reaches persistence.
- Inventory does not query ProcurementDbContext or future Sales tables, use raw
  cross-schema SQL, or own Procurement/Sales commercial state. Procurement-facing
  handoff and Goods Receipt cancellation use application/provider contracts.

## Database and migration ownership

Formal Inventory migration:
`20260822092802_MESP129PhysicalStockMovements`.

It adds the MESP-129 Inventory transfer tables and physical-movement lineage /
valuation columns, without recreating or dropping the shared
`tenancy.TenantOwnedRecords` table or taking ownership of its Tenancy migration.
The SQL safety fixture applies the committed Tenancy, Master Data, Business
Parties, Procurement, and Inventory migrations in that order to one disposable
LocalDB catalog, verifies each separate history table is complete, checks the
single shared Tenant-owned table/index and expected Inventory tables, and
safely disposes the disposable catalog. No persistent MESP database was used or
changed.

## Validation evidence

- `dotnet build backend/MiniErp.sln -c Release --no-restore`: 0 warnings / 0
  errors.
- Focused Inventory architecture/persistence tests: 26/26 passed, including
  tracked-source, durable replay, duplicate receipt, and destination-anchor
  regressions.
- Focused Goods Receipt/Supplier Return cancellation and handoff tests: 19/19
  passed.
- SQL Server LocalDB safety tests: 28/28 passed using one disposable catalog.
- Canonical `scripts/Test-MiniErpBackend.ps1 -NoBuild:$false`: 884/884 passed,
  0 skipped, with disposable LocalDB safety execution.
- Angular unit tests: 241/241 across 32 spec files.
- Angular production build: 499.97 kB initial; Inventory lazy chunk 33.12 kB.
- Playwright Chromium: 26/26.
- `npm audit --omit=dev` and full `npm audit`: 0 vulnerabilities.
- `git diff --check`: clean.
- `frontend/assets`: unchanged.
- Official Development launcher restart and authenticated HTTP health/frontend
  verification: required before handoff; the final URLs and PIDs are recorded
  in the completion report, and both processes remain running.

## Exact Sol review checklist

Review the code-complete commit
`01ea8f7369d173c15cf55a723d6bd95006208282`, the six-remediation source commit
recorded below, and Draft PR #73, especially:

1. Procurement source/provider contracts and Goods Receipt cancellation seam.
2. Supplier Return `AwaitingInventory` eligibility, lineage, reservation guard,
   handoff convergence, duplicate audit, and reversal boundary.
3. Transfer authorization on both warehouses, atomic direct flow, Serializable
   lock ordering, immutable event-derived InTransit, partial receipt, overage,
   shortage/loss, and cancellation rules.
4. Movement source uniqueness, replay/fingerprint conflict handling, audit and
   history evidence, and pending valuation representation.
5. Customer Return unavailable boundary and absence of arbitrary client posting.
6. Formal migration ownership, model snapshot, Tenant query filters, and the
   disposable LocalDB migration-order regression.
7. REST/OpenAPI metadata, anti-forgery, If-Match/ETag, permissions, EN/AR RTL
   UI behavior, and the exact validation counts above.
8. The remediation-specific fail-closed results, replay-before-source order,
   duplicate receipt convergence, destination anchor acquisition, and truthful
   five-context migration-order SQL proof.

## Required next action

Sol should perform delta acceptance of the exact final branch SHA recorded below
against the six remediation findings. Keep PR #73 Draft and unmerged. Do not
write Jira and do not start MESP-130, MESP-131, Sales commercial returns,
Finance, or downstream work.

Remediation source commit: recorded after commit/push.
Final handoff/tracker commit: recorded after commit/push.

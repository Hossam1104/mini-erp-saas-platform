# MESP-131 Moving Weighted Average Valuation Architecture

**Status:** Final valuation-integrity remediation complete; pending GPT-5.6 Sol delta acceptance and merge<br>
**Date:** 24 August 2026<br>
**Capability:** MESP-131 — Moving Weighted Average valuation, reconciliation, and inventory reporting<br>
**Base main:** `b470179e1d18ef75c0a9247b2340407da6220dc4`<br>
**Starting SHA:** `fa0091ac6a698cbd58b0cb28e57bb36f527ed9b2`<br>
**Remediation implementation commit:** `42794bd6c13d2eae7c8b0b5d4e4c67e73a1ef7e5`<br>
**Final branch SHA:** final documentation handoff commit is reported in the completion response<br>
**Draft PR:** #75 — Open, Draft, unmerged

## Purpose and boundary

MESP-131 adds deterministic Inventory-owned operational valuation over the existing immutable physical stock ledger. Inventory remains owner of physical movements and operational valuation evidence. Finance remains owner of account mapping, periods, balanced journals, subledgers, posting, reversal and accounting reconciliation.

No GL, AP, AR, tax posting, payment, B2B Sales, generic Reporting, external FX provider, statutory/ZATCA/FATOORA, migration/cutover, or Wafra-specific reusable core behavior is added.

## Final valuation-integrity remediation

Known-policy failures are tracked by the derived `ValuationScopeKey`. A
tracking-scoped policy keeps each TrackingIdentity independent, so a missing
FX/source-cost/current-MWA/transfer/correction/calculation predecessor in
LOT-A stops only LOT-A. A non-tracking policy deliberately derives an empty
TrackingIdentity and keeps the combined Warehouse/Product/UOM pool stopped.
Missing-policy evidence remains conservative at the base
Company/Branch/Warehouse/Product/UOM pool because scope mode is not yet known.

For an ordinary outbound that fully depletes the stored quantity, the
valuation engine uses the persisted rounded prior average for its formula,
but the actual movement value is the complete stored prior value. It persists
the unsigned formula value and the explicit absolute rounding bridge:

`RoundingAdjustmentAmount = ActualMovementValue - FormulaMovementValue`

The resulting quantity, value, and average are all zero. Partial outbound
continues to use the normal rounded formula and does not force closeout. A
full-depletion correction restores the actual original value and carries the
formula/rounding lineage into the linked reversal evidence. Finance handoff
uses the actual absolute `BaseAmount`, directional `SignedBaseAmount`, and
the same rounding evidence; Finance does not infer the residual.

Valuation state transitions reject negative state and zero-quantity/non-zero-
value writes. Reconciliation independently detects legacy or corrupt
zero-quantity/non-zero-value and negative state as `ValuationMismatch`, and a
Warehouse summary with any mismatch is incomplete and partial.

The formal additive migration
`20260823211902_MESP131SolFinalValuationIntegrity` adds nullable
`MovementValuationEvents.FormulaMovementValue`, nullable
`MovementValuationEvents.RoundingAdjustmentAmount`, and required/default-zero
`FinanceValuationHandoffs.RoundingAdjustmentAmount`. The preceding MESP-131
migrations remain unchanged.

Final evidence: focused valuation `34/34`; SQL Server safety `39/39` against
disposable LocalDB; full backend `952/952` with zero failures/skips; Release
build 0 warnings/errors; Angular `254/254`; focused/full Chromium `5/5` and
`32/32`; initial bundle `499.94 kB`; valuation lazy chunk `35.96 kB`; both
npm audits report 0 vulnerabilities.

## Durable ledger ordering

Future valuation does not use `PostedAt` or `EffectiveDate` as transaction ordering authority. Every physical Inventory movement receives a durable Company-scoped `long` `LedgerSequence` under Tenant + Company ownership.

The MESP-131 migration deterministically bootstraps pre-MESP-131 movement sequence by Tenant, Company, `PostedAt`, and movement ID. This is a one-time pre-production starting order and is not claimed to reconstruct historical database commit order.

## Valuation policy

Valuation is versioned and configuration-led. Policy facts include Tenant, Company, effective period, scope mode, functional currency, quantity/unit-cost/amount precision and explicit rounding mode. Supported scope is Warehouse/Product/UOM or Warehouse/Product/UOM/Tracking identity. Version numbers are assigned as `max + 1` within the Company series and policy rows are immutable.

Current state and scope-anchor identity is the physical valuation pool, not
PolicyId: Tenant, Company, Branch, Warehouse, Product, UOM, and TrackingIdentity
only when configured by the selected scope mode. A compatible policy transition
preserves state and records current policy metadata; currency, scope, precision,
or rounding incompatibility fails closed with a rebaseline error.

No Tenant/customer-specific valuation rule is hard-coded.

## MWA calculation

Inbound:

`NewQuantity = PriorQuantity + InboundQuantity`

`InboundValue = InboundQuantity × BaseUnitCost`

`NewValue = PriorValue + InboundValue`

`NewAverage = NewValue / NewQuantity` when quantity is non-zero.

Outbound:

`MovementUnitCost = PriorAverageCost`

`MovementValue = OutboundQuantity Ã— PriorAverageCost`

`NewQuantity = PriorQuantity - OutboundQuantity`

`NewValue = PriorValue - MovementValue`

`NewAverage = NewValue / NewQuantity` when quantity is non-zero.

Calculations use decimal arithmetic and policy-defined precision/rounding. The
outbound input is the persisted rounded prior average, and an empty state using
CurrentMovingAverage for a positive adjustment/count variance becomes Pending
with `current_moving_average_unavailable` rather than a zero-valued application.
Negative valuation state is blocked.

## Valuation state and immutable history

Current valuation state is a mutable projection. Applied valuation evidence is append-only and snapshots movement lineage, policy/version, currency, Exchange Rate identity/version/provenance, prior state, movement value, new state, actor and correlation. Original valuation evidence is never rewritten.

## Pending predecessor rule

If a movement cannot be valued because authoritative policy, source cost, FX,
linked valuation or another required fact is unavailable, it creates durable
Pending/Blocked evidence. A missing-policy movement uses the conservative
Company/Branch/Warehouse/Product/UOM predecessor pool and blocks later movement
in that pool until coverage exists. Unrelated valuation pools may continue.

## Source valuation

- Opening Balance uses posted source unit cost/currency.
- Goods Receipt resolves authoritative Purchase Order cost/currency lineage.
- Foreign currency requires the exact active effective-dated MESP-120 Exchange Rate version.
- Stock Issue, negative Adjustment and negative Count Variance consume current MWA.
- Positive Adjustment/Count Variance use configured treatment only.
- Supplier Return uses configured current-MWA or linked-receipt treatment.
- Customer Return stays Pending until authoritative Sales delivery valuation exists.

## Corrections

Physical corrections append new linked movements. Full valuation reversal uses original movement value exactly; partial reversal uses deterministic quantity-pro-rata treatment. Original valuation evidence remains immutable.

Authoritative revised-source persistence remains a provider-required seam; arbitrary user-entered replacement value is not accepted.

## Warehouse Transfer and In Transit

Transfer Shipment consumes source-Warehouse MWA. In-Transit quantity is shipped
less physical receipt, resolved loss, and physical return, never below zero;
In-Transit value covers only the remaining quantity at inherited shipment unit
valuation. Destination receipt inherits shipment valuation. Loss and return
preserve linked transfer valuation lineage. Missing prerequisite shipment
valuation leaves quantity truthful and value status Pending.

## Multi-currency evidence

MESP-131 reuses MESP-120 Exchange Rate identities/versions. Same-currency valuation uses explicit rate-one evidence. Foreign-currency valuation requires correct active direct source/target rate version for the effective date. No browser-authored rate, external feed, inverse guess or silent latest-rate fallback is authoritative.

## Concurrency and idempotency

Valuation uses durable pool serialization, Serializable persistence, optimistic
concurrency and database uniqueness so multiple workers cannot apply a movement
twice or fork state. Process and correction fingerprints are deterministic
SHA-256 values bounded for SQL storage. Process and policy creation reuse the
existing Inventory durable idempotency infrastructure; same-key/different-
fingerprint replays are conflicts, corrupt replay is a safe failure, and a
first-scope insert race is classified as a safe conflict.

## Finance handoff

Applied valuation produces versioned Inventory-to-Finance facts: movement/source
lineage, LedgerSequence, quantity, functional currency, base unit cost, absolute
non-negative BaseAmount, Direction, SignedBaseAmount (Inbound positive and
Outbound negative), transaction currency, Exchange Rate evidence, policy/version
and correction references under `inventory-valuation-finance.v1`.

Inventory does not claim Journal/GL completion before MESP-132+ exists.

## Reconciliation and Inventory reporting

The dedicated `/summary` contract aggregates Warehouse physical quantity, valued
quantity and valued amount across Products without exposing a warehouse-level
AverageUnitCost. It reports Pending/Blocked counts, In-Transit quantity/value,
reconciliation status, latest physical/valued sequences, IsComplete/IsPartial,
as-of and freshness. Detailed reconciliation retains per-Product MWA rows.
Current-state reconciliation accepts only safe current-scope filters; effective
date and sequence history remain report/history evidence rather than a mutation
or false period reconciliation.

Bounded Inventory views:
1. Valuation Summary
2. MWA Cost History
3. Pending / Blocked Valuation
4. Inventory Reconciliation
5. In-Transit Valuation
6. Finance Handoff Evidence
7. Valuation Correction History

CSV export is Tenant-authorized, bounded and audited. Generic Reporting remains MESP-139.

## Angular

`/app/inventory/valuation` is lazy-loaded, server-scoped, EN/AR and RTL. It
exposes a real aggregate summary plus per-Product reconciliation evidence
without client Tenant authority or client-authored cost/FX facts. The client
selects the current effective policy and excludes future versions; it does not
use `summary()[0]` or aggregate AverageUnitCost. Finance handoff displays
Direction and signed-value meaning. SAR presentation is used only when the
actual configured currency is SAR.

## Persistence and migration

Formal additive Inventory migrations:

`20260823124304_MESP131MovingWeightedAverageValuation`

`20260823180537_MESP131SolFinancialIntegrityRemediation`

The remediation migration removes PolicyId from authoritative pool uniqueness,
adds current-policy metadata and policy lineage/version backstops, makes pending
policy evidence truthful, adds Finance Direction/SignedBaseAmount, and preserves
the old MESP-131 migration byte-for-byte. MESP-128/129/130 migrations are not
rewritten.

## Reported validation pending Sol acceptance

- Focused MESP-131 valuation: `27/27`
- Inventory regression: `52/52`
- SQL Server safety: `38/38` (previous baseline `32`)
- Full backend disposable-LocalDB suite: `944/944`, 0 failed, 0 skipped
- Release build: 0 warnings / 0 errors
- Angular: `254/254` across 35 spec files
- Production initial bundle: `499.94 kB`
- Valuation lazy chunk: `35.96 kB`
- Focused Chromium: `5/5`
- Full Chromium: `32/32`
- npm audits: 0 vulnerabilities
- `frontend/assets`: untouched

These are executor-reported results. MESP-131 remains In Progress until Sol accepts the exact live branch head and PR #75 is subsequently merged.

## Jira traceability

- MESP-131 comment `11779`
- MESP-8 comment `11780`
- MESP-54 comment `11781`
- MESP-53 comment `11782`
- MESP-113 comment `11783`
- MESP-120 comment `11784`
- MESP-132 comment `11785`
- MESP-139 comment `11786`
- Sol acceptance comment `11788`
- Sol delta acceptance comment `11789`

No Jira writes were performed by this implementation session.

Approved BRDs and decision packs remain historically unchanged.

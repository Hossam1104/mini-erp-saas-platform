# MESP-131 Moving Weighted Average Valuation Architecture

**Status:** Implementation handoff; pending GPT-5.6 Sol acceptance and merge  
**Date:** 23 August 2026  
**Capability:** MESP-131 â€” Moving Weighted Average valuation, reconciliation, and inventory reporting  
**Base main:** `b470179e1d18ef75c0a9247b2340407da6220dc4`  
**Implementation commit:** `bf491c867b554b2c1f3b091b5196bf82199e161d`  
**Implementation handoff tip before documentation reconciliation:** `39fa538d3c6968476927f01c19669715cdc1147f`  
**Draft PR:** #75 â€” Open, Draft, unmerged

## Purpose and boundary

MESP-131 adds deterministic Inventory-owned operational valuation over the existing immutable physical stock ledger. Inventory remains owner of physical movements and operational valuation evidence. Finance remains owner of account mapping, periods, balanced journals, subledgers, posting, reversal and accounting reconciliation.

No GL, AP, AR, tax posting, payment, B2B Sales, generic Reporting, external FX provider, statutory/ZATCA/FATOORA, migration/cutover, or Wafra-specific reusable core behavior is added.

## Durable ledger ordering

Future valuation does not use `PostedAt` or `EffectiveDate` as transaction ordering authority. Every physical Inventory movement receives a durable Company-scoped `long` `LedgerSequence` under Tenant + Company ownership.

The MESP-131 migration deterministically bootstraps pre-MESP-131 movement sequence by Tenant, Company, `PostedAt`, and movement ID. This is a one-time pre-production starting order and is not claimed to reconstruct historical database commit order.

## Valuation policy

Valuation is versioned and configuration-led. Policy facts include Tenant, Company, effective period, scope mode, functional currency, quantity/unit-cost/amount precision and explicit rounding mode. Supported scope is Warehouse/Product/UOM with optional Tracking identity.

No Tenant/customer-specific valuation rule is hard-coded.

## MWA calculation

Inbound:

`NewQuantity = PriorQuantity + InboundQuantity`

`InboundValue = InboundQuantity Ã— BaseUnitCost`

`NewValue = PriorValue + InboundValue`

`NewAverage = NewValue / NewQuantity` when quantity is non-zero.

Outbound:

`MovementUnitCost = PriorAverageCost`

`MovementValue = OutboundQuantity Ã— PriorAverageCost`

`NewQuantity = PriorQuantity - OutboundQuantity`

`NewValue = PriorValue - MovementValue`

`NewAverage = NewValue / NewQuantity` when quantity is non-zero.

Calculations use decimal arithmetic and policy-defined precision/rounding. Negative valuation state is blocked.

## Valuation state and immutable history

Current valuation state is a mutable projection. Applied valuation evidence is append-only and snapshots movement lineage, policy/version, currency, Exchange Rate identity/version/provenance, prior state, movement value, new state, actor and correlation. Original valuation evidence is never rewritten.

## Pending predecessor rule

If a movement cannot be valued because authoritative policy, source cost, FX, linked valuation or another required fact is unavailable, it remains Pending/Blocked. Later movement in the same valuation scope cannot leap over it. Unrelated valuation scopes may continue.

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

Transfer Shipment consumes source-Warehouse MWA. In-Transit quantity/value remains visible between shipment and receipt. Destination receipt inherits shipment valuation. Loss and return preserve linked transfer valuation lineage. Missing prerequisite shipment valuation blocks downstream valuation.

## Multi-currency evidence

MESP-131 reuses MESP-120 Exchange Rate identities/versions. Same-currency valuation uses explicit rate-one evidence. Foreign-currency valuation requires correct active direct source/target rate version for the effective date. No browser-authored rate, external feed, inverse guess or silent latest-rate fallback is authoritative.

## Concurrency and idempotency

Valuation uses durable scope serialization, Serializable persistence, optimistic concurrency and database uniqueness so multiple workers cannot apply a movement twice or fork state. Public mutations follow existing antiforgery, server Tenant context, correlation, durable idempotency/fingerprint replay and audit conventions.

## Finance handoff

Applied valuation produces versioned Inventory-to-Finance facts: movement/source lineage, LedgerSequence, quantity, functional currency, base unit cost/value, transaction currency, Exchange Rate evidence, policy/version and correction references.

Inventory does not claim Journal/GL completion before MESP-132+ exists.

## Reconciliation and Inventory reporting

Inventory reconciliation compares physical quantity to valued quantity and exposes value, MWA cost, latest physical/valued sequences, pending/blocked counts, oldest pending movement, In-Transit quantity/value, Finance handoff state, policy/currency, as-of and freshness.

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

`/app/inventory/valuation` is lazy-loaded, server-scoped, EN/AR and RTL. It exposes valuation/reconciliation evidence without client Tenant authority or client-authored cost/FX facts. SAR presentation is used only when the actual configured currency is SAR.

## Persistence and migration

Formal additive Inventory migration:

`20260823124304_MESP131MovingWeightedAverageValuation`

It adds durable movement sequence plus Inventory-owned valuation policy/state/evidence/handoff structures and related uniqueness/concurrency backstops. MESP-128/129/130 migrations are not rewritten.

## Reported validation pending Sol acceptance

- Focused MESP-131 valuation: `7/7`
- Inventory regression: `52/52`
- Full backend disposable-LocalDB suite: `919/919`, 0 failed, 0 skipped
- Release build: 0 warnings / 0 errors
- Angular: `248/248` across 34 spec files
- Production initial bundle: `499.94 kB`
- Valuation lazy chunk: `35.43 kB`
- Focused Chromium: `1/1`
- Full Chromium: `28/28`
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

Approved BRDs and decision packs remain historically unchanged.
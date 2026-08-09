# Next session - M95-SL-03 Product Identity implementation only

This is the exact next bounded implementation session after the completed
MESP-101 readiness/documentation gate. Do not start this task automatically in
the current chat. The next fresh session must re-read the current Jira item,
the approved Product-readiness baseline, the BRD/LIS, ADR-002, ADR-005,
ADR-006, ADR-011, the Foundation specification, the glossary, the delivery
plan, and `.ai/CURRENT_STATE.md` before changing source.

## Exact objective

Implement only the approved M95-SL-03 Product identity slice. Product and Item
remain one Release-1 master-data identity with no separate variant/Item entity
or variant behavior. The implementation must preserve Tenant isolation,
server-derived authority, Product-owned authorization, append-before-effect
audit evidence, optimistic concurrency, and the bounded lifecycle and
identifier rules from MESP-101.

## Entry gates

- MESP-101 is Done in Jira with its merged readiness PR and final repository
  state recorded in `.ai/CURRENT_STATE.md`.
- The dedicated Product implementation Jira item is explicitly activated and
  is the only active implementation item for this session.
- The six Product-only bounds remain unchanged: MD-OD-001, MD-OD-003,
  MD-OD-005, MD-OD-008, MD-OD-010, and MD-OD-011. Do not generalize the
  Category/UOM policy or silently resolve unrelated decisions.
- ADR-002 and ADR-006 are revalidated: `MiniErp.Api` is the composition root,
  `MiniErp.App` is EF-free, `MiniErp.Contracts` is infrastructure-free, and
  `MiniErp.Infrastructure` owns provider/EF/module persistence seams.
- The configured SQL Server/provider and migration gates are explicitly
  available before any migration or production-database claim is made.

## Allowed implementation boundary

- Product master identity and Product-owned persistence in the module-owned
  infrastructure boundary.
- Product/Item identity fields, English-required and Arabic-conditional names,
  Category/Base-UOM references, hybrid Tenant-unique SKU/barcode identifiers,
  Product-side tracking configuration, and Active/Inactive lifecycle.
- Server-derived Tenant and capability authorization using Product-owned policy
  identifiers; no client-supplied Tenant or scope authority.
- Append-before-effect audit evidence, correlation/actor/session context,
  before/after payloads, reason, and stale-write/concurrency behavior.
- Focused API/application contracts and tests needed to demonstrate this slice,
  without implementing downstream Inventory, Procurement, Sales, Tax, or UI
  behavior.

## Hard exclusions

- No Product variants, separate Item identity, Wafra-specific semantics, or
  Retail POS behavior.
- No Inventory batch/lot/serial/expiry records or operational tracking
  behavior; Product stores configuration only until a separately authorized
  Inventory slice consumes it.
- No Tax master/classification behavior, Price List, Supplier, Business
  Customer, approval catalogue, or downstream transaction behavior.
- No EAN/GS1/barcode symbology or checksum policy, SKU sequence/generator
  policy, localized search/collation/tokenization, RTL document behavior, or
  other ADR-011 decision without a separately approved gate.
- No cross-Tenant sharing, direct cross-module table access, production
  database provisioning, or migration execution without the applicable gates.

## Required validation and handoff

- Run the focused Product tests plus applicable non-SQL architecture,
  composition, authorization, Tenant-isolation, audit, concurrency, and API
  boundary tests.
- Prove no client Tenant/scope authority, no cross-Tenant reference, no
  Product-owned policy reuse of Category/UOM policy, no false success after
  audit failure, and no source outside the allowed Product boundary.
- Record migration/provider/SQL validation truthfully; do not claim production
  readiness from a build or unit tests alone.
- Review the complete diff, update `.ai/CURRENT_STATE.md`, every genuinely
  affected state/plan/tracker document, Jira, and this task to the next exact
  bounded session. Commit, push, merge only when clean and unblocked, then
  stop for ChatGPT review. Never execute the following task automatically.

## Stop conditions

Stop on unresolved Product decisions, Tenant-isolation or authorization
weakness, accounting/data-integrity risk, destructive migration/data-loss risk,
legal/privacy or external-validation dependency, missing credentials or
production/provider infrastructure, or material scope/architecture change.
Keep MESP-48, MESP-49, and MESP-50 open unless their own separately authorized
work resolves them.

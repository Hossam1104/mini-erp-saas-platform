# Next session — M95-SL-03 Product identity readiness and decision gate

MESP-99 / M95-SL-02 Category and UOM is complete at the approved bounded
scope. This root task is a handoff for a future fresh session only; do not
start M95-SL-03 automatically in the current chat.

## Exact objective

Prepare and review the documentation/readiness gate for the Product identity
slice. Do not implement Product persistence or Product behavior in this
readiness task. Establish the exact owner-approved scope and acceptance
traceability needed before any Product source change is authorized.

## Entry gates

- A dedicated Jira implementation/readiness item for M95-SL-03 is explicitly
  activated and is the only active implementation item.
- MESP-99 is confirmed Done with its final PR, merge, validation, and Jira
  evidence in `.ai/CURRENT_STATE.md`.
- MD-OD-003 (SKU/Barcode coding), MD-OD-010 (tracking), and MD-OD-011
  (Product-versus-Item identity) are resolved or explicitly bounded by the
  Owner for the affected Product slice. Do not infer any of them from a
  recommendation.
- The applicable MD-OD-001, MD-OD-005, and MD-OD-008 scope, approval, and
  lifecycle bounds are revalidated for Product separately; the Category/UOM
  policy must not be generalized.
- ADR-002, ADR-005, ADR-006, ADR-011, the approved BRD, the lean
  implementation specification, glossary, Foundation specification, and
  delivery plan are reread from their current authoritative versions.

## Required deliverables

- A bounded Product identity/readiness note or specification correction with
  explicit inclusions, exclusions, open decisions, data/authorization/audit
  gates, and acceptance tests.
- A traceability update for the affected BRD/LIS and delivery-plan state only;
  preserve the remaining MD-OD-001 through MD-OD-011 register.
- No Product/Item/SKU/Barcode/tracking tables, entities, migrations,
  endpoints, or business behavior until a later explicitly activated
  implementation task authorizes them.

## Stop conditions and session boundary

Stop on unresolved owner decisions, Tenant-isolation or authorization
weakness, accounting/data-integrity risk, destructive migration/data-loss
risk, legal/privacy or external-validation dependency, credential/production
infrastructure blocker, or material scope/architecture change. Keep
MESP-48, MESP-49, and MESP-50 open. At the end of the future session, update
this task to the next exact bounded session and stop; never execute the next
session automatically.

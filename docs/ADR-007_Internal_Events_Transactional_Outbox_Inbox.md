# ADR-007 — Internal events and transactional outbox/inbox

| Field | Decision |
|---|---|
| Status | Foundation implementation baseline; MESP-92 single-effect correction In Progress; production delivery provider deferred |
| Date | 4 August 2026; reconciled 6 August 2026 |
| Owners | Solution Architecture / Application Engineering |
| Related Jira | MESP-61, MESP-64, MESP-91, MESP-92, MESP-48, MESP-50 |
| Supersedes | None |

## Context

Durable work needs a recoverable hand-off from application state to a later
effect. The first implementation must preserve exact Tenant and organization
scope, survive duplicate delivery, and remain practical for a single developer.

## Decision

1. The smallest approved seam is a Tenant-owned transactional outbox message
   created with the durable-work record. The event identity is stable and the
   idempotency key is unique within the Tenant boundary.
2. An inbox record keyed by `(TenantId, EventId)` makes duplicate delivery a
   safe no-op. A protected effect is acknowledged only after the effect
   callback succeeds; failures use a bounded retry and safe dead-letter state.
3. Outbox and inbox records carry Tenant, applicable Company/Branch/Warehouse
   scope, work identity and correlation, but never payload secrets, tokens,
   cookies, private file bytes or provider exception text.
4. MESP-61 supplies the typed contract and deterministic in-memory adapter for
   tests/development. No broker, Kafka/RabbitMQ cluster or production provider
   is selected.
5. A production relational implementation must preserve the same atomicity,
   unique-key and Tenant-ownership invariants and will be validated through
   MESP-64 before any production decision.

## Alternatives considered

- A broker-first design was rejected because no distributed infrastructure is
  approved for the Foundation and it would obscure the transaction boundary.
- Fire-and-forget in-process events were rejected because process failure could
  lose a required protected effect.
- A global event table or dispatcher query was rejected because it would create
  an unscoped Tenant business-data path.

## MESP-92 single-effect correction (In Progress)

The inbox uniqueness marker described in point 2 is superseded by a
server-owned `DurableWorkEffectKey` (Tenant, WorkItemId, OperationId) guarded
by `IDurableWorkEffectGuard`/`IDurableWorkEffectExecutor`. Reservation of
that key is the single non-reversible boundary; a protected effect is
acknowledged as `Applied` (recorded Completed, never repeats) only after the
effect callback returns normally. Outbox dispatch now reports one of three
explicit outcomes:

- **Applied** — the effect ran (or was proven already Completed) and is
  recorded so duplicate delivery safely replays the same result without
  repeating the effect.
- **NotAppliedRetryable** — the effect is proven not to have started (for
  example, a live-authority provider outage or cancellation before the
  reservation boundary); bounded retry may run.
- **OutcomeUnknown** — the effect boundary was reached but a crash,
  cancellation or completion-recording failure means its outcome cannot be
  proven; delivery is never automatically repeated and requires
  reconciliation.

The same effect key and guard protect both the outbox effect path and the
direct worker/dispatcher handler path, so a duplicate submission or a
concurrent redelivery across either path still produces exactly one effect
invocation. Provider exception text is never persisted in outbox or audit
evidence; only a safe, bounded category and reason are recorded.

## Consequences and guardrails

- Every new event requires an owning module, stable event type, Tenant/scope
  facts, correlation and idempotency behavior.
- Duplicate delivery must be demonstrably single-effect; inbox uniqueness is
  part of the persistence contract, not merely a convention.
- Retry delay and attempt count are bounded. Dead-letter evidence contains only
  a safe category/reason and identifiers allowed by the audit policy.
- A production exporter, broker, delivery provider, retention period, purge
  policy, legal hold and residency are not implied by this ADR.

## Gates

MESP-48 owns supported-volume/performance and operational capacity evidence.
MESP-50 owns retention, privacy, legal hold, purge, residency, backup and
restoration decisions. No production readiness claim is made until both gates
and the applicable operational ADRs are closed.

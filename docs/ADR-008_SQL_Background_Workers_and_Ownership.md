# ADR-008 — SQL-backed job execution and worker ownership

| Field | Decision |
|---|---|
| Status | Foundation worker seam; deployment topology deferred |
| Date | 4 August 2026 |
| Owners | Solution Architecture / Background Processing |
| Related Jira | MESP-61, MESP-64, MESP-91, MESP-48, MESP-50 |
| Supersedes | None |

## Context

Background work must not lose Tenant identity when execution is detached from
the request that created it. It must also avoid a privileged global scan of
business repositories and must provide bounded retry, lease ownership and
safe failure evidence.

## Decision

1. Work is claimed from an owned durable-work store using an atomic lease and
   optimistic concurrency version. Only one worker can hold an active lease.
2. The worker/outbox consumer reconstructs a trusted execution context only
   from a server-issued `VerifiedDurableWorkAuthorization`. That result binds
   the stored Tenant, exact operation descriptor, authorization path,
   exact organization scope, actor/session and correlation facts after live
   Identity revalidation through narrow ports. Stored expiry, permission and
   scope snapshots are evidence only; they are not current authorization.
   Identity-owned hierarchy resolution proves Tenant -> Company -> Branch ->
   Warehouse ownership and downward containment. Missing, mismatched or
   unauthorized context fails closed; there is no fallback Tenant.
   `PlatformGovernanceContext` cannot execute Tenant work.
3. A dispatcher resolves one typed handler by the authoritative operation
   descriptor, including its exact permission, allowed authorization paths and
   scope policy. It may inspect only the module-owned envelope needed to
   claim/execute that work and must not enumerate Tenant business data globally.
4. Handler outcomes are success, bounded retry or safe dead letter. Expired
   leases can be reclaimed; an active lease cannot be stolen.
5. MESP-61 implements this seam with a deterministic local adapter. The
   production SQL-backed worker store and hosting topology remain implementation
   and deployment decisions validated by MESP-64 and later operational review.

MESP-91 adds the live authority correction to this seam. A failed current
User/session, Membership, SupportGrant/SupportCase, Permission, scope or
organization-ownership check is a terminal `AuthorizationDenied` dead letter
with safe evidence; it does not retry indefinitely and it cannot reach a
handler or protected outbox effect. An authority-provider exception is
`TemporarilyUnavailable`/`ProviderUnavailable` and follows bounded retry; a
cancellation is a distinct recoverable `Cancelled` outcome. Neither is
converted into an authorization denial, and the lease/outbox state transition
uses the minimal detached cancellation token needed to preserve recovery.
The operation catalogue is the only source of the exact permission and
handler binding; unknown or mismatched descriptors fail closed. The
SQL/MESP-64 probes validate persistence, lease, transaction and idempotency
behavior only; they are not worker-authorization evidence.

## Alternatives considered

- A hosted worker that trusts request/client Tenant input was rejected because
  it permits context confusion and cross-Tenant execution.
- A global platform governance context was rejected as a Tenant execution path;
  governance remains purpose-bound control-plane work.
- An unbounded retry loop was rejected because it can amplify provider failure
  and hide dead-letter conditions.

## Consequences and guardrails

- Stored owner verification is required on read, claim and completion. The
  worker never mutates ownership or scope.
- The initiating Tenant, verified organization ownership, authorized scope and
  live Identity authority are revalidated immediately before handler and
  outbox-effect dispatch. True denials are terminal and safe; provider
  unavailability and cancellation have bounded recovery outcomes.
- Lease duration, maximum attempts and backoff are bounded by code-level
  contracts; production values require an approved operational decision.
- The host may later run a dedicated worker process or the same deployable
  application, but this ADR does not select production topology, capacity,
  scheduler, region or provider.
- Worker telemetry and audit are allow-listed and redacted; payloads, tokens,
  cookies, private bytes and provider exception text are excluded.

## Gates

MESP-48 owns supported-volume, queue depth, throughput, lease and recovery
evidence. MESP-50 owns retention, privacy, legal hold, purge, residency,
backup and restoration requirements for durable records. No production worker
deployment or retention claim is authorized by this ADR.

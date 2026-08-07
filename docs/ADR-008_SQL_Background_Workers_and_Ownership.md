# ADR-008 — SQL-backed job execution and worker ownership

| Field | Decision |
|---|---|
| Status | Foundation worker seam; MESP-91 live authority correction merged and Done (PR #20, `f2cde57400fed470ab048776e05b56f353b36890`); MESP-92 single-effect/immutable-payload correction merged and Done (PR #22, `322341e70e56270797d5770b4b90342c20b7833e`); deployment topology deferred |
| Date | 4 August 2026; reconciled 6 August 2026; reconciled again 7 August 2026 |
| Owners | Solution Architecture / Background Processing |
| Related Jira | MESP-61, MESP-64, MESP-91, MESP-92, MESP-93, MESP-48, MESP-50 |
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
   the exact stored WorkItemId and Tenant, operation descriptor, correlation,
   organization boundary, execution TenantContext, authorization path,
   Membership or SupportGrant, actor and session after live Identity
   revalidation through narrow ports. `DurableWorkExecutionContext` defensively
   repeats the same exact-binding check. Stored expiry, permission and scope
   snapshots are evidence only; they are not current authorization.
   Identity-owned hierarchy resolution proves Tenant -> Company -> Branch ->
   Warehouse ownership and downward containment. Missing, mismatched or
   unauthorized context fails closed; an ordinary context requires a canonical
   explicit selected scope, while a SupportGrant uses the current case-bound
   stored grant scope rather than a context marker. There is no fallback Tenant.
   `PlatformGovernanceContext` cannot execute Tenant work.
3. A dispatcher resolves one typed handler by the authoritative operation
   descriptor, including its exact permission, allowed authorization paths and
   scope policy and mandatory security-evidence requirement. A descriptor that
   opts out of mandatory evidence cannot create work, register a handler,
   dispatch, or produce verified authority. Only the Identity issuer may issue
   shipping verified authority; a structural architecture test allow-lists that
   issuer and keeps test-only fixtures in the test project. The dispatcher may
   inspect only the module-owned envelope needed to claim/execute that work and
   must not enumerate Tenant business data globally.
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
handler binding; unknown, mismatched or non-evidenced descriptors fail closed.
The focused H91-03/H91-04 regression suite covers missing/malformed ordinary
scope, support-grant authority, broader/sibling scope and every exact stored
binding. The
SQL/MESP-64 probes validate persistence, lease, transaction and idempotency
behavior only; they are not worker-authorization evidence.

MESP-92 adds single-effect and immutable-payload guarantees to this seam. A
submitted payload is captured immediately into an immutable, checksummed
envelope through an explicit `IDurableWorkPayloadRegistry`; no original
caller payload reference is retained, and unknown types, handler/payload
mismatches, checksum tampering and oversized payloads fail closed before a
handler runs. A focused ChatGPT re-review of PR #22 requires production code
to expose no payload-mutation fault-injection hook; checksum-corruption is
exercised only through bounded test-project reflection, and a custom codec's
encode/decode exception is always wrapped in the safe
`DurableWorkPayloadException`.

The dispatcher resolves a stable, purpose-qualified `DurableWorkEffectKey`
(Tenant, `DurableWorkEffectPurpose.Handler`, WorkItemId, OperationId — an
outbox-purpose key additionally carries the immutable `EventId`), and every
registered handler invocation is routed exclusively through
`IDurableWorkEffectExecutor.ExecuteHandlerEffectAsync` (architecture-enforced;
a handler cannot bypass this while remaining a protected durable-work
handler). The store's outbox dispatch and the dispatcher's handler execution
share one authoritative executor, obtained only through the single approved
composition entry point `DurableWorkLocalRuntime.Create(operationCatalogue,
payloadRegistry)` (H92-03 focused review correction: it is the only place
shipping code may construct the guard, executor, store or dispatcher, all
four constructors now being `internal`, and a syntax-tree architecture test
proves no other shipping construction site exists); the purpose and
EventId in the key keep the two effect categories independent within that one
shared guard. `DurableWorkLocalRuntime`'s public surface is limited to
`Store` and `Dispatcher` (H92-05 focused review correction): `EffectGuard`
and `EffectExecutor` are internal properties, and `IDurableWorkEffectGuard`,
`InMemoryDurableWorkEffectGuard`, `IDurableWorkEffectExecutor` and
`DurableWorkEffectExecutor` are internal types, so a shipping caller holding
the runtime cannot reserve, release, complete or mark an effect uncertain
outside the executor — closing a release-mid-flight path that would have let
a second dispatch execute an already-reserved effect twice. Reservation of
that key is the single non-reversible boundary:
an interruption before it permits bounded retry, and the protected callback
must return an explicit `DurableWorkProtectedEffectResult` outcome —
`Applied`, `NotAppliedRetryable`, `OutcomeUnknown` or `TerminalNotApplied` —
so a bare generic retry can never release a reservation after an effect may
already have run. A caught exception or cancellation observed inside the
running process after that boundary is recorded `OutcomeUnknown` — a
dedicated, Tenant-scoped reconciliation lifecycle state, never automatically
repeated and readable only through
`IDurableWorkStore.ReadUncertainEffectsAsync`, which requires a server-issued
`VerifiedDurableWorkReconciliationAuthorization` scoped to the exact
Tenant/Company/Branch/Warehouse boundary and its verified descendants only
(H92-04 focused review correction; a sibling organization is never visible).
The guard preserves its own safe reason on the OutcomeUnknown transition
(O92-01), but `IDurableWorkEffectGuard.GetOutcomeUnknownReason` is not a
public raw-key evidence path: the interface is internal (M92-05 focused
review correction), so the only publicly reachable uncertain-effect evidence
remains the scope-authorized `ReadUncertainEffectsAsync` port above. A
duplicate dispatch of an already-Completed effect replays the exact recorded
safe result instead of re-invoking the handler. `InMemoryRelationalDurableWorkStore`/
`IRelationalDurableWorkStore` are renamed to `InMemoryDurableWorkStore`/
`IDurableWorkStore` to remove the misleading relational/SQL-backed
implication.

**Maturity boundary, corrected:** this in-memory adapter preserves only a
caught post-boundary interruption (an exception or cancellation observed
inside the running process) as `OutcomeUnknown`. An actual process crash
loses the adapter's in-memory guard and lifecycle state entirely; that state
loss is **not** represented as `OutcomeUnknown` or any other recorded
outcome. Production durable crash recovery remains deferred to a future
SQL/durable provider; this adapter remains a non-crash-durable Foundation
seam only.

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

## Composition status

Verified on 6 August 2026: `DurableWorkLocalRuntime`,
`InMemoryDurableWorkStore`, `DurableWorkDispatcher` and
`TenantDurableWorkWorker` are **not referenced by `MiniErp.Api`**. This ADR
describes a contract and a local, in-memory, non-crash-durable adapter proven
by automated tests; **no worker is composed into the running host**, no worker
is scheduled, and nothing here is a production capability. A future host
composition root must call `DurableWorkLocalRuntime.Create` exactly once and
reuse the returned instance; the syntax-tree architecture test enforces that
no other shipping construction site exists. Verified at head
`576996f94ae9ddc251767445a7ebddd60c492c45` (H92-05/M92-05 correction, 7 August
2026): `MiniErp.Api` still does not reference any durable-work type, so
tightening `DurableWorkLocalRuntime`'s public surface to `Store`/`Dispatcher`
only changed nothing reachable from the host.

**H92-06/M92-07 correction (7 August 2026, head `e991641`):** at every head up
to and including `576996f94ae9ddc251767445a7ebddd60c492c45`, `MiniErp.App`
still granted `[assembly: InternalsVisibleTo("MiniErp.Api")]`. **That grant
alone made the preceding paragraphs' `internal` claims incomplete**: a friend
assembly sees another assembly's `internal` members exactly as if they were
public, so `MiniErp.Api` could still reach `EffectGuard`/`EffectExecutor`,
construct `InMemoryDurableWorkEffectGuard`/`DurableWorkEffectExecutor`
directly, and call `TryReserve`/`Release`/`RecordCompleted`/
`RecordOutcomeUnknown`/`GetOutcomeUnknownReason` — the H92-05/M92-05 `internal`
modifiers narrowed the *source-level* surface but did not close the
*compiled* shipping boundary. The correction removes that grant; `MiniErp.App`
now declares `InternalsVisibleTo` only for `MiniErp.ArchitectureTests`. The
one resulting `MiniErp.Api` compile break was unrelated to durable work
(`FoundationHostSignInResult.Principal`, needed by the sign-in endpoint to
call `HttpContext.SignInAsync`) and was resolved by making that one property
public rather than restoring friend access. No mutable ledger type is public.
M92-07 closes as a direct consequence: `GetOutcomeUnknownReason` is declared
only on the already-internal `IDurableWorkEffectGuard`, so removing the friend
grant removes `MiniErp.Api`'s only path to it too.
`FriendAssemblyPolicyTests.cs` proves this by full Roslyn compilation: source
compiled under the assembly name `MiniErp.Api` fails to compile (`CS0122`)
against the internal ledger surface, while identical source compiled under
`MiniErp.ArchitectureTests` still succeeds.

## Gates

MESP-48 owns supported-volume, queue depth, throughput, lease and recovery
evidence. MESP-50 owns retention, privacy, legal hold, purge, residency,
backup and restoration requirements for durable records. No production worker
deployment or retention claim is authorized by this ADR.
